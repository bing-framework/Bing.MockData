using System;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Schema
{
    // Keep numeric lexemes instead of passing Schema numbers through double/decimal.
    internal static class ExactJson
    {
        internal static JToken Parse(string json,int maxDepth=256)
        {int pos=0;var value=Read(json,ref pos,0,maxDepth);Space(json,ref pos);if(pos!=json.Length)throw new FormatException("JSON 尾部存在额外内容。");return value;}
        private static void Space(string s,ref int p){while(p<s.Length&&(s[p]==' '||s[p]=='\t'||s[p]=='\r'||s[p]=='\n'))p++;}
        private static string String(string s,ref int p)
        {int start=p++;while(p<s.Length){char c=s[p++];if(c=='\\'){p++;continue;}if(c=='"')return JsonConvert.DeserializeObject<string>(s.Substring(start,p-start));}throw new FormatException("JSON 字符串未结束。");}
        private static JToken Read(string s,ref int p,int depth,int maxDepth)
        {
            if(depth>maxDepth)throw new FormatException("JSON 嵌套超限。");Space(s,ref p);if(p>=s.Length)throw new FormatException("JSON 已结束。");char c=s[p];
            if(c=='"')return new JValue(String(s,ref p));
            if(c=='{'||c=='[')
            {
                bool obj=c=='{';p++;JContainer result=obj?(JContainer)new JObject():new JArray();Space(s,ref p);char end=obj?'}':']';if(p<s.Length&&s[p]==end){p++;return result;}
                while(true){Space(s,ref p);string name=null;if(obj){if(p>=s.Length||s[p]!='"')throw new FormatException("JSON 属性名无效。");name=String(s,ref p);Space(s,ref p);if(p>=s.Length||s[p++]!=':')throw new FormatException("JSON 缺少冒号。");}var value=Read(s,ref p,depth+1,maxDepth);if(obj)((JObject)result).Add(name,value);else result.Add(value);Space(s,ref p);if(p>=s.Length)throw new FormatException("JSON 未结束。");c=s[p++];if(c==end)return result;if(c!=',')throw new FormatException("JSON 缺少逗号。");}
            }
            foreach(var literal in new[]{"true","false","null"})if(p+literal.Length<=s.Length&&string.CompareOrdinal(s,p,literal,0,literal.Length)==0){p+=literal.Length;return literal=="null"?JValue.CreateNull():new JValue(literal=="true");}
            int startNumber=p;if(c=='-')p++;if(p>=s.Length||s[p]<'0'||s[p]>'9')throw new FormatException("JSON 值无效。");if(s[p]=='0')p++;else while(p<s.Length&&char.IsDigit(s[p]))p++;
            if(p<s.Length&&s[p]=='.'){p++;int digits=p;while(p<s.Length&&char.IsDigit(s[p]))p++;if(digits==p)throw new FormatException("JSON 小数无效。");}
            if(p<s.Length&&(s[p]=='e'||s[p]=='E')){p++;if(p<s.Length&&(s[p]=='+'||s[p]=='-'))p++;int digits=p;while(p<s.Length&&char.IsDigit(s[p]))p++;if(digits==p)throw new FormatException("JSON 指数无效。");}
            return new JRaw(s.Substring(startNumber,p-startNumber));
        }
        internal static int Integer(JToken value,int fallback)
        {if(value==null)return fallback;var integer=Rational.Parse(value);if(integer.D!=1)throw new SchemaGenerationException(SchemaFailure.InvalidSchema,"约束需要整数。");return integer.N>int.MaxValue?int.MaxValue:integer.N<int.MinValue?int.MinValue:(int)integer.N;}
        internal static string Write(JToken token)=>token.ToString(Formatting.None);
    }
    internal readonly struct Rational:IComparable<Rational>
    {
        internal readonly BigInteger N,D;
        internal Rational(BigInteger n,BigInteger d){if(d<=0)throw new ArgumentOutOfRangeException(nameof(d));var gcd=BigInteger.GreatestCommonDivisor(BigInteger.Abs(n),d);N=n/gcd;D=d/gcd;}
        internal static Rational Parse(JToken value)
        {
            string s=value.ToString();int e=s.IndexOfAny(new[]{'e','E'}),exponent=0;if(e>=0&&!int.TryParse(s.Substring(e+1),NumberStyles.Integer,CultureInfo.InvariantCulture,out exponent))throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"数值指数超出生成预算。");if(e>=0)s=s.Substring(0,e);int dot=s.IndexOf('.');long wideScale=(long)(dot<0?0:s.Length-dot-1)-exponent;if(Math.Abs(wideScale)>100000)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"数值指数超出生成预算。");int scale=(int)wideScale;s=s.Replace(".","");
            if(Math.Abs((long)scale)>100000)throw new SchemaGenerationException(SchemaFailure.BudgetExhausted,"数值指数超过生成文本预算。");
            var n=BigInteger.Parse(s,CultureInfo.InvariantCulture);return scale<0?new Rational(n*BigInteger.Pow(10,-scale),1):new Rational(n,BigInteger.Pow(10,scale));
        }
        public int CompareTo(Rational other)=>(N*other.D).CompareTo(other.N*D);
        internal BigInteger Floor(){var q=BigInteger.DivRem(N,D,out var r);return r<0?q-1:q;}
        internal BigInteger Ceiling(){var q=BigInteger.DivRem(N,D,out var r);return r>0?q+1:q;}
        internal static Rational Divide(Rational a,Rational b)=>b.N>0?new Rational(a.N*b.D,a.D*b.N):throw new ArgumentOutOfRangeException(nameof(b));
        internal JRaw Json()
        {
            if(D==1)return new JRaw(N.ToString(CultureInfo.InvariantCulture));
            var d=D;int twos=0,fives=0;while(d%2==0){d/=2;twos++;}while(d%5==0){d/=5;fives++;}if(d!=1)throw new InvalidOperationException("非有限十进制。");int scale=Math.Max(twos,fives);var n=BigInteger.Abs(N)*BigInteger.Pow(2,scale-twos)*BigInteger.Pow(5,scale-fives);string digits=n.ToString(CultureInfo.InvariantCulture).PadLeft(scale+1,'0');return new JRaw((N<0?"-":"")+digits.Insert(digits.Length-scale,"."));
        }
    }
}
