using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Bing.MockData
{
    public enum PhoneNumberType { Mobile, FixedLine }
    public sealed class PhoneNumberFormat
    {
        public string Pattern {get;} public string Replacement {get;} public string LeadingDigits {get;} public string NationalPrefixRule {get;}
        public PhoneNumberFormat(string pattern,string replacement,string leadingDigits="",string nationalPrefixRule="")
        {Pattern=pattern;Replacement=replacement;LeadingDigits=leadingDigits;NationalPrefixRule=nationalPrefixRule;}
    }
    public sealed class PhonePattern
    {
        public string CountryCode { get; } public string CallingCode { get; } public PhoneNumberType Type { get; }
        public string NationalPattern { get; } public string NationalPrefix { get; }
        public IReadOnlyList<PhoneNumberFormat> Formats {get;}
        public PhonePattern(string countryCode,string callingCode,PhoneNumberType type,string nationalPattern,string nationalPrefix="",IEnumerable<PhoneNumberFormat> formats=null)
        { CountryCode=countryCode;CallingCode=callingCode;Type=type;NationalPattern=nationalPattern;NationalPrefix=nationalPrefix;Formats=Array.AsReadOnly((formats??Array.Empty<PhoneNumberFormat>()).ToArray()); }
    }
    public interface IPhoneMetadataProvider { PhonePattern GetPattern(string countryCode,PhoneNumberType type); }
    /// <summary>可选的非地理号码元数据能力；呼叫码仅用于解析 001 区域，不推测国家。</summary>
    public interface INonGeographicPhoneMetadataProvider
    {
        /// <summary>返回指定国际呼叫码及类型的非地理号码规则。</summary>
        PhonePattern GetNonGeographicPattern(string callingCode, PhoneNumberType type);
    }
    public sealed class MockPhone
    {
        public string CountryCode {get;} public string CallingCode {get;} public string NationalNumber {get;} public string Extension {get;}
        public string E164=>"+"+CallingCode+NationalNumber; public string NationalFormat {get;}
        internal MockPhone(PhonePattern p,string number,string extension)
        {
            CountryCode=p.CountryCode;CallingCode=p.CallingCode;NationalNumber=number;NationalFormat=number;Extension=extension;
            foreach(var format in p.Formats)
            {
                var timeout=TimeSpan.FromSeconds(1);
                if(!string.IsNullOrEmpty(format.LeadingDigits)&&!Regex.IsMatch(number,"\\A(?:"+format.LeadingDigits+")",RegexOptions.CultureInvariant,timeout))continue;
                var regex=new Regex("\\A(?:"+format.Pattern+")\\z",RegexOptions.CultureInvariant,timeout);if(!regex.IsMatch(number))continue;
                string replacement=format.Replacement;
                if(!string.IsNullOrEmpty(format.NationalPrefixRule))
                {
                    // $FG 表示替换表达式中的第一个捕获引用，可能为 $2（例如阿根廷移动号码）。
                    var firstGroup=Regex.Match(replacement,@"\$[0-9]+",RegexOptions.CultureInvariant,timeout);
                    if(firstGroup.Success)
                    {
                        string rule=format.NationalPrefixRule.Replace("$NP",p.NationalPrefix).Replace("$FG",firstGroup.Value);
                        replacement=replacement.Substring(0,firstGroup.Index)+rule+replacement.Substring(firstGroup.Index+firstGroup.Length);
                    }
                }
                NationalFormat=regex.Replace(number,replacement);break;
            }
        }
    }
    public readonly struct PhoneData
    {
        private readonly MockContext _c; internal PhoneData(MockContext c){_c=c;}
        /// <summary>按已挂载的 libphonenumber 元数据生成；只保证格式，不保证号码已分配。</summary>
        public MockPhone Number(string countryCode="CN",PhoneNumberType type=PhoneNumberType.Mobile,bool extension=false)
        {
            _c.Check();
            if (!Enum.IsDefined(typeof(PhoneNumberType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            var pattern=_c.Extension<IPhoneMetadataProvider>("phone-metadata").GetPattern(countryCode,type);
            return Generate(pattern, extension);
        }
        /// <summary>生成 001 非地理号码；呼叫码为无前导零的 1–3 位 ASCII 数字，不含加号。</summary>
        /// <exception cref="NotSupportedException">已挂载的元数据 Provider 未实现非地理号码能力。</exception>
        public MockPhone NonGeographicNumber(string callingCode, PhoneNumberType type=PhoneNumberType.Mobile, bool extension=false)
        {
            _c.Check();
            if (callingCode == null) throw new ArgumentNullException(nameof(callingCode));
            if (callingCode.Length < 1 || callingCode.Length > 3 || callingCode[0] == '0' || !callingCode.All(ch => ch >= '0' && ch <= '9'))
                throw new ArgumentException("呼叫码必须为无前导零的 1–3 位 ASCII 数字，不含加号。", nameof(callingCode));
            if (!Enum.IsDefined(typeof(PhoneNumberType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            var provider = _c.Extension<object>("phone-metadata") as INonGeographicPhoneMetadataProvider;
            if (provider == null) throw new NotSupportedException("电话元数据 Provider 未实现非地理号码能力。");
            var pattern = provider.GetNonGeographicPattern(callingCode, type);
            if (pattern.CountryCode != "001" || pattern.CallingCode != callingCode || pattern.Type != type)
                throw new InvalidOperationException("非地理号码规则的地区、呼叫码或类型与请求不符。");
            return Generate(pattern, extension);
        }
        private MockPhone Generate(PhonePattern pattern, bool extension)
        {
            for(int attempt=0;attempt<_c.Limits.MaxAttempts;attempt++)
            {
                // libphonenumber 的数字元数据使用 ASCII；通用 .NET 正则的 \d 还包含 Unicode 数字。
                _c.CancellationToken.ThrowIfCancellationRequested();string number=_c.Text.Regex(pattern.NationalPattern.Replace(@"\d","[0-9]"));
                if(!Regex.IsMatch(number,"\\A(?:"+pattern.NationalPattern+")\\z",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1)) || !number.All(c=>c>='0'&&c<='9') || pattern.CallingCode.Length+number.Length>15)continue;
                return new MockPhone(pattern,number,extension?_c.Text.Random(4,4,"0123456789"):null);
            }
            throw new InvalidOperationException(pattern.CountryCode+"/"+pattern.CallingCode+"/"+pattern.Type+" 在尝试预算内未生成满足 E.164 的号码。");
        }
    }
    /// <summary>客户端样本所描述的设备类型。</summary>
    public enum ClientDeviceType
    {
        /// <summary>桌面或笔记本设备。</summary>
        Desktop,
        /// <summary>手机设备。</summary>
        Mobile,
        /// <summary>平板设备。</summary>
        Tablet
    }
    /// <summary>描述一个成组的模拟浏览器、操作系统、设备类型及 User-Agent。</summary>
    public sealed class ClientProfile
    {
        /// <summary>获取浏览器名称。</summary>
        public string Browser {get;}
        /// <summary>获取操作系统名称。</summary>
        public string OperatingSystem {get;}
        /// <summary>获取 User-Agent 样本文本。</summary>
        public string UserAgent {get;}
        /// <summary>获取设备类型。</summary>
        public ClientDeviceType DeviceType {get;}
        /// <summary>创建一组模拟客户端标识；User-Agent 是格式示例，不表示真实设备信息。</summary>
        public ClientProfile(string browser,string operatingSystem,string userAgent,ClientDeviceType deviceType=ClientDeviceType.Desktop)
        {
            if(string.IsNullOrWhiteSpace(browser))throw new ArgumentException("浏览器名称不能为空。",nameof(browser));
            if(string.IsNullOrWhiteSpace(operatingSystem))throw new ArgumentException("操作系统名称不能为空。",nameof(operatingSystem));
            if(string.IsNullOrWhiteSpace(userAgent))throw new ArgumentException("User-Agent 不能为空。",nameof(userAgent));
            if(!Enum.IsDefined(typeof(ClientDeviceType),deviceType))throw new ArgumentOutOfRangeException(nameof(deviceType));
            Browser=browser;OperatingSystem=operatingSystem;UserAgent=userAgent;DeviceType=deviceType;
        }
    }
    public sealed class HttpExchange
    { public string Method {get;set;} public string Url {get;set;} public int StatusCode {get;set;} public ClientProfile Client {get;set;} public DateTimeOffset RequestedAt {get;set;} public DateTimeOffset RespondedAt {get;set;} public double DurationMilliseconds=>(RespondedAt-RequestedAt).TotalMilliseconds; }
    public readonly partial struct NetworkData
    {
        public string Domain(string suffix="example.com") { TextData.ValidateDomain(suffix);return DomainGuard.Text(_context,_context.Text.Random(8,8,"abcdefghijklmnopqrstuvwxyz")+"."+suffix); }
        public int Port(int min=1,int max=65535) { if(min<0||max>65535)throw new ArgumentOutOfRangeException(nameof(min));return _context.Numbers.Int(min,max); }
        public string HttpMethod()=>_context.Collections.Pick(new[]{"GET","POST","PUT","PATCH","DELETE","HEAD","OPTIONS"});
        public int HttpStatus()=>_context.Collections.Pick(new[]{200,201,202,204,301,302,304,400,401,403,404,409,422,429,500,502,503});
        public ClientProfile Client()=>ClientSamples.Pick(_context,null);
        public HttpExchange Exchange(string domain="example.com")
        {
            var end=_context.ForField("end").Dates.Past(TimeSpan.FromDays(30));var duration=_context.ForField("duration").Numbers.Int(1,10000);
            return new HttpExchange {Method=_context.ForField("method").Network.HttpMethod(),Url=_context.ForField("url").Network.Url(domain),StatusCode=_context.ForField("status").Network.HttpStatus(),Client=_context.ForField("client").Network.Client(),RequestedAt=end.AddMilliseconds(-duration),RespondedAt=end};
        }
    }
    public sealed class FileTypeDefinition
    {
        public string Extension {get;} public string MimeType {get;}
        public FileTypeDefinition(string extension,string mimeType)
        { if(string.IsNullOrWhiteSpace(extension)||!extension.All(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'))throw new ArgumentException("扩展名只能包含字母、数字、下划线和横线。");if(string.IsNullOrWhiteSpace(mimeType)||!mimeType.Contains("/"))throw new ArgumentException("MIME 无效。");Extension=extension;MimeType=mimeType; }
    }
    public interface IFileTypeProvider { IReadOnlyList<FileTypeDefinition> Find(string extension); FileTypeDefinition Sample(ulong selector); }
    public enum MockPathStyle { Windows, Posix }
    public sealed class FileMetadata
    { public string Name {get;set;} public string Extension {get;set;} public string MimeType {get;set;} public string Path {get;set;} public long SizeBytes {get;set;} public DateTimeOffset CreatedAt {get;set;} public DateTimeOffset ModifiedAt {get;set;} }
    public readonly struct FileData
    {
        private readonly MockContext _c; internal FileData(MockContext c){_c=c;}
        public FileMetadata Metadata(string extension=null,MockPathStyle style=MockPathStyle.Posix,long maxBytes=10485760)
        {
            if(maxBytes<0)throw new ArgumentOutOfRangeException(nameof(maxBytes));
            var provider=_c.Extension<IFileTypeProvider>("mime-types");var type=extension==null?provider.Sample(_c.ForField("type").Numbers.ULong()):_c.ForField("type").Collections.Pick(provider.Find(extension));
            string name=_c.ForField("name").Text.Random(12,12,"abcdefghijklmnopqrstuvwxyz")+"."+type.Extension;
            var modified=_c.ForField("modified").Dates.Past(TimeSpan.FromDays(30));
            return new FileMetadata {Name=DomainGuard.Text(_c,name),Extension=type.Extension,MimeType=type.MimeType,Path=DomainGuard.Text(_c,(style==MockPathStyle.Windows?"C:\\mock\\":"/mock/")+name),SizeBytes=_c.ForField("size").Numbers.Long(0,maxBytes),ModifiedAt=modified,CreatedAt=modified.AddDays(-_c.ForField("created").Numbers.Int(0,365))};
        }
    }
    public sealed partial class MockContext
    { public PhoneData Phone=>new PhoneData(this);public FileData Files=>new FileData(this); }
}
