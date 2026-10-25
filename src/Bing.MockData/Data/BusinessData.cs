using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Bing.MockData
{
    /// <summary>本库原创的模拟文案类型，不代表真实公告或评价。</summary>
    public enum BusinessTextKind { Sentence, Title, Announcement, Review, ProductDescription }
    public readonly partial struct TextData
    {
        public string Business(BusinessTextKind kind = BusinessTextKind.Sentence, int maxLength = 200)
        {
            _context.Check(); if (maxLength < 1) throw new ArgumentOutOfRangeException(nameof(maxLength));
            if (maxLength > _context.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength),_context.Budget.MaxTextLength);
            if(!Enum.IsDefined(typeof(BusinessTextKind),kind))throw new ArgumentOutOfRangeException(nameof(kind));
            bool zh=_context.Locale=="zh-CN";
            string[] topics=zh?new[]{"服务","订单","交付","账户","库存","产品","售后","活动"}:new[]{"service","orders","delivery","accounts","stock","products","support","events"};
            string[] actions=zh?new[]{"升级","优化","调整","检查","说明","指南","安排","提醒"}:new[]{"update","improvement","review","check","overview","guide","schedule","reminder"};
            string[] teams=zh?new[]{"服务团队","项目团队","运营团队","支持团队","我们的同事","工作人员"}:new[]{"Our service team","The project team","Our operations team","The support team","Our colleagues","The staff"};
            string[] qualities=zh?new[]{"操作简单","包装完整","说明清楚","结构合理","使用方便","外观整洁"}:new[]{"easy to use","well packaged","clearly documented","well structured","convenient","neatly finished"};
            string[] uses=zh?new[]{"日常办公","家庭使用","学习记录","团队协作","出行携带","日常整理"}:new[]{"office work","home use","study notes","team projects","travel","daily organization"};
            string[] timing=zh?new[]{"本周","近期","明日","本月","下周","今日"}:new[]{"this week","soon","tomorrow","this month","next week","today"};
            for(int attempt=0;attempt<_context.Budget.MaxAttempts;attempt++)
            {
                var c=_context.ForField("business["+attempt+"]");c.Check();
                string topic=c.ForField("topic").Collections.Pick(topics),action=c.ForField("action").Collections.Pick(actions);
                string team=c.ForField("team").Collections.Pick(teams),quality=c.ForField("quality").Collections.Pick(qualities);
                string use=c.ForField("use").Collections.Pick(uses),when=c.ForField("when").Collections.Pick(timing);
                string value;
                switch(kind)
                {
                    case BusinessTextKind.Title:value=zh?topic+action:char.ToUpperInvariant(topic[0])+topic.Substring(1)+" "+action;break;
                    case BusinessTextKind.Announcement:value=zh?team+"将于"+when+"开展"+topic+action+"，请留意后续通知。":team+" will provide a "+topic+" "+action+" "+when+". Please check the next notice.";break;
                    case BusinessTextKind.Review:value=zh?quality+"，适合"+use+"，整体体验符合预期。":"It is "+quality+" and suitable for "+use+". The experience met expectations.";break;
                    case BusinessTextKind.ProductDescription:value=zh?quality+"，兼顾"+use+"的便利与舒适。":"Designed to be "+quality+", with practical features for "+use+".";break;
                    default:value=zh?team+"正在完善"+topic+action+"，方便大家了解进展。":team+" is preparing a "+topic+" "+action+" to keep everyone informed.";break;
                }
                if(value.Length<=maxLength)return value;
            }
            throw new ArgumentException("长度预算不足以生成此类完整文案。",nameof(maxLength));
        }
        public string BusinessParagraph(int sentences = 3, int maxLength = 1000)
        {
            DomainGuard.Count(_context,sentences); if(maxLength < 0 || maxLength > _context.Budget.MaxTextLength) throw new ArgumentOutOfRangeException(nameof(maxLength));
            var sb=new StringBuilder(); for(int i=0;i<sentences;i++) { _context.Check(); string s=_context.ForField("sentence["+i+"]").Text.Business(); if(sb.Length+s.Length+(_context.Locale=="en-US"&&i>0?1:0)>maxLength) throw new ArgumentException("段落长度预算不足。",nameof(maxLength)); if(i>0&&_context.Locale=="en-US")sb.Append(' ');sb.Append(s); } return sb.ToString();
        }
    }
    public readonly partial struct PeopleData
    {
        private readonly MockContext _c; internal PeopleData(MockContext c){_c=c;}
        public string Username(int length = 12) => _c.Text.Random(length,length,"abcdefghijklmnopqrstuvwxyz0123456789_");
        public string Nickname() => _c.Text.Name();
        public string Biography() => _c.Text.Business();
        public string Job() => CareerCatalog.Pick(_c, null, null).Job;
    }
    public readonly partial struct CompanyData
    {
        private readonly MockContext _c; internal CompanyData(MockContext c){_c=c;}
        public string Name() => DomainGuard.Text(_c,_c.Locale=="zh-CN"?_c.Collections.Pick(new[]{"星河","远航","青禾","云帆"})+_c.Collections.Pick(new[]{"科技","商贸","咨询","设计"})+"有限公司（模拟）":_c.Collections.Pick(new[]{"Meadow","Harbor","Summit","Orchard"})+" "+_c.Collections.Pick(new[]{"Technology","Trading","Consulting","Design"})+" Example Ltd");
        /// <summary>从企业名称取得简称；中文移除组织形式，英文取首个词。</summary>
        public string ShortName(string companyName)
        {
            _c.Check();if(string.IsNullOrWhiteSpace(companyName))throw new ArgumentException("企业名称不能为空。",nameof(companyName));
            string name=companyName.Replace("（模拟）","").Trim();
            foreach(string suffix in new[]{"有限责任公司","股份有限公司","有限公司"})if(name.EndsWith(suffix,StringComparison.Ordinal)){name=name.Substring(0,name.Length-suffix.Length);break;}
            if(_c.Locale=="en-US")name=name.Split(' ')[0];
            return DomainGuard.Text(_c,name);
        }
        public string Industry() => DomainGuard.Text(_c,_c.Collections.Pick(CareerCatalog.Industries(_c.Locale)));
        public string Department() => DomainGuard.Text(_c,_c.Collections.Pick(CareerCatalog.Departments(_c.Locale, null)));
        /// <summary>按 GB 32100-2015 生成含有效区划与 MOD 31-3 校验的模拟企业标识。</summary>
        public string CreditCode(string divisionCode)
        {
            _c.Check(); if(Bing.MockData.Geography.ChinaDivisions.Find(divisionCode)==null || divisionCode.Length!=6 || !divisionCode.All(char.IsDigit)) throw new ArgumentException("需要已知六位行政区划代码。",nameof(divisionCode));
            const string symbols="0123456789ABCDEFGHJKLMNPQRTUWXY";
            // 主体标识码的前八位采用数字，末位按组织机构代码 MOD 11-2 权重计算。
            string org=_c.Text.Random(8,8,"0123456789"); int[] orgWeights={3,7,9,10,5,8,4,2}; int orgSum=0;for(int i=0;i<8;i++)orgSum+=(org[i]-'0')*orgWeights[i];int orgCheck=11-orgSum%11;org+=orgCheck==10?"X":orgCheck==11?"0":orgCheck.ToString(CultureInfo.InvariantCulture);
            string body="91"+divisionCode+org; int weight=1,sum=0; foreach(char c in body){sum+=symbols.IndexOf(c)*weight;weight=weight*3%31;} return DomainGuard.Text(_c,body+symbols[(31-sum%31)%31]);
        }
    }
    public sealed class ProductCategory
    {
        public string Code {get;} public string Name {get;} public string Material {get;} public string Specification {get;} public string Unit {get;}
        public ProductCategory(string code,string name,string material,string specification,string unit){Code=code;Name=name;Material=material;Specification=specification;Unit=unit;}
    }
    public readonly partial struct CommerceData
    {
        private readonly MockContext _c; internal CommerceData(MockContext c){_c=c;}
        public ProductCategory Category() { var sample = ProductCatalog.Generate(_c, ProductCatalog.Snapshot(_c, null)); return new ProductCategory(sample.CategoryCode, sample.CategoryName, sample.Material, sample.Specification, sample.Unit); }
        public string Brand() => ProductCatalog.Generate(_c, ProductCatalog.Snapshot(_c, null)).Brand;
        public string Sku() => _c.Identifiers.Business("SKU-");
        public string Ean13() => Barcode(12);
        public string Upc() => Barcode(11);
        private string Barcode(int bodyLength) { string body=_c.Text.Random(bodyLength,bodyLength,"0123456789"); return body+BarcodeCheckDigit(body); }
        /// <summary>GS1 GTIN 的模 10 校验位，输入不包含校验位。</summary>
        public static char BarcodeCheckDigit(string body)
        { if(string.IsNullOrEmpty(body)||!body.All(c=>c>='0'&&c<='9'))throw new ArgumentException("条码必须为数字。",nameof(body));int sum=0,weight=3;for(int i=body.Length-1;i>=0;i--){sum+=(body[i]-'0')*weight;weight=4-weight;}return (char)('0'+(10-sum%10)%10); }
    }
    public sealed class CurrencyInfo
    { public string Code {get;} public int DecimalPlaces {get;} public CurrencyInfo(string code,int decimalPlaces){if(code==null||code.Length!=3||!code.All(c=>c>='A'&&c<='Z'))throw new ArgumentException("币种代码必须为三位大写字母。");if(decimalPlaces<0||decimalPlaces>28)throw new ArgumentOutOfRangeException(nameof(decimalPlaces));Code=code;DecimalPlaces=decimalPlaces;} }
    public sealed class LedgerEntry
    { public DateTimeOffset Timestamp {get;} public decimal Amount {get;} public decimal Balance {get;} internal LedgerEntry(DateTimeOffset time,decimal amount,decimal balance){Timestamp=time;Amount=amount;Balance=balance;} }
    public sealed class AccountLedger
    { public CurrencyInfo Currency {get;} public decimal OpeningBalance {get;} public IReadOnlyList<LedgerEntry> Entries {get;} public decimal ClosingBalance => Entries.Count==0?OpeningBalance:Entries[Entries.Count-1].Balance;internal AccountLedger(CurrencyInfo currency,decimal opening,List<LedgerEntry> entries){Currency=currency;OpeningBalance=opening;Entries=entries.AsReadOnly();} }
    public readonly partial struct FinanceData
    {
        private readonly MockContext _c;internal FinanceData(MockContext c){_c=c;}
        /// <summary>核心常用币种样本；完整币种范围由 CLDR 目录提供。</summary>
        public CurrencyInfo Currency() => _c.Collections.Pick(new[]{new CurrencyInfo("CNY",2),new CurrencyInfo("USD",2),new CurrencyInfo("EUR",2),new CurrencyInfo("JPY",0),new CurrencyInfo("KWD",3)});
        public decimal Amount(CurrencyInfo currency,decimal min=0,decimal max=10000) => _c.Numbers.Decimal(min,max,(currency??throw new ArgumentNullException(nameof(currency))).DecimalPlaces);
        public string AccountType() => _c.Collections.Pick(new[]{"checking","savings","business"});
        public AccountLedger Ledger(int count=10,decimal openingBalance=1000,CurrencyInfo currency=null)
        {
            DomainGuard.Count(_c,count);currency=currency??Currency();if(decimal.Round(openingBalance,currency.DecimalPlaces)!=openingBalance)throw new ArgumentException("期初余额精度与币种不一致。");
            var entries=new List<LedgerEntry>();decimal balance=openingBalance;for(int i=0;i<count;i++){var c=_c.ForField("entry["+i+"]");decimal amount=c.Finance.Amount(currency,-100,100);balance=checked(balance+amount);entries.Add(new LedgerEntry(_c.ReferenceTime.AddMinutes(i-count),amount,balance));}return new AccountLedger(currency,openingBalance,entries);
        }
    }
    public enum InvalidInputKind { Unicode, Whitespace, Newlines, Escapes, Date, CheckDigit, Duplicate }
    public readonly partial struct BoundaryData
    {
        /// <summary>显式生成异常测试文本；Unicode 类型可包含孤立代理项。</summary>
        public string Invalid(InvalidInputKind kind,string validValue="12345670")
        {
            string value;switch(kind){case InvalidInputKind.Unicode:value="\uD800";break;case InvalidInputKind.Whitespace:value=" \t\u00A0\u200B";break;case InvalidInputKind.Newlines:value="first\r\nsecond\nthird\r";break;case InvalidInputKind.Escapes:value="\"\\<>&'";break;case InvalidInputKind.Date:value="2025-02-30T25:61:61Z";break;case InvalidInputKind.CheckDigit:if(string.IsNullOrEmpty(validValue))throw new ArgumentException(nameof(validValue));value=validValue.Substring(0,validValue.Length-1)+(validValue[validValue.Length-1]=='0'?'1':'0');break;case InvalidInputKind.Duplicate:value=validValue??throw new ArgumentNullException(nameof(validValue));break;default:throw new ArgumentOutOfRangeException(nameof(kind));}return DomainGuard.Text(_context,value);
        }
    }
    public sealed partial class MockContext
    { public PeopleData People=>new PeopleData(this); public CompanyData Companies=>new CompanyData(this); public CommerceData Commerce=>new CommerceData(this); public FinanceData Finance=>new FinanceData(this); }
}
