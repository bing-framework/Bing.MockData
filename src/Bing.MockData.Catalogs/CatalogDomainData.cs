using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Catalogs
{
    /// <summary>CLDR 与 GeoNames 的按需适配器；借用目录，不缓存查询结果。</summary>
    public static class DomainCatalogExtensions
    {
        public static MockOptions UseGeographicCatalog(this MockOptions options,IBingMockCatalog catalog,bool cities=false,bool ownsCatalog=false)
            => options.UseGeographicCatalogs(new[]{catalog},cities,ownsCatalog);
        public static MockOptions UseGeographicCatalogs(this MockOptions options,IEnumerable<IBingMockCatalog> catalogs,bool cities=false,bool ownsCatalogs=false)
        {
            string name=cities?"geographic-cities":"geographic-postal";
            var snapshot=(catalogs??throw new ArgumentNullException(nameof(catalogs))).ToArray();if(snapshot.Length==0||snapshot.Any(c=>c==null))throw new ArgumentException("需要目录分片。");
            for(int i=0;i<snapshot.Length;i++)options.UseCatalog(name+"-"+i,snapshot[i],ownsCatalogs);
            return options.UseExtensionFactory(name,()=>new GeographyProvider(snapshot),string.Join(";",snapshot.Select(c=>c.Info.Id+"@"+c.Info.Version)));
        }
        public static MockOptions UseCurrencyCatalog(this MockOptions options,IBingMockCatalog catalog,bool ownsCatalog=false)
        {
            options.UseCatalog("currency-metadata",catalog,ownsCatalog);
            return options.UseExtensionFactory("currency-metadata",()=>new CurrencyProvider(catalog),catalog.Info.Version);
        }
        private sealed class GeographyProvider:IGeographicCatalogProvider,IDisposable
        {
            private readonly IBingMockCatalog[] _catalogs;private bool _disposed;
            internal GeographyProvider(IBingMockCatalog[] catalogs){_catalogs=catalogs;}
            public CatalogLocation Sample(string country,string postalCode,bool city,ulong selector,CancellationToken cancellationToken)
            {
                if(_disposed)throw new ObjectDisposedException(nameof(GeographyProvider));
                if(country==null||country.Length!=2)throw new ArgumentException("需要明确的两位国家代码。");
                country=country.ToUpperInvariant();var query=new CatalogQuery{Kind=city?"city":"postal",CountryCode=country,KeyPrefix=postalCode==null?null:"postal|"+country+"|"+postalCode+"|",CancellationToken=cancellationToken};
                var counts=_catalogs.Select(c=>c.Count(query)).ToArray();long total=0;foreach(long count in counts)total=checked(total+count);if(total==0)throw new CatalogCoverageException(string.Join(",",_catalogs.Select(c=>c.Info.Id)),country+"/"+postalCode);
                ulong offset=selector%(ulong)total;int part=0;while(offset>=(ulong)counts[part])offset-=(ulong)counts[part++];var catalog=_catalogs[part];var row=catalog.Sample(offset,query);
                var p=JObject.Parse(row.PayloadJson);GeoPoint? coordinates=p["latitude"]?.Type==JTokenType.Null||p["longitude"]?.Type==JTokenType.Null||p["latitude"]==null||p["longitude"]==null?null:(GeoPoint?)new GeoPoint((double)p["latitude"]!,(double)p["longitude"]!);
                return new CatalogLocation(row.Key,row.CountryCode!,row.Name,(string?)p["postalCode"]!,coordinates,(int?)p["accuracy"],(string?)p["adminName1"]!,catalog.Info.Version);
            }
            public void Dispose(){_disposed=true;}
        }
        private sealed class CurrencyProvider:ICurrencyCatalogProvider,IDisposable
        {
            private readonly IBingMockCatalog _catalog;private bool _disposed;
            internal CurrencyProvider(IBingMockCatalog catalog){_catalog=catalog;}
            public CurrencyInfo Get(string code)
            {
                if(_disposed)throw new ObjectDisposedException(nameof(CurrencyProvider));
                if(!_catalog.TryGet("currency|"+code,out var row))throw new CatalogCoverageException(_catalog.Info.Id,code);
                var p=JObject.Parse(row!.PayloadJson);return new CurrencyInfo((string)p["code"]!,(int)p["digits"]!);
            }
            public void Dispose(){_disposed=true;}
        }
    }
}
