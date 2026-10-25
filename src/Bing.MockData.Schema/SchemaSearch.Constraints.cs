using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Schema
{
    internal sealed partial class SchemaSearch
    {
        private sealed class IntegerRange
        {
            internal readonly BigInteger Low,High;
            internal IntegerRange(BigInteger low,BigInteger high){Low=low;High=high;}
        }

        private List<IntegerRange> Subtract(List<IntegerRange> source,List<IntegerRange> excluded)
        {
            foreach(var cut in excluded)
            {
                var next=new List<IntegerRange>();
                foreach(var range in source)
                {
                    Step();if(cut.High<range.Low||cut.Low>range.High){next.Add(range);continue;}
                    if(cut.Low>range.Low)next.Add(new IntegerRange(range.Low,cut.Low-1));
                    if(cut.High<range.High)next.Add(new IntegerRange(cut.High+1,range.High));
                }
                source=next;
            }
            return source;
        }

        // 只对完全理解的数值子式计算精确补集；遇到引用或其他关键字交给完整根验证。
        private List<IntegerRange> NumericRanges(JToken schema,BigInteger low,BigInteger high,Rational step,bool integer)
        {
            Step();var whole=new List<IntegerRange>{new IntegerRange(low,high)};
            if(schema.Type==JTokenType.Boolean)return (bool)schema?whole:new List<IntegerRange>();
            if(!(schema is JObject obj))return null;
            var allowed=new HashSet<string>(new[]{"type","const","enum","minimum","maximum","exclusiveMinimum","exclusiveMaximum","anyOf","allOf","not","title","description","$comment","default","examples","deprecated","readOnly","writeOnly"},StringComparer.Ordinal);
            if(obj.Properties().Any(p=>!allowed.Contains(p.Name)))return null;
            var result=whole;
            void Intersect(List<IntegerRange> ranges){result=Subtract(result,Subtract(whole,ranges));}
            if(obj["type"]!=null)
            {
                var types=obj["type"] is JArray a?a.Select(x=>(string)x).ToArray():new[]{(string)obj["type"]};
                if(!types.Contains("number"))
                {
                    if(!types.Contains("integer"))return new List<IntegerRange>();
                    if(!integer)return null;
                }
            }
            foreach(var key in new[]{"minimum","exclusiveMinimum","maximum","exclusiveMaximum"})if(obj[key]!=null)
            {
                var boundary=Rational.Parse(obj[key]);var quotient=Rational.Divide(boundary,step);
                bool lower=key=="minimum"||key=="exclusiveMinimum";
                var index=lower?quotient.Ceiling():quotient.Floor();
                if(key.StartsWith("exclusive",StringComparison.Ordinal)&&new Rational(index*step.N,step.D).CompareTo(boundary)==0)index+=lower?1:-1;
                var first=lower?BigInteger.Max(low,index):low;var last=lower?high:BigInteger.Min(high,index);
                Intersect(first<=last?new List<IntegerRange>{new IntegerRange(first,last)}:new List<IntegerRange>());
            }
            foreach(var key in new[]{"const","enum"})if(obj.Property(key)!=null)
            {
                var values=key=="const"?new[]{obj[key]}:((JArray)obj[key]).ToArray();var points=new List<IntegerRange>();
                foreach(var value in values)
                {
                    Step();if(!IsNumber(value))continue;var n=Rational.Parse(value);var index=Rational.Divide(n,step).Floor();
                    if(index>=low&&index<=high&&new Rational(index*step.N,step.D).CompareTo(n)==0)points.Add(new IntegerRange(index,index));
                }
                Intersect(points);
            }
            if(obj["anyOf"] is JArray any)
            {
                var union=new List<IntegerRange>();foreach(var item in any){var ranges=NumericRanges(item,low,high,step,integer);if(ranges==null)return null;union.AddRange(ranges);}Intersect(union);
            }
            if(obj["allOf"] is JArray all)foreach(var item in all){var ranges=NumericRanges(item,low,high,step,integer);if(ranges==null)return null;Intersect(ranges);}
            if(obj["not"]!=null){var ranges=NumericRanges(obj["not"],low,high,step,integer);if(ranges==null)return null;result=Subtract(result,ranges);}
            return result;
        }

        private static bool IsNumber(JToken value)=>value.Type==JTokenType.Integer||value.Type==JTokenType.Float||value.Type==JTokenType.Raw;

        private IEnumerable<Rational> NumericBoundaries(JToken schema)
        {
            Step();if(!(schema is JObject obj))yield break;
            foreach(var key in new[]{"minimum","maximum","exclusiveMinimum","exclusiveMaximum","const"})
                if(obj[key]!=null&&IsNumber(obj[key]))yield return Rational.Parse(obj[key]);
            if(obj["enum"] is JArray values)foreach(var value in values){Step();if(IsNumber(value))yield return Rational.Parse(value);}
            foreach(var key in new[]{"anyOf","allOf"})if(obj[key] is JArray branches)
                foreach(var branch in branches)foreach(var value in NumericBoundaries(branch))yield return value;
            if(obj["not"]!=null)foreach(var value in NumericBoundaries(obj["not"]))yield return value;
        }

        // 三态判断：未知不代表满足，用于提前排除有限域内已知不合格的候选。
        private bool? KnownMatch(JToken value,JToken schema)
        {
            Step();if(schema.Type==JTokenType.Boolean)return (bool)schema;if(!(schema is JObject obj))return null;
            bool unknown=false;
            foreach(var property in obj.Properties())
            {
                Step();bool? matches=null;
                switch(property.Name)
                {
                    case "const":matches=SameValue(value,property.Value);break;
                    case "enum":matches=((JArray)property.Value).Any(x=>SameValue(value,x));break;
                    case "not":var inner=KnownMatch(value,property.Value);matches=inner.HasValue?!inner.Value:(bool?)null;break;
                    case "allOf":case "anyOf":
                        var children=((JArray)property.Value).Select(x=>KnownMatch(value,x)).ToArray();
                        matches=property.Name=="allOf"?(children.Any(x=>x==false)?false:children.All(x=>x==true)?true:(bool?)null):(children.Any(x=>x==true)?true:children.All(x=>x==false)?false:(bool?)null);break;
                    case "type":
                        var types=property.Value is JArray typesArray?typesArray.Select(x=>(string)x):new[]{(string)property.Value};
                        matches=types.Any(type=>type=="number"?IsNumber(value):type=="integer"?IsNumber(value)&&Rational.Parse(value).D==1:type=="null"?value.Type==JTokenType.Null:type=="boolean"?value.Type==JTokenType.Boolean:type=="string"?value.Type==JTokenType.String:type=="array"?value is JArray:type=="object"&&value is JObject);break;
                    case "minimum":case "maximum":case "exclusiveMinimum":case "exclusiveMaximum":
                        if(!IsNumber(value)){matches=true;break;}
                        int comparison=Rational.Parse(value).CompareTo(Rational.Parse(property.Value));
                        matches=property.Name=="minimum"?comparison>=0:property.Name=="maximum"?comparison<=0:property.Name=="exclusiveMinimum"?comparison>0:comparison<0;break;
                    case "title":case "description":case "$comment":case "default":case "examples":case "deprecated":case "readOnly":case "writeOnly":matches=true;break;
                }
                if(matches==false)return false;if(matches==null)unknown=true;
            }
            return unknown?(bool?)null:true;
        }

        private string FormatFill(MockContext c,string shortest,int min,int max,string preferred)
        {
            string prefix=max>=preferred.Length?preferred:shortest;
            if(max<prefix.Length)throw new CandidateRejected();
            int lower=Math.Max(0,min-prefix.Length),upper=Math.Min(max-prefix.Length,Math.Max(lower,12));
            return prefix+c.Text.Random(lower,upper,"abcdefghijklmnopqrstuvwxyz");
        }
        private string HostWithin(MockContext c,int min,int max)
        {
            if(max<min||max<1)throw new CandidateRejected();int count=c.Numbers.Int(min,Math.Min(max,Math.Max(min,11)));
            var labels=new List<string>();
            while(count>63){Step();int take=Math.Min(62,count-2);labels.Add(c.Text.Random(take,take,"abcdefghijklmnopqrstuvwxyz"));count-=take+1;}
            labels.Add(c.Text.Random(count,count,"abcdefghijklmnopqrstuvwxyz"));return string.Join(".",labels);
        }
        private string EmailWithin(MockContext c,int min,int max)
        {
            max=Math.Min(254,max);if(Math.Max(3,min)>max)throw new CandidateRejected();
            int preferredMinimum=max>=13?13:max>=8?8:3;
            int length=c.Numbers.Int(Math.Max(preferredMinimum,min),Math.Min(max,Math.Max(min,24)));
            string host=length>=13&&length<=76?"example.com":length>=8&&length<=12?"x.test":
                length>76?HostWithin(c,length-70,length-70)+".test":HostWithin(c,1,1);
            int local=length-host.Length-1;if(local<1||local>64)throw new CandidateRejected();
            return c.Text.Random(local,local,"abcdefghijklmnopqrstuvwxyz")+"@"+host;
        }
        private string TimeWithin(MockContext c,string time,int min,int max)
        {
            int basic=time.Length+1;if(min<=basic&&max>=basic)return time+"Z";
            if(max<basic+2)throw new CandidateRejected();
            int digits=Math.Max(1,min-basic-1);if(basic+digits+1>max)throw new CandidateRejected();
            return time+"."+c.Text.Random(digits,digits,"0123456789")+"Z";
        }
        private string DurationWithin(MockContext c,int min,int max)
        {
            int digits=Math.Max(1,min-2);if(digits+2>max)throw new CandidateRejected();
            return "P"+"1"+c.Text.Random(digits-1,digits-1,"0123456789")+"D";
        }
        private string IpWithin(MockContext c,int min,int max,bool ipv6)
        {
            if(!ipv6)
            {
                int low=Math.Max(7,min),high=Math.Min(15,max);if(low>high)throw new CandidateRejected();
                int remaining=c.Numbers.Int(low,high)-3;var octets=new List<string>();
                for(int i=0;i<4;i++){int digits=c.Numbers.Int(Math.Max(1,remaining-(3-i)*3),Math.Min(3,remaining-(3-i)));remaining-=digits;octets.Add(c.Numbers.Int(digits==1?0:digits==2?10:100,digits==1?9:digits==2?99:255).ToString(CultureInfo.InvariantCulture));}
                return string.Join(".",octets);
            }
            var possibilities=new List<Tuple<int,int,int>>();
            // 压缩形式保留至少一个零组；全形式固定八组。
            for(int groups=0;groups<=8;groups++)
            {
                int separators=groups==8?7:groups==0?2:groups+1;
                int low=Math.Max(min,separators+groups),high=Math.Min(max,separators+groups*4);
                if(low<=high)possibilities.Add(Tuple.Create(groups,low,high));
            }
            if(possibilities.Count==0)throw new CandidateRejected();var selected=c.Collections.Pick(possibilities);int count=selected.Item1;
            int left=c.Numbers.Int(selected.Item2,selected.Item3)-(count==8?7:count==0?2:count+1);var parts=new List<string>();
            for(int i=0;i<count;i++){int digits=c.Numbers.Int(Math.Max(1,left-(count-i-1)*4),Math.Min(4,left-(count-i-1)));left-=digits;parts.Add(c.Text.Random(digits,digits,"0123456789abcdef"));}
            return (count==8?"":"::")+string.Join(":",parts);
        }
    }
}
