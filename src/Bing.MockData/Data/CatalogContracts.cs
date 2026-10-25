using System;
using System.Threading;

namespace Bing.MockData
{
    /// <summary>来自同一离线目录行的地点；邮编粒度和坐标精度保留来源定义。</summary>
    public sealed class CatalogLocation
    {
        public string Key {get;} public string CountryCode {get;} public string PlaceName {get;}
        public string PostalCode {get;} public GeoPoint? Coordinates {get;} public int? Accuracy {get;}
        public string AdministrativeArea {get;} public string DataVersion {get;}
        public CatalogLocation(string key,string country,string place,string postal,GeoPoint? coordinates,int? accuracy,string area,string version)
        {Key=key;CountryCode=country;PlaceName=place;PostalCode=postal;Coordinates=coordinates;Accuracy=accuracy;AdministrativeArea=area;DataVersion=version;}
    }
    public interface IGeographicCatalogProvider
    { CatalogLocation Sample(string country,string postalCode,bool city,ulong selector,CancellationToken cancellationToken); }
    public interface ICurrencyCatalogProvider { CurrencyInfo Get(string code); }
    public readonly partial struct GeographyData
    {
        /// <summary>按国家及可选邮编筛选来源行，不将城市名模糊匹配到行政区划。</summary>
        public CatalogLocation Postal(string countryCode,string postalCode=null)=>_c.Extension<IGeographicCatalogProvider>("geographic-postal").Sample(countryCode,postalCode,false,_c.Numbers.ULong(),_c.CancellationToken);
        /// <summary>返回目录城市、原始名称与同一记录的 WGS84 坐标。</summary>
        public CatalogLocation City(string countryCode)=>_c.Extension<IGeographicCatalogProvider>("geographic-cities").Sample(countryCode,null,true,_c.Numbers.ULong(),_c.CancellationToken);
    }
    public readonly partial struct FinanceData
    {
        /// <summary>按已挂载 CLDR 目录返回币种精度；没有覆盖时明确报错。</summary>
        public CurrencyInfo Currency(string code)=>_c.Extension<ICurrencyCatalogProvider>("currency-metadata").Get(code);
        /// <summary>生成符合 BIC 结构的模拟银行标识，不声明对应真实银行。</summary>
        public string BankIdentifier(string countryCode="CN")
        {if(countryCode==null||countryCode.Length!=2||countryCode[0]<'A'||countryCode[0]>'Z'||countryCode[1]<'A'||countryCode[1]>'Z')throw new ArgumentException("需要两位大写国家代码。");return DomainGuard.Text(_c,_c.Text.Random(4,4,"ABCDEFGHIJKLMNOPQRSTUVWXYZ")+countryCode+_c.Text.Random(2,2,"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"));}
    }
}
