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
        private readonly SchemaOptions _options;
        private readonly SchemaGraph _graph;
        private readonly string _json;
        private readonly Dictionary<string,string> _documents;
        private readonly bool _correctFormats;
        public string Identity {get;}
        private CompiledSchema(string json,IReadOnlyDictionary<string,string> documents,SchemaOptions options)
        {
            _options=options.Copy();long bytes=Encoding.UTF8.GetByteCount(json);if(documents.Count+1>_options.MaxDocuments)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"Schema 文档数超限。");
            _json=json;_documents=documents.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
            var frozen=new Dictionary<string,byte[]>(StringComparer.Ordinal);foreach(var p in documents){bytes=checked(bytes+Encoding.UTF8.GetByteCount(p.Value));if(bytes>_options.MaxDocumentBytes)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"Schema 输入字节超限。");frozen.Add(p.Key,Encoding.UTF8.GetBytes(p.Value));}
            if(bytes>_options.MaxDocumentBytes)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"Schema 输入字节超限。");
            using(var sha=SHA256.Create()){var content=Newtonsoft.Json.JsonConvert.SerializeObject(new {GeneratorContract="schema-search-v3",Schema=json,Documents=documents.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray(),Options=_options});Identity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-","").ToLowerInvariant();}
            string missingReference=null;
            try
            {
                bool hasFormat=false;FormatAssertions.Visit(ExactJson.Parse(json),node=>{if(node["format"]!=null)hasFormat=true;});
                foreach(var document in documents.Values)FormatAssertions.Visit(ExactJson.Parse(document),node=>{if(node["format"]!=null)hasFormat=true;});
                _correctFormats=hasFormat&&(_options.AssertFormat==true||(_options.AssertFormat==null&&documents.Values.Any(FormatAssertions.AssertsFormats)));
                using (var meta = JsonSchemaEvaluator.Compile("{\"$ref\":\"https://json-schema.org/draft/2020-12/schema\"}", new JsonSchemaEvaluatorOptions { CodeGeneration=JsonSchemaCodeGeneration.Disabled, AssertContent=false, CompileRegularExpressions=false, RegexMatchTimeout=_options.RegexTimeout }))
                    if(!meta.Evaluate(json)) throw new SchemaGenerationException(SchemaFailure.InvalidSchema,"输入不符合 Draft 2020-12 元 Schema。");
                _graph=new SchemaGraph(json,documents);
                bool Resolve(string uri,out ReadOnlyMemory<byte> result)
                {if(frozen.TryGetValue(uri,out var source)){result=source;return true;}missingReference=uri;result=default;return false;}
                _evaluator=JsonSchemaEvaluator.Compile(_json,new JsonSchemaEvaluatorOptions{BaseUri=SchemaGraph.RootUri,DefaultDialect=JsonSchemaDialect.Draft202012,DocumentResolver=Resolve,CodeGeneration=JsonSchemaCodeGeneration.Disabled,AssertFormat=_options.AssertFormat,AssertContent=false,MaxDepth=Math.Max(32,_options.MaxDepth*4),CompileRegularExpressions=false,RegexMatchTimeout=_options.RegexTimeout});
            }
            catch(SchemaGenerationException){throw;}
            catch(Exception e){throw new SchemaGenerationException(missingReference==null?SchemaFailure.InvalidSchema:SchemaFailure.MissingReference,"Schema 编译失败："+e.Message,inner:e);}
        }
        public static CompiledSchema Compile(string json,IReadOnlyDictionary<string,string> documents=null,SchemaOptions options=null)
        {if(json==null)throw new ArgumentNullException(nameof(json));return new CompiledSchema(json,documents??new Dictionary<string,string>(),options??new SchemaOptions());}
        public bool Validate(string json) => Validate(json,null);
        private bool Validate(string json,Corvus.Text.Json.IJsonSchemaResultsCollector collector)
        {
            if(json==null)throw new ArgumentNullException(nameof(json));lock(_gate)
            {
                if(_evaluator==null)throw new ObjectDisposedException(nameof(CompiledSchema));
                if(!_correctFormats)return _evaluator.Evaluate(json,collector);
                var values=FormatAssertions.Strings(ExactJson.Parse(json));
                bool Resolve(string uri,out ReadOnlyMemory<byte> document){if(_documents.TryGetValue(uri,out var text)){document=Encoding.UTF8.GetBytes(FormatAssertions.Rewrite(text,values,_options.RegexTimeout,_options.AssertFormat,_documents));return true;}document=default;return false;}
                using(var evaluator=JsonSchemaEvaluator.Compile(FormatAssertions.Rewrite(_json,values,_options.RegexTimeout,_options.AssertFormat,_documents),new JsonSchemaEvaluatorOptions{BaseUri=SchemaGraph.RootUri,DefaultDialect=JsonSchemaDialect.Draft202012,DocumentResolver=Resolve,CodeGeneration=JsonSchemaCodeGeneration.Disabled,AssertFormat=_options.AssertFormat,AssertContent=false,MaxDepth=Math.Max(32,_options.MaxDepth*4),CompileRegularExpressions=false,RegexMatchTimeout=_options.RegexTimeout}))return evaluator.Evaluate(json,collector);
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
                        if(Validate(json,results))return new JsonSample(json,Identity,replay);
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
        public void Dispose(){lock(_gate){_evaluator?.Dispose();_evaluator=null;}}
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
        internal SchemaGraph(string json,IReadOnlyDictionary<string,string> documents)
        {Root=ExactJson.Parse(json);if(!(Root is JObject)&&Root.Type!=JTokenType.Boolean)throw new FormatException("Schema 必须为对象或布尔值。");Add(Root,RootUri);foreach(var p in documents)Add(ExactJson.Parse(p.Value),p.Key);}
        private void Add(JToken root,string uri){_resources[uri]=root;Walk(root,uri);}
        private void Walk(JToken node,string uri)
        {
            _bases[node]=uri;
            if(!(node is JObject o))return;
            if(o["$id"]?.Type==JTokenType.String){uri=new Uri(new Uri(uri),(string)o["$id"]).AbsoluteUri;_resources[uri]=node;_bases[node]=uri;}
            foreach(var key in new[]{"$anchor","$dynamicAnchor"})if(o[key]?.Type==JTokenType.String)_resources[uri+"#"+(string)o[key]]=node;
            foreach(var child in FormatAssertions.Children(o))Walk(child,uri);
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
