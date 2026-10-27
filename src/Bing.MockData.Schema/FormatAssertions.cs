using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Text.Json.RuntimeEvaluator;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Schema
{
    // Format predicates used by shared private constraints. Corvus retains applicator/dynamic scope semantics.
    internal static class FormatAssertions
    {
        private static readonly string[] Single={"additionalProperties","unevaluatedProperties","propertyNames","items","unevaluatedItems","contains","not","if","then","else","contentSchema"};
        private static readonly string[] Maps={"properties","patternProperties","$defs","definitions","dependentSchemas"};
        private static readonly string[] Arrays={"allOf","anyOf","oneOf","prefixItems"};
        internal static IEnumerable<JToken> Children(JObject o,Func<string,bool> enabled=null)
        {
            foreach(string key in Single)if((enabled==null||enabled(key))&&o[key]!=null)yield return o[key];
            foreach(string key in Maps)if((enabled==null||enabled(key))&&o[key] is JObject map)foreach(var p in map.Properties().ToArray())yield return p.Value;
            foreach(string key in Arrays)if((enabled==null||enabled(key))&&o[key] is JArray array)foreach(var child in array.ToArray())yield return child;
        }
        internal static readonly HashSet<string> Corrected=new HashSet<string>(new[]{"date-time","time","duration","regex","email","idn-email","idn-hostname","ipv6","uri","uri-reference","iri","iri-reference","uri-template"},StringComparer.Ordinal);
        private static bool Match(string value,string pattern,TimeSpan timeout)=>Regex.IsMatch(value,"\\A(?:"+pattern+")\\z",RegexOptions.CultureInvariant,timeout);
        internal static bool Valid(string format,string value,TimeSpan timeout)
        {
            switch(format)
            {
                case "date-time":
                    if(!Match(value,"[0-9]{4}-[0-9]{2}-[0-9]{2}[Tt].+",timeout))return false;
                    string date=value.Substring(0,10);if(!DateTime.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))return false;return Time(value.Substring(11),timeout);
                case "time":return Time(value,timeout);
                case "duration":
                    if(!Match(value,"P(?:[0-9]+W|(?=[0-9T])(?:[0-9]+Y)?(?:[0-9]+M)?(?:[0-9]+D)?(?:T(?=[0-9])(?:[0-9]+H)?(?:[0-9]+M)?(?:[0-9]+S)?)?)",timeout)||value=="P")return false;
                    string[] parts=value.Split('T');if(parts[0].Contains("Y")&&parts[0].Contains("D")&&!parts[0].Contains("M"))return false;if(parts.Length>1&&parts[1].Contains("H")&&parts[1].Contains("S")&&!parts[1].Contains("M"))return false;return true;
                case "regex":
                    // Translation already handles empty classes. Replacing text afterwards corrupts
                    // escaped brackets such as [\[]. The .NET fallback also needs a dialect guard.
                    try{if(!RegexDialectAllowed(value))return false;new Regex(JsonSchemaEvaluator.ToDotNetPattern(value),RegexOptions.CultureInvariant,timeout);return true;}catch(ArgumentException){return false;}catch(FormatException){return false;}
                case "email":return Email(value,false,timeout);case "idn-email":return Email(value,true,timeout);
                case "idn-hostname":return IdnHost(value);
                case "ipv6":return Ipv6(value);
                case "uri":case "uri-reference":case "iri":case "iri-reference":return UriValue(value,format.StartsWith("iri",StringComparison.Ordinal),format.EndsWith("reference",StringComparison.Ordinal),timeout);
                case "uri-template":return Template(value,timeout);
                default:return true;
            }
        }
        private static bool RegexDialectAllowed(string pattern)
        {
            bool inClass=false;
            for(int i=0;i<pattern.Length;i++)
            {
                char c=pattern[i];
                if(c=='\\'&&i+1<pattern.Length)
                {
                    char escaped=pattern[++i];
                    if(escaped=='a'||escaped=='e')return false;
                    if(escaped=='k'&&i+1<pattern.Length&&pattern[i+1]=='\'')return false;
                    continue;
                }
                if(inClass){if(c==']')inClass=false;continue;}
                if(c=='['){inClass=true;continue;}
                if(c!='('||i+1>=pattern.Length||pattern[i+1]!='?')continue;
                int start=i+2;if(start>=pattern.Length)return false;
                char kind=pattern[start];
                if(kind==':'||kind=='='||kind=='!')continue;
                if(kind=='<')
                {
                    if(start+1<pattern.Length&&(pattern[start+1]=='='||pattern[start+1]=='!'))continue;
                    int end=pattern.IndexOf('>',start+1);if(end<0)return false;
                    string name=pattern.Substring(start+1,end-start-1);
                    if(name.Length==0||name.Contains("-")||char.IsDigit(name[0]))return false;
                    continue;
                }
                // ES2025 scoped modifiers permit i/m/s only, without repeated flags.
                int cursor=start;bool minus=false;var flags=new HashSet<char>();
                for(;cursor<pattern.Length&&pattern[cursor]!=':';cursor++)
                {
                    char flag=pattern[cursor];
                    if(flag=='-'&&!minus){minus=true;continue;}
                    if((flag!='i'&&flag!='m'&&flag!='s')||!flags.Add(flag))return false;
                }
                if(cursor==pattern.Length||flags.Count==0||pattern[cursor-1]=='-')return false;
            }
            return true;
        }
        private static bool Time(string value,TimeSpan timeout)
        {
            var m=Regex.Match(value,@"\A(?<h>[0-9]{2}):(?<m>[0-9]{2}):(?<s>[0-9]{2})(?:\.[0-9]+)?(?:(?<z>[Zz])|(?<sign>[+-])(?<oh>[0-9]{2}):(?<om>[0-9]{2}))\z",RegexOptions.CultureInvariant,timeout);if(!m.Success)return false;
            int h=int.Parse(m.Groups["h"].Value),minute=int.Parse(m.Groups["m"].Value),second=int.Parse(m.Groups["s"].Value);if(h>23||minute>59||second>60)return false;
            int offset=0;if(!m.Groups["z"].Success){int oh=int.Parse(m.Groups["oh"].Value),om=int.Parse(m.Groups["om"].Value);if(oh>23||om>59)return false;offset=(oh*60+om)*(m.Groups["sign"].Value=="-"?-1:1);}
            return second<60||((h*60+minute-offset+1440)%1440)==1439;
        }
        private static bool Ipv6(string s)
        {
            if(s.IndexOfAny(new[]{'[',']','%'})>=0)return false;
            if(s.Contains(".")){string tail=s.Substring(s.LastIndexOf(':')+1);var parts=tail.Split('.');if(parts.Length!=4||parts.Any(p=>p.Length==0||(p.Length>1&&p[0]=='0')||!byte.TryParse(p,NumberStyles.None,CultureInfo.InvariantCulture,out _)))return false;}
            return IPAddress.TryParse(s,out var ip)&&ip.AddressFamily==AddressFamily.InterNetworkV6;
        }
        private static bool Host(string s)
        {if(s.Length>253)return false;if(s.EndsWith(".",StringComparison.Ordinal))s=s.Substring(0,s.Length-1);return s.Length>0&&s.Split('.').All(p=>p.Length>0&&p.Length<=63&&p[0]!='-'&&p[p.Length-1]!='-'&&p.All(c=>(c>='a'&&c<='z')||(c>='A'&&c<='Z')||(c>='0'&&c<='9')||c=='-'));}
        private static bool IdnHost(string s)
        {
            try
            {
                // UTS #46 mapping before IDNA validation, preserving the contextual rules of Corvus.
                s=s.Replace("\u200B","").Replace("\u00AD","").Normalize(NormalizationForm.FormKC).ToLowerInvariant();
                using(var e=JsonSchemaEvaluator.Compile("{\"format\":\"idn-hostname\"}",new JsonSchemaEvaluatorOptions{AssertFormat=true,CodeGeneration=JsonSchemaCodeGeneration.Disabled}))return e.Evaluate(Newtonsoft.Json.JsonConvert.SerializeObject(s));
            }catch(ArgumentException){return false;}
        }
        private static bool Email(string s,bool international,TimeSpan timeout)
        {
            if(!international&&s.Any(c=>c>127))return false;int at=s.LastIndexOf('@');if(at<=0||at==s.Length-1)return false;string local=s.Substring(0,at),host=s.Substring(at+1);
            if(local.Length>64)return false;
            if(local[0]=='"')
            {if(local.Length<2||local[local.Length-1]!='"')return false;for(int i=1;i<local.Length-1;i++){char c=local[i];if(c=='\\'){if(++i>=local.Length-1)return false;c=local[i];if(c<32||c>126)return false;}else if(c=='"'||c<32||c==127)return false;}}
            else {if(local.StartsWith(".",StringComparison.Ordinal)||local.EndsWith(".",StringComparison.Ordinal)||local.Contains(".."))return false;foreach(char c in local)if(!(c>127&&international)&&!char.IsLetterOrDigit(c)&&!"!#$%&'*+-/=?^_`{|}~.".Contains(c))return false;}
            if(host.StartsWith("[",StringComparison.Ordinal)&&host.EndsWith("]",StringComparison.Ordinal)){string ip=host.Substring(1,host.Length-2);if(ip.StartsWith("IPv6:",StringComparison.OrdinalIgnoreCase))return Ipv6(ip.Substring(5));return ip.Split('.').Length==4&&IPAddress.TryParse(ip,out var address)&&address.AddressFamily==AddressFamily.InterNetwork;}
            if(host.EndsWith(".",StringComparison.Ordinal))return false;
            if(!international)return Host(host);try{return IdnHost(host)&&Host(new IdnMapping().GetAscii(host));}catch(ArgumentException){return false;}
        }
        private static bool UriValue(string s,bool iri,bool relative,TimeSpan timeout)
        {
            if(s.Any(c=>char.IsWhiteSpace(c)||char.IsControl(c)||(!iri&&c>127))||Regex.IsMatch(s,@"%(?![0-9a-fA-F]{2})",RegexOptions.CultureInvariant,timeout))return false;
            if(s.IndexOfAny(new[]{'\\','<','>','"','{','}','|','^','`'})>=0)return false;
            var m=Regex.Match(s,@"\A(?:(?<scheme>[A-Za-z][A-Za-z0-9+.-]*):)?(?://(?<authority>[^/?#]*))?(?<path>[^?#]*)(?:\?[^#]*)?(?:#[^#]*)?\z",RegexOptions.CultureInvariant,timeout);if(!m.Success||(!relative&&!m.Groups["scheme"].Success))return false;
            string path=m.Groups["path"].Value;if(path.IndexOfAny(new[]{'[',']'})>=0)return false;
            if(!m.Groups["scheme"].Success&&!m.Groups["authority"].Success&&path.Split('/')[0].Contains(":"))return false;
            if(m.Groups["authority"].Success)
            {
                string authority=m.Groups["authority"].Value;if(authority.Count(c=>c=='@')>1)return false;int at=authority.LastIndexOf('@');if(at>=0&&authority.Substring(0,at).IndexOfAny(new[]{'[',']'})>=0)return false;string host=authority.Substring(at+1);
                if(host.StartsWith("[",StringComparison.Ordinal)){int close=host.IndexOf(']');if(close<0)return false;string literal=host.Substring(1,close-1);if(!Ipv6(literal)&&!Match(literal,@"[vV][0-9a-fA-F]+\.[A-Za-z0-9._~!$&'()*+,;=:-]+",timeout))return false;string rest=host.Substring(close+1);if(rest.Length>0&&!Match(rest,":([0-9]*)",timeout))return false;}
                else {if(host.IndexOfAny(new[]{'[',']'})>=0||host.Count(c=>c==':')>1)return false;int colon=host.LastIndexOf(':');if(colon>=0&&!host.Substring(colon+1).All(c=>c>='0'&&c<='9'))return false;}
            }return true;
        }
        private static bool Template(string s,TimeSpan timeout)
        {
            int pos=0;while(pos<s.Length){char c=s[pos++];if(c=='{'){int end=s.IndexOf('}',pos);if(end<0)return false;string expression=s.Substring(pos,end-pos);if(expression.Contains("..") || Regex.IsMatch(expression,@"\.(?:[,:*]|$)",RegexOptions.CultureInvariant,timeout))return false;if(!Match(expression,@"[+#./;?&]?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:[A-Za-z0-9_.]|%[0-9A-Fa-f]{2})*(?::[1-9][0-9]{0,3}|\*)?(?:,(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:[A-Za-z0-9_.]|%[0-9A-Fa-f]{2})*(?::[1-9][0-9]{0,3}|\*)?)*",timeout))return false;pos=end+1;}else if(c=='}'||c<33||c==127||c=='"'||c=='<'||c=='>'||c=='\\'||c=='^'||c=='`'||c=='|')return false;else if(c=='%'){if(pos+1>=s.Length||!Uri.IsHexDigit(s[pos])||!Uri.IsHexDigit(s[pos+1]))return false;pos+=2;}}return true;
        }
    }
}
