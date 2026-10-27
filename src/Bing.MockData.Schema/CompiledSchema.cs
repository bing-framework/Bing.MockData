using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Corvus.Text.Json.RuntimeEvaluator;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Schema
{
    /// <summary>可释放、无网络解析、无动态代码的 Draft 2020-12 编译实例。</summary>
    public sealed class CompiledSchema:IDisposable
    {
        private readonly object _gate=new object();
        private JsonSchemaEvaluator _evaluator;
        private JsonSchemaEvaluator _formatEvaluator;
        private IReadOnlyDictionary<string,string> _formatConstraints;
        private readonly FormatValidationPlan _formatPlan;
        private readonly SchemaOptions _options;
        private readonly SchemaGraph _graph;
        private readonly string _json;
        public string Identity {get;}
        private CompiledSchema(string json,IReadOnlyDictionary<string,string> documents,SchemaOptions options)
        {
            _options=options.Copy();long bytes=Encoding.UTF8.GetByteCount(json);if(documents.Count+1>_options.MaxDocuments)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"Schema 文档数超限。");
            _json=json;
            var frozen=new Dictionary<string,byte[]>(StringComparer.Ordinal);foreach(var p in documents){bytes=checked(bytes+Encoding.UTF8.GetByteCount(p.Value));if(bytes>_options.MaxDocumentBytes)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"Schema 输入字节超限。");frozen.Add(p.Key,Encoding.UTF8.GetBytes(p.Value));}
            if(bytes>_options.MaxDocumentBytes)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"Schema 输入字节超限。");
            using(var sha=SHA256.Create()){var content=Newtonsoft.Json.JsonConvert.SerializeObject(new {GeneratorContract="schema-search-v5",Schema=json,Documents=documents.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray(),Options=_options});Identity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-","").ToLowerInvariant();}
            string missingReference=null;
            try
            {
                _graph=new SchemaGraph(json,documents);
                bool Resolve(string uri,out ReadOnlyMemory<byte> result)
                {if(frozen.TryGetValue(uri,out var source)){result=source;return true;}missingReference=uri;result=default;return false;}
                ValidateResources();
                _graph.NormalizeGeneration();
                if(_options.AssertFormat!=false)
                {
                    var plan=new FormatValidationPlan(json,documents,_graph.Dialects,_options.AssertFormat,Identity);
                    if(plan.HasFormats)_formatPlan=plan;
                }
                _evaluator=CompileEvaluator(null,_formatPlan==null?Resolve:(JsonSchemaDocumentResolver)null);

                void ValidateResources()
                {
                    var validators=new Dictionary<string,JsonSchemaEvaluator>(StringComparer.Ordinal);
                    try
                    {
                        _graph.VisitReferencedSchemas((node,dialect)=>
                        {
                            if(!validators.TryGetValue(dialect,out var meta))
                            {
                                meta=JsonSchemaEvaluator.Compile(new JObject{["$ref"]=dialect}.ToString(),new JsonSchemaEvaluatorOptions{DocumentResolver=Resolve,CodeGeneration=JsonSchemaCodeGeneration.Disabled,AssertContent=false,CompileRegularExpressions=false,RegexMatchTimeout=_options.RegexTimeout});
                                validators.Add(dialect,meta);
                            }
                            using(var results=Corvus.Text.Json.JsonSchemaResultsCollector.CreateUnrented(Corvus.Text.Json.JsonSchemaResultsLevel.Detailed))
                            {
                                if(meta.Evaluate(ExactJson.Write(node),results))return;
                                string pointer="";
                                foreach(var result in results.EnumerateResults())if(!result.IsMatch)
                                {string current=result.GetDocumentEvaluationLocationText();if(current.Length>pointer.Length)pointer=current;}
                                if(pointer.StartsWith("#",StringComparison.Ordinal))pointer=pointer.Substring(1);
                                throw new SchemaGenerationException(SchemaFailure.InvalidSchema,"引用资源不符合其元 Schema："+dialect,_graph.Location(node)+pointer){SchemaIdentity=Identity};
                            }
                        });
                    }
                    finally{foreach(var validator in validators.Values)validator.Dispose();}
                }
            }
            catch(SchemaGenerationException){throw;}
            catch(Exception e){throw new SchemaGenerationException(missingReference==null?SchemaFailure.InvalidSchema:SchemaFailure.MissingReference,"Schema 编译失败："+e.Message,inner:e);}
        }
        public static CompiledSchema Compile(string json,IReadOnlyDictionary<string,string> documents=null,SchemaOptions options=null)
        {if(json==null)throw new ArgumentNullException(nameof(json));return new CompiledSchema(json,documents??new Dictionary<string,string>(),options??new SchemaOptions());}
        public bool Validate(string json) => Validate(json,CancellationToken.None);
        /// <summary>在输入、格式处理步骤和取消预算内验证实例；不会强制中断底层单次编译。</summary>
        public bool Validate(string json,CancellationToken cancellationToken)
        {
            int remaining=_options.MaxSearchSteps;
            void Step(){cancellationToken.ThrowIfCancellationRequested();if(remaining--<=0)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"格式验证步骤超限。") {SchemaIdentity=Identity};}
            try{return Validate(json,null,Step);}
            catch(SchemaGenerationException error){error.SchemaIdentity=Identity;throw;}
        }
        private JsonSchemaEvaluator CompileEvaluator(IReadOnlyDictionary<string,string> constraints,JsonSchemaDocumentResolver original=null)
        {
            bool Resolve(string uri,out ReadOnlyMemory<byte> document)
            {
                if(_formatPlan!=null)return _formatPlan.Resolve(uri,constraints,out document);
                if(original!=null)return original(uri,out document);
                document=default;return false;
            }
            return JsonSchemaEvaluator.Compile(_formatPlan?.Json??_json,new JsonSchemaEvaluatorOptions{BaseUri=SchemaGraph.RootUri,DefaultDialect=JsonSchemaDialect.Draft202012,DocumentResolver=Resolve,CodeGeneration=JsonSchemaCodeGeneration.Disabled,AssertFormat=_options.AssertFormat,AssertContent=false,MaxDepth=Math.Max(32,_options.MaxDepth*4),CompileRegularExpressions=false,RegexMatchTimeout=_options.RegexTimeout});
        }
        private bool Validate(string json,Corvus.Text.Json.IJsonSchemaResultsCollector collector,Action step)
        {
            if(json==null)throw new ArgumentNullException(nameof(json));lock(_gate)
            {
                if(_evaluator==null)throw new ObjectDisposedException(nameof(CompiledSchema));
                step();
                if(Encoding.UTF8.GetByteCount(json)>_options.MaxDocumentBytes)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"验证实例输入字节超限。");
                var evaluator=_evaluator;
                if(_formatPlan!=null&&_formatPlan.HasUsedFormats)
                {
                    var constraints=_formatPlan.Constraints(json,_options.RegexTimeout,_options.MaxDocumentBytes,step);
                    if(constraints.Count>0)
                    {
                        bool same=_formatConstraints!=null&&constraints.Count==_formatConstraints.Count;
                        if(same)foreach(var entry in constraints)
                        {step();if(!_formatConstraints.TryGetValue(entry.Key,out var previous)||!string.Equals(entry.Value,previous,StringComparison.Ordinal)){same=false;break;}}
                        if(!same)
                        {
                            step();var compiled=CompileEvaluator(constraints);
                            try{step();}catch{compiled.Dispose();throw;}
                            _formatEvaluator?.Dispose();_formatEvaluator=compiled;_formatConstraints=constraints;
                        }
                        evaluator=_formatEvaluator;
                    }
                }
                step();bool valid=evaluator.Evaluate(json,collector);step();return valid;
            }
        }
        public MockGenerator<JsonSample> Generator(MockContext context)
        {if(context==null)throw new ArgumentNullException(nameof(context));lock(_gate){if(_evaluator==null)throw new ObjectDisposedException(nameof(CompiledSchema));}return context.Generator(Generate,"schema:"+Identity);}
        public JsonSample Generate(MockContext context)
        {
            lock(_gate){if(_evaluator==null)throw new ObjectDisposedException(nameof(CompiledSchema));}
            if(context==null)throw new ArgumentNullException(nameof(context));context.CancellationToken.ThrowIfCancellationRequested();var replay=context.Replay;
            if(_graph.Root.Type==JTokenType.Boolean&&!(bool)_graph.Root)throw new SchemaGenerationException(SchemaFailure.ProvenContradiction,"根 Schema 为 false。",replay:replay){SchemaIdentity=Identity};
            var search=new SchemaSearch(_graph,_options,context);string lastLocation="#",lastInstance="";
            int attempts=0;
            while(search.Remaining>0&&attempts++<context.Limits.MaxAttempts)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var candidate=search.Next();string json=ExactJson.Write(candidate);if(json.Length>context.Limits.MaxTextLength){search.Step();continue;}
                    using(var results=Corvus.Text.Json.JsonSchemaResultsCollector.CreateUnrented(Corvus.Text.Json.JsonSchemaResultsLevel.Detailed))
                    {
                        if(Validate(json,results,search.Step))return new JsonSample(json,Identity,replay);
                        foreach(var failure in results.EnumerateResults())if(!failure.IsMatch)
                        {
                            string instance=failure.GetDocumentEvaluationLocationText();
                            if(instance.Length>=lastInstance.Length){lastLocation=failure.GetSchemaEvaluationLocationText();lastInstance=instance;}
                        }
                    }
                }
                catch(CandidateRejected e){lastLocation=e.Location??"#";lastInstance=e.InstanceLocation??"";}
                catch(SchemaGenerationException e){throw new SchemaGenerationException(e.Failure,e.Message,e.SchemaLocation,e.InstanceLocation,replay,e){SchemaIdentity=Identity};}
                catch(System.Text.RegularExpressions.RegexMatchTimeoutException){throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"正则执行超时。",lastLocation,lastInstance,replay:replay){SchemaIdentity=Identity};}
            }
            throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"在搜索预算内未找到有效实例；此结果不证明 Schema 无解。",lastLocation,lastInstance,replay:replay){SchemaIdentity=Identity};
        }
        public void Dispose(){lock(_gate){_formatEvaluator?.Dispose();_formatEvaluator=null;_formatConstraints=null;_evaluator?.Dispose();_evaluator=null;}}
    }
    /// <summary>在上下文构建时编译并拥有 Schema；身份参与重放。</summary>
    public static class SchemaExtensions
    {
        public static MockOptions UseSchema(this MockOptions options,string name,string json,IReadOnlyDictionary<string,string> documents=null,SchemaOptions limits=null)
        {
            if(options==null)throw new ArgumentNullException(nameof(options));if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Schema 名称不能为空。",nameof(name));var copy=(limits??new SchemaOptions()).Copy();var docs=documents==null?new Dictionary<string,string>():documents.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
            string identity;using(var compiled=CompiledSchema.Compile(json,docs,copy))identity=compiled.Identity;
            return options.UseExtensionFactory("schema:"+name,()=>CompiledSchema.Compile(json,docs,copy),identity);
        }
        public static MockGenerator<JsonSample> Schema(this MockContext context,string name)=>context.Extension<CompiledSchema>("schema:"+name).Generator(context);
    }
    internal sealed class SchemaGraph
    {
        internal const string RootUri="https://mock.invalid/schema/root";
        internal readonly JToken Root;
        private readonly Dictionary<string,JToken> _resources=new Dictionary<string,JToken>(StringComparer.Ordinal);
        private readonly Dictionary<JToken,string> _bases=new Dictionary<JToken,string>();
        private readonly Dictionary<JToken,string> _nodeDialects=new Dictionary<JToken,string>();
        private readonly List<JToken> _roots=new List<JToken>();
        internal SchemaDialect Dialects {get;}
        internal SchemaGraph(string json,IReadOnlyDictionary<string,string> documents)
        {Dialects=new SchemaDialect(documents);Root=ExactJson.Parse(json);if(!(Root is JObject)&&Root.Type!=JTokenType.Boolean)throw new FormatException("Schema 必须为对象或布尔值。");Add(Root,RootUri);foreach(var p in documents)Add(ExactJson.Parse(p.Value),p.Key);}
        private void Add(JToken root,string uri){_resources[uri]=root;_roots.Add(root);Walk(root,uri,SchemaDialect.Default);}
        internal void NormalizeGeneration(){foreach(var root in _roots)Dialects.Normalize(root);}
        private void Walk(JToken node,string uri,string dialect)
        {
            _bases[node]=uri;
            if(!(node is JObject o))return;
            dialect=SchemaDialect.Declared(o,dialect);_nodeDialects[node]=dialect;
            if(o["$id"]?.Type==JTokenType.String){uri=new Uri(new Uri(uri),(string)o["$id"]).AbsoluteUri;_resources[uri]=node;_bases[node]=uri;}
            foreach(var key in new[]{"$anchor","$dynamicAnchor"})if(o[key]?.Type==JTokenType.String)_resources[uri+"#"+(string)o[key]]=node;
            foreach(var child in FormatAssertions.Children(o,key=>Dialects.Enabled(dialect,key)))Walk(child,uri,dialect);
        }
        internal string Location(JToken node)
        {
            if(node==null)return "#";
            var segments=new List<string>();
            for(var item=node;item.Parent!=null;item=item.Parent)
            {
                if(item.Parent is JProperty p)segments.Add(p.Name.Replace("~","~0").Replace("/","~1"));
                else if(item.Parent is JArray a)segments.Add(a.IndexOf(item).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            segments.Reverse();
            JToken root=node;while(root.Parent!=null)root=root.Parent;
            string document=ReferenceEquals(root,Root)?"":_resources.FirstOrDefault(x=>ReferenceEquals(x.Value,root)&&!x.Key.Contains("#")).Key??"";
            return document+"#"+(segments.Count==0?"":"/"+string.Join("/",segments));
        }
        internal void VisitReferencedSchemas(Action<JToken,string> validate)
        {
            var visited=new HashSet<JToken>();var validated=new HashSet<JToken>();
            string Dialect(JToken node)
            {
                for(var current=node;current!=null;current=current.Parent)
                    if(current is JObject o&&o["$schema"]?.Type==JTokenType.String)return (string)o["$schema"];
                return SchemaDialect.Default;
            }
            var pending=new Stack<KeyValuePair<JToken,bool>>();pending.Push(new KeyValuePair<JToken,bool>(Root,true));
            while(pending.Count>0)
            {
                var entry=pending.Pop();var node=entry.Key;bool resource=entry.Value;
                if(resource&&validated.Add(node))validate(node,Dialect(node));
                if(!visited.Add(node)||!(node is JObject o))continue;
                foreach(var name in new[]{"$ref","$dynamicRef"})if(o[name]?.Type==JTokenType.String)
                {
                    var uri=new Uri(new Uri(_bases.TryGetValue(o,out var baseUri)?baseUri:RootUri),(string)o[name]);
                    string document=uri.GetLeftPart(UriPartial.Path)+uri.Query;
                    // These meta-schemas are already bundled and compiled by Corvus. They do not
                    // need a second user-supplied copy in our generation graph.
                    if(!_resources.ContainsKey(document)&&IsBundledMetaSchema(document))continue;
                    pending.Push(new KeyValuePair<JToken,bool>(Resolve(o,(string)o[name],System.Array.Empty<JObject>(),false),true));
                }
                string dialect=_nodeDialects.TryGetValue(o,out var knownDialect)?knownDialect:Dialect(o);
                foreach(var child in FormatAssertions.Children(o,key=>Dialects.Enabled(dialect,key)))
                {
                    // Definitions and contentSchema do not evaluate instances by themselves.
                    // Follow them when explicitly referenced, not merely because they were supplied.
                    if(ReferenceEquals(child,o["contentSchema"]))continue;
                    if(child.Parent is JProperty entryProperty&&(ReferenceEquals(entryProperty.Parent,o["$defs"])||ReferenceEquals(entryProperty.Parent,o["definitions"])))continue;
                    pending.Push(new KeyValuePair<JToken,bool>(child,child is JObject embedded&&embedded["$schema"]!=null));
                }
            }
        }
        private static bool IsBundledMetaSchema(string uri)
        {
            foreach(string version in new[]{"2020-12","2019-09"})
            {
                string prefix="https://json-schema.org/draft/"+version+"/";
                if(uri==prefix+"schema")return true;
                foreach(string vocabulary in new[]{"core","applicator","validation","meta-data","format","format-annotation","format-assertion","content","unevaluated"})
                    if(uri==prefix+"meta/"+vocabulary)return true;
            }
            return uri=="http://json-schema.org/draft-04/schema"||uri=="http://json-schema.org/draft-06/schema"||uri=="http://json-schema.org/draft-07/schema";
        }
        internal JToken Resolve(JObject source,string reference,IReadOnlyList<JObject> dynamicScope,bool dynamic)
        {
            var target=new Uri(new Uri(_bases.TryGetValue(source,out var scope)?scope:RootUri),reference);string fragment=target.Fragment,document=target.GetLeftPart(UriPartial.Path)+target.Query;
            if(_resources.TryGetValue(target.AbsoluteUri,out var exact))
            {
                // Dynamic scope is consulted only when the resolved target defines a dynamic anchor.
                if(dynamic&&exact is JObject anchored&&fragment.Length>1&&(string)anchored["$dynamicAnchor"]==Uri.UnescapeDataString(fragment.Substring(1)))
                    foreach(var entry in dynamicScope)if((string)entry["$dynamicAnchor"]==(string)anchored["$dynamicAnchor"])return entry;
                return exact;
            }
            if(!_resources.TryGetValue(document,out var root))throw new SchemaGenerationException(SchemaFailure.MissingReference,"引用文档未预加载："+document,reference);
            if(fragment.Length==0||fragment=="#")return root;
            if(!fragment.StartsWith("#/",StringComparison.Ordinal))throw new SchemaGenerationException(SchemaFailure.MissingReference,"找不到锚点："+reference,reference);
            JToken current=root;foreach(string raw in Uri.UnescapeDataString(fragment.Substring(2)).Split('/')){string part=raw.Replace("~1","/").Replace("~0","~");if(current is JObject obj)current=obj[part];else if(current is JArray array&&int.TryParse(part,out int i)&&i>=0&&i<array.Count)current=array[i];else current=null;if(current==null)throw new SchemaGenerationException(SchemaFailure.MissingReference,"找不到 JSON Pointer："+reference,reference);}return current;
        }
    }
    internal sealed class CandidateRejected:Exception {internal string Location {get;}internal string InstanceLocation {get;}internal CandidateRejected(string location=null,string instanceLocation=null){Location=location;InstanceLocation=instanceLocation;}}
}
