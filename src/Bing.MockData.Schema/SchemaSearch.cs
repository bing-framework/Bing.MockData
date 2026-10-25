using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Bing.MockData.Schema
{
    // Candidate construction is intentionally separate from the standards validator.
    // No candidate is returned to a consumer until the entire root schema accepts it.
    internal sealed partial class SchemaSearch
    {
        private readonly SchemaGraph _graph;private readonly SchemaOptions _options;private readonly MockContext _context;
        private int _attempt;internal int Remaining {get;private set;}
        internal SchemaSearch(SchemaGraph graph,SchemaOptions options,MockContext context){_graph=graph;_options=options;_context=context;Remaining=options.MaxSearchSteps;}
        internal void Step(){_context.CancellationToken.ThrowIfCancellationRequested();if(Remaining--<=0){Remaining=0;throw new CandidateRejected();}}
        internal JToken Next(){Step();return Generate(new[]{_graph.Root},_context.ForField("candidate["+(_attempt++)+"]"),0,new List<JObject>(),"");}
        private List<JObject> Expand(IEnumerable<JToken> source,MockContext c,List<JObject> scope,int depth=0)
        {
            if(depth>_options.MaxDepth*4)throw new CandidateRejected();var result=new List<JObject>();
            foreach(var schema in source)
            {
                Step();if(schema==null)continue;if(schema.Type==JTokenType.Boolean){if(!(bool)schema)throw new CandidateRejected(_graph.Location(schema));continue;}if(!(schema is JObject o))throw new SchemaGenerationException(SchemaFailure.InvalidSchema,"子 Schema 必须为对象或布尔值。",schema.Path);
                result.Add(o);if(o["$dynamicAnchor"]!=null&&!scope.Contains(o))scope.Add(o);
                foreach(var name in new[]{"$ref","$dynamicRef"})if(o[name]?.Type==JTokenType.String)result.AddRange(Expand(new[]{_graph.Resolve(o,(string)o[name],scope,name=="$dynamicRef")},c,scope,depth+1));
                if(o["allOf"] is JArray all)result.AddRange(Expand(all,c,scope,depth+1));
                foreach(var name in new[]{"anyOf","oneOf"})if(o[name] is JArray alternatives){if(alternatives.Count==0)throw new CandidateRejected(_graph.Location(o));result.AddRange(Expand(new[]{alternatives[c.Numbers.Int(0,alternatives.Count-1)]},c,scope,depth+1));}
                if(o["if"]!=null){bool then=c.Numbers.Boolean();if(then)result.AddRange(Expand(new[]{o["if"],o["then"]},c,scope,depth+1));else if(o["else"]!=null)result.AddRange(Expand(new[]{o["else"]},c,scope,depth+1));}
            }
            return result;
        }
        private JToken Generate(IEnumerable<JToken> source,MockContext c,int depth,List<JObject> parentScope,string instancePath)
        {
            var inputs=source.Where(x=>x!=null).ToArray();
            try { return GenerateCore(inputs,c,depth,parentScope,instancePath); }
            catch(CandidateRejected e)
            {
                if(e.InstanceLocation!=null)throw;
                throw new CandidateRejected(e.Location??_graph.Location(inputs.FirstOrDefault()),instancePath);
            }
            catch(SchemaGenerationException e)
            {
                if(e.InstanceLocation.Length>0)throw;
                throw new SchemaGenerationException(e.Failure,e.Message,e.SchemaLocation=="#"?_graph.Location(inputs.FirstOrDefault()):e.SchemaLocation,instancePath,inner:e);
            }
        }
        private JToken GenerateCore(IEnumerable<JToken> source,MockContext c,int depth,List<JObject> parentScope,string instancePath)
        {
            Step();if(depth>Math.Min(_options.MaxDepth,_context.Limits.MaxDepth))return JValue.CreateNull();var scope=new List<JObject>(parentScope);var schemas=Expand(source,c,scope);
            if(TryFiniteValue(schemas,c,depth,out var finite))return finite;
            var types=new HashSet<string>(new[]{"null","boolean","integer","number","string","array","object"},StringComparer.Ordinal);bool explicitType=false;
            foreach(var s in schemas)if(s["type"]!=null){explicitType=true;var declared=s["type"] is JArray array?array.Select(x=>(string)x).ToList():new List<string>{(string)s["type"]};if(declared.Contains("number"))declared.Add("integer");types.IntersectWith(declared);}
            // number 已包含整数，避免随机选择更窄的 integer 子域而丢失唯一的小数解。
            if(types.Contains("number"))types.Remove("integer");
            if(types.Count==0)throw new CandidateRejected();string type;
            if(!explicitType&&c.Numbers.Boolean(.85))type=schemas.Any(s=>s["properties"]!=null||s["required"]!=null||s["minProperties"]!=null)?"object":schemas.Any(s=>s["items"]!=null||s["prefixItems"]!=null||s["contains"]!=null||s["minItems"]!=null)?"array":schemas.Any(s=>s["pattern"]!=null||s["minLength"]!=null||s["format"]!=null)?"string":schemas.Any(s=>s["minimum"]!=null||s["maximum"]!=null||s["multipleOf"]!=null)?"number":c.Collections.Pick(types.ToArray());
            else type=c.Collections.Pick(types.ToArray());
            switch(type)
            {
                case "null":if(schemas.Any(s=>KnownMatch(JValue.CreateNull(),s)==false))throw new CandidateRejected();return JValue.CreateNull();
                case "boolean":var booleans=new[]{new JValue(false),new JValue(true)}.Where(v=>schemas.All(s=>KnownMatch(v,s)!=false)).ToArray();if(booleans.Length==0)throw new CandidateRejected();return c.Collections.Pick(booleans);
                case "integer":case "number":return Number(schemas,c,type=="integer");
                case "string":return new JValue(Text(schemas,c));
                case "array":return Array(schemas,c,depth,scope,instancePath);
                case "object":return Object(schemas,c,depth,scope,instancePath);
                default:throw new SchemaGenerationException(SchemaFailure.InvalidSchema,"未知 type。",type);
            }
        }
        private static int Minimum(List<JObject> ss,string name,int initial=0)=>ss.Select(s=>ExactJson.Integer(s[name],initial)).DefaultIfEmpty(initial).Max();
        private static int Maximum(List<JObject> ss,string name,int initial)=>ss.Where(s=>s[name]!=null).Select(s=>ExactJson.Integer(s[name],initial)).DefaultIfEmpty(initial).Min();
        private JToken Number(List<JObject> ss,MockContext c,bool integer)
        {
            Rational? min=null,max=null;bool exclusiveMin=false,exclusiveMax=false;
            var negativeBounds=ss.Where(s=>s["not"]!=null).SelectMany(s=>NumericBoundaries(s["not"])).ToArray();
            foreach(var s in ss)
            {
                Step();foreach(var key in new[]{"minimum","exclusiveMinimum"})if(s[key]!=null){var x=Rational.Parse(s[key]);int comparison=min.HasValue?x.CompareTo(min.Value):1;if(comparison>0){min=x;exclusiveMin=key=="exclusiveMinimum";}else if(comparison==0)exclusiveMin|=key=="exclusiveMinimum";}
                foreach(var key in new[]{"maximum","exclusiveMaximum"})if(s[key]!=null){var x=Rational.Parse(s[key]);int comparison=max.HasValue?x.CompareTo(max.Value):-1;if(comparison<0){max=x;exclusiveMax=key=="exclusiveMaximum";}else if(comparison==0)exclusiveMax|=key=="exclusiveMaximum";}
            }
            Rational step=new Rational(1,integer?1:100);bool hasStep=integer;
            foreach(var s in ss)if(s["multipleOf"]!=null){var n=Rational.Parse(s["multipleOf"]);if(n.N<=0)throw new SchemaGenerationException(SchemaFailure.InvalidSchema,"multipleOf 必须大于零。");step=hasStep?new Rational(BigInteger.Abs(step.N*n.N)/BigInteger.GreatestCommonDivisor(step.N,n.N),BigInteger.GreatestCommonDivisor(step.D,n.D)):n;hasStep=true;}
            if(!hasStep){BigInteger denominator=100;foreach(var bound in new[]{min,max})if(bound.HasValue)denominator=BigInteger.Max(denominator,bound.Value.D*100);foreach(var bound in negativeBounds)denominator=BigInteger.Max(denominator,bound.D*100);step=new Rational(1,denominator);}
            BigInteger low=min.HasValue?Rational.Divide(min.Value,step).Ceiling():max.HasValue?Rational.Divide(max.Value,step).Floor()-10000:-10000;
            BigInteger high=max.HasValue?Rational.Divide(max.Value,step).Floor():low+10000;
            foreach(var boundary in negativeBounds)
            {
                Step();if(!min.HasValue)low=BigInteger.Min(low,Rational.Divide(boundary,step).Floor()-1);
                if(!max.HasValue)high=BigInteger.Max(high,Rational.Divide(boundary,step).Ceiling()+1);
            }
            if(min.HasValue&&exclusiveMin&&new Rational(low*step.N,step.D).CompareTo(min.Value)==0)low++;
            if(max.HasValue&&exclusiveMax&&new Rational(high*step.N,step.D).CompareTo(max.Value)==0)high--;
            if(low>high)throw new CandidateRejected();
            var ranges=new List<IntegerRange>{new IntegerRange(low,high)};
            foreach(var s in ss)if(s["not"]!=null)
            {
                var excluded=NumericRanges(s["not"],low,high,step,integer);
                if(excluded!=null)ranges=Subtract(ranges,excluded);
            }
            if(ranges.Count==0)throw new CandidateRejected();
            BigInteger total=ranges.Aggregate(BigInteger.Zero,(sum,r)=>sum+r.High-r.Low+1),offset=RandomBelow(total,c),selected=0;
            foreach(var range in ranges){Step();var size=range.High-range.Low+1;if(offset<size){selected=range.Low+offset;break;}offset-=size;}
            if(c.Numbers.Boolean(.2)){var range=ranges[c.Numbers.Int(0,ranges.Count-1)];selected=c.Numbers.Boolean()?range.Low:range.High;}
            return new Rational(selected*step.N,step.D).Json();
        }
        private BigInteger RandomBelow(BigInteger count,MockContext c)
        {
            if(count<=ulong.MaxValue)return (BigInteger)c.Numbers.ULong() % count;
            var bytes=(count-1).ToByteArray();
            byte mask=255;while((bytes[bytes.Length-1]&mask)==bytes[bytes.Length-1] && mask>0)mask>>=1;
            mask=(byte)((mask<<1)|1);
            for(;;)
            {
                Step();for(int i=0;i<bytes.Length;i++){Step();bytes[i]=(byte)c.Numbers.Int(0,255);}
                bytes[bytes.Length-1]&=mask;
                var value=new BigInteger(bytes);
                if(value>=0&&value<count)return value;
            }
        }
        private string Text(List<JObject> ss,MockContext c)
        {
            int min=Minimum(ss,"minLength"),max=Math.Min(Maximum(ss,"maxLength",_context.Limits.MaxTextLength),_context.Limits.MaxTextLength);
            if(min<0||max<min)throw new CandidateRejected();
            int sampleMax=Math.Min(max,Math.Max(min,16));
            var patterns=ss.Select(s=>(string)s["pattern"]).Where(s=>s!=null).ToArray();string value;
            if(patterns.Length>0)
            {
                try{value=c.Text.Regex(patterns[c.Numbers.Int(0,patterns.Length-1)]);}catch(ArgumentException){value=c.Text.Random(min,sampleMax);}catch(NotSupportedException){value=c.Text.Random(min,sampleMax);}
                foreach(var pattern in patterns){Step();if(!Regex.IsMatch(value,JsonSchemaEvaluator.ToDotNetPattern(pattern),RegexOptions.CultureInvariant,_options.RegexTimeout))throw new CandidateRejected();}
            }
            else
            {
                var format=ss.Select(s=>(string)s["format"]).FirstOrDefault(s=>s!=null);
                try
                {
                switch(format)
                {
                    case "date-time":value=TimeWithin(c,c.ReferenceTime.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture),min,max);break;case "date":value="2024-02-29";break;case "time":value=TimeWithin(c,"12:30:45",min,max);break;case "duration":value=DurationWithin(c,min,max);break;
                    case "email":case "idn-email":value=EmailWithin(c,min,max);break;case "hostname":case "idn-hostname":value=HostWithin(c,Math.Max(1,min),Math.Min(253,max));break;case "ipv4":value=IpWithin(c,min,max,false);break;case "ipv6":value=IpWithin(c,min,max,true);break;
                    case "uri":case "iri":value=FormatFill(c,"x:",min,max,"https://example.com/");break;
                    case "uri-reference":case "iri-reference":case "uri-template":value=FormatFill(c,"",min,max,"https://example.com/");break;case "uuid":value=c.Network.Guid().ToString();break;
                    case "json-pointer":value=min==0?"":FormatFill(c,"/",min,max,"/items/");break;case "relative-json-pointer":value=FormatFill(c,"0",min,max,"0/");break;case "regex":value=c.Text.Random(min,sampleMax,"abcdefghijklmnopqrstuvwxyz");break;default:value=c.Text.Random(min,sampleMax);break;
                }
                }
                // 注解模式不能因为格式与长度不兼容而缩小合法实例集合；断言仍由根验证执行。
                catch(CandidateRejected) when(Remaining>0){value=c.Text.Random(min,sampleMax);}
            }
            int points=0;for(int i=0;i<value.Length;i++){if(char.IsHighSurrogate(value[i])&&i+1<value.Length&&char.IsLowSurrogate(value[i+1]))i++;points++;}
            // A format is an annotation unless its vocabulary/options assert it. Rejection sampling
            // lets the complete validator decide, rather than silently asserting an annotation.
            if(points<min||points>max)
            {
                if(patterns.Length==0)return c.Text.Random(min,sampleMax);
                throw new CandidateRejected();
            }
            if(value.Length>_context.Limits.MaxTextLength)throw new CandidateRejected();
            return value;
        }
        private JArray Array(List<JObject> ss,MockContext c,int depth,List<JObject> scope,string instancePath)
        {
            int min=Minimum(ss,"minItems");
            foreach(var s in ss)if(s["contains"]!=null)min=Math.Max(min,ExactJson.Integer(s["minContains"],1));
            int preferred=ss.Select(s=>(s["prefixItems"] as JArray)?.Count??0).DefaultIfEmpty(0).Max();
            int hardMax=Math.Min(Maximum(ss,"maxItems",_context.Limits.MaxCollectionSize),_context.Limits.MaxCollectionSize);
            foreach(var s in ss)if(s["items"]?.Type==JTokenType.Boolean&&!(bool)s["items"])hardMax=Math.Min(hardMax,(s["prefixItems"] as JArray)?.Count??0);
            if(min<0||min>hardMax)throw new CandidateRejected();
            int count=c.Numbers.Int(min,Math.Min(hardMax,Math.Max(min,Math.Max(3,preferred))));
            var result=new JArray();
            var contains=new Dictionary<JObject,HashSet<int>>();
            foreach(var s in ss)if(s["contains"]!=null)
            {
                int required=ExactJson.Integer(s["minContains"],1),maximum=ExactJson.Integer(s["maxContains"],count);
                if(required>maximum)throw new CandidateRejected();
                var forced=new HashSet<int>();
                for(int i=0;i<count;i++)foreach(var source in ss)
                {
                    var prefix=source["prefixItems"] as JArray;
                    var item=(prefix!=null&&i<prefix.Count?prefix[i]:source["items"]) as JObject;
                    if(item?.Property("const")!=null&&KnownMatch(item["const"],s["contains"])==true)forced.Add(i);
                }
                if(forced.Count>maximum)throw new CandidateRejected();
                var slots=Enumerable.Range(0,count).Where(i=>!forced.Contains(i)).ToList();
                for(int i=slots.Count-1;i>0;i--){Step();int j=c.Numbers.Int(0,i);int old=slots[i];slots[i]=slots[j];slots[j]=old;}
                forced.UnionWith(slots.Take(Math.Max(0,required-forced.Count)));contains[s]=forced;
            }
            for(int i=0;i<count;i++)
            {
                Step();var constraints=new List<JToken>();
                foreach(var s in ss)
                {
                    var prefix=s["prefixItems"] as JArray;
                    if(prefix!=null&&i<prefix.Count)constraints.Add(prefix[i]);else if(s["items"]!=null)constraints.Add(s["items"]);else if(s["unevaluatedItems"] is JObject)constraints.Add(s["unevaluatedItems"]);
                    if(contains.TryGetValue(s,out var positions)&&positions.Contains(i))constraints.Add(s["contains"]);
                    else if(s["contains"]!=null&&s["maxContains"]!=null)constraints.Add(new JObject{["not"]=s["contains"].DeepClone()});
                }
                bool unique=ss.Any(s=>(bool?)s["uniqueItems"]==true);JToken value=null;
                for(int attempt=0;attempt<_context.Limits.MaxAttempts;attempt++)
                {
                    Step();value=Generate(constraints,c.ForField("["+i+"].attempt["+attempt+"]"),depth+1,scope,instancePath+"/"+i);
                    if(!unique||!result.Any(x=>SameValue(x,value)))break;
                    value=null;
                }
                if(value==null)throw new CandidateRejected(_graph.Location(ss.FirstOrDefault()),instancePath+"/"+i);
                result.Add(value);
            }
            return result;
        }
        private bool TryFiniteValue(List<JObject> schemas,MockContext c,int depth,out JToken value)
        {
            value=null;
            var constants=schemas.Where(s=>s.Property("const")!=null).Select(s=>s["const"]).ToArray();
            var enums=schemas.Select(s=>s["enum"]).OfType<JArray>().ToArray();
            if(constants.Length==0&&enums.Length==0)return false;
            IList<JToken> candidates=constants.Length>0?(IList<JToken>)constants.Take(1).ToArray():enums.OrderBy(e=>e.Count).First();
            if(candidates.Count==0)throw new CandidateRejected();
            int start=c.Numbers.Int(0,candidates.Count-1);
            for(int i=0;i<candidates.Count;i++)
            {
                Step();var candidate=candidates[(start+i)%candidates.Count];
                if(constants.Any(other=>!SameValue(candidate,other))||enums.Any(e=>!e.Any(other=>SameValue(candidate,other))))continue;
                if(schemas.Any(schema=>KnownMatch(candidate,schema)==false))continue;
                CheckLiteral(candidate,depth);value=candidate.DeepClone();return true;
            }
            throw new CandidateRejected();
        }

        // JSON Schema compares numbers mathematically and object members without ordering.
        private bool SameValue(JToken left,JToken right)
        {
            Step();
            bool NumberToken(JToken token)=>token.Type==JTokenType.Raw||token.Type==JTokenType.Integer||token.Type==JTokenType.Float;
            if(NumberToken(left)&&NumberToken(right))return Rational.Parse(left).CompareTo(Rational.Parse(right))==0;
            if(left is JObject a&&right is JObject b)
                return a.Count==b.Count&&a.Properties().All(p=>b.Property(p.Name)!=null&&SameValue(p.Value,b[p.Name]));
            if(left is JArray x&&right is JArray y)
                return x.Count==y.Count&&x.Select((item,index)=>SameValue(item,y[index])).All(equal=>equal);
            return JToken.DeepEquals(left,right);
        }

        private void CheckLiteral(JToken value,int depth)
        {
            Step();if(depth>Math.Min(_options.MaxDepth,_context.Limits.MaxDepth))throw new CandidateRejected();
            if(value is JObject obj)
            {
                if(obj.Count>_context.Limits.MaxCollectionSize)throw new CandidateRejected();
                foreach(var property in obj.Properties())
                {
                    if(property.Name.Length>_context.Limits.MaxTextLength)throw new CandidateRejected();
                    CheckLiteral(property.Value,depth+1);
                }
            }
            else if(value is JArray array)
            {
                if(array.Count>_context.Limits.MaxCollectionSize)throw new CandidateRejected();
                foreach(var item in array)CheckLiteral(item,depth+1);
            }
            else if(value.Type==JTokenType.String&&((string)value).Length>_context.Limits.MaxTextLength)throw new CandidateRejected();
        }

        private JObject Object(List<JObject> ss,MockContext c,int depth,List<JObject> scope,string instancePath)
        {
            var properties=new Dictionary<string,List<JToken>>(StringComparer.Ordinal);
            var selected=new HashSet<string>(StringComparer.Ordinal);
            var merged=new HashSet<JObject>();var fired=new HashSet<JProperty>();
            var active=new HashSet<JObject>(ss);
            var checkedNames=new HashSet<string>(StringComparer.Ordinal);int checkedRevision=-1;
            var known=new List<string>();int knownRevision=-1;
            int processed=0;
            void Select(string name)
            {
                Step();if(name.Length>_context.Limits.MaxTextLength)throw new CandidateRejected();
                selected.Add(name);
                if(selected.Count>_context.Limits.MaxCollectionSize)throw new CandidateRejected();
            }
            void CloseDependencies()
            {
                bool changed;
                do
                {
                    Step();int beforeCount=selected.Count,beforeSchemas=ss.Count;
                    while(processed<ss.Count)
                    {
                        Step();var schema=ss[processed++];if(!merged.Add(schema))continue;
                        if(schema["properties"] is JObject declared)foreach(var property in declared.Properties())
                        {
                            Step();if(!properties.TryGetValue(property.Name,out var constraints))properties.Add(property.Name,constraints=new List<JToken>());
                            constraints.Add(property.Value);
                        }
                        if(schema["required"] is JArray required)foreach(var name in required)Select((string)name);
                    }
                    // Dependencies introduced by other dependencies participate in the same closure.
                    for(int i=0;i<ss.Count;i++)
                    {
                        Step();var schema=ss[i];
                        if(schema["dependentRequired"] is JObject required)foreach(var dependency in required.Properties())
                        {
                            Step();if(selected.Contains(dependency.Name))foreach(var name in (JArray)dependency.Value)Select((string)name);
                        }
                        if(schema["dependentSchemas"] is JObject dependent)foreach(var dependency in dependent.Properties())
                        {
                            Step();if(!selected.Contains(dependency.Name)||!fired.Add(dependency))continue;
                            foreach(var expanded in Expand(new[]{dependency.Value},c,scope))if(active.Add(expanded))ss.Add(expanded);
                        }
                    }
                    changed=selected.Count!=beforeCount||ss.Count!=beforeSchemas;
                }while(changed);
            }
            bool Matches(JObject schema,string name)
            {
                if((schema["properties"] as JObject)?.Property(name)!=null)return true;
                if(schema["patternProperties"] is JObject patterns)foreach(var pattern in patterns.Properties())
                {
                    Step();if(Regex.IsMatch(name,JsonSchemaEvaluator.ToDotNetPattern(pattern.Name),RegexOptions.CultureInvariant,_options.RegexTimeout))return true;
                }
                return false;
            }
            bool NameAllowed(string name)
            {
                Step();if(name.Length>_context.Limits.MaxTextLength)return false;bool evaluated=false;
                foreach(var schema in ss)
                {
                    Step();bool matched=Matches(schema,name);var additional=schema["additionalProperties"];
                    if(!matched&&additional?.Type==JTokenType.Boolean&&!(bool)additional)return false;
                    evaluated|=matched||additional!=null;
                }
                return evaluated||!ss.Any(s=>s["unevaluatedProperties"]?.Type==JTokenType.Boolean&&!(bool)s["unevaluatedProperties"]);
            }
            void Bounds(out int min,out int max)
            {
                min=Minimum(ss,"minProperties");max=Math.Min(Maximum(ss,"maxProperties",_context.Limits.MaxCollectionSize),_context.Limits.MaxCollectionSize);
                if(min>max||selected.Count>max)throw new CandidateRejected();
                if(checkedRevision!=ss.Count){checkedNames.Clear();checkedRevision=ss.Count;}
                foreach(var name in selected)if(checkedNames.Add(name)&&!NameAllowed(name))throw new CandidateRejected();
            }
            CloseDependencies();Bounds(out int lower,out int upper);
            int preferred=Math.Max(Math.Max(lower,selected.Count),properties.Count);
            int target=c.Numbers.Int(Math.Max(lower,selected.Count),Math.Min(upper,preferred));
            while(selected.Count<target)
            {
                Step();
                if(knownRevision!=ss.Count)
                {
                    known=properties.Keys.Where(n=>!selected.Contains(n)&&NameAllowed(n)).OrderBy(n=>n,StringComparer.Ordinal).ToList();
                    knownRevision=ss.Count;
                }
                string name=null;
                while(known.Count>0&&name==null)
                {
                    Step();int index=c.Numbers.Int(0,known.Count-1);var candidate=known[index];
                    known[index]=known[known.Count-1];known.RemoveAt(known.Count-1);
                    if(!selected.Contains(candidate))name=candidate;
                }
                if(name==null)
                {
                    for(int attempt=0;attempt<_context.Limits.MaxAttempts;attempt++)
                    {
                        Step();name=null;
                        var field=c.ForField("property-name["+selected.Count+"].attempt["+attempt+"]");
                        var constraints=ss.Select(s=>s["propertyNames"]).Where(s=>s!=null).ToArray();
                        if(constraints.Length>0)
                        {
                            var generated=Generate(constraints.Concat(new[]{new JObject{["type"]="string"}}),field,depth+1,scope,instancePath);
                            if(generated.Type==JTokenType.String)name=(string)generated;
                        }
                        var patterns=ss.Select(s=>s["patternProperties"]).OfType<JObject>().SelectMany(s=>s.Properties()).ToArray();
                        if(name==null&&patterns.Length>0)
                        {
                            try{name=field.Text.Regex(patterns[field.Numbers.Int(0,patterns.Length-1)].Name);}
                            catch(ArgumentException){}catch(NotSupportedException){}
                        }
                        if(name==null)name="p"+(selected.Count+attempt).ToString(CultureInfo.InvariantCulture);
                        if(!selected.Contains(name)&&NameAllowed(name))break;name=null;
                    }
                }
                if(name==null)throw new CandidateRejected();
                Select(name);CloseDependencies();Bounds(out lower,out upper);
                target=Math.Max(lower,Math.Min(target,upper));
            }
            // An activated dependent schema may itself supply a finite object domain.
            if(TryFiniteValue(ss,c,depth,out var finite))
            {
                if(finite is JObject literal)return literal;
                throw new CandidateRejected();
            }
            var result=new JObject();
            foreach(var name in selected.OrderBy(n=>n,StringComparer.Ordinal))
            {
                Step();var constraints=properties.TryGetValue(name,out var declared)?new List<JToken>(declared):new List<JToken>();
                bool evaluated=false;
                foreach(var schema in ss)
                {
                    Step();bool matched=(schema["properties"] as JObject)?.Property(name)!=null;
                    if(schema["patternProperties"] is JObject patterns)foreach(var pattern in patterns.Properties())
                    {
                        Step();if(Regex.IsMatch(name,JsonSchemaEvaluator.ToDotNetPattern(pattern.Name),RegexOptions.CultureInvariant,_options.RegexTimeout)){constraints.Add(pattern.Value);matched=true;}
                    }
                    if(!matched&&schema["additionalProperties"]!=null){constraints.Add(schema["additionalProperties"]);evaluated=true;}
                    evaluated|=matched;
                }
                if(!evaluated)foreach(var schema in ss)if(schema["unevaluatedProperties"]!=null)constraints.Add(schema["unevaluatedProperties"]);
                string field="$property/"+name.Replace("~","~0").Replace("/","~1").Replace(".","~2");
                result.Add(name,Generate(constraints,c.ForField(field),depth+1,scope,instancePath+"/"+name.Replace("~","~0").Replace("/","~1")));
            }
            return result;
        }
    }
}

