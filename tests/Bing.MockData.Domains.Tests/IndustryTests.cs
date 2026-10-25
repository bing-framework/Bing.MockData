#pragma warning disable xUnit1051
using System;
using System.IO;
using System.Linq;
using Bing.MockData.Catalogs;
using Bing.MockData.Transport;
using Bing.MockData.Devices;
using Bing.MockData.Education;
using Bing.MockData.Healthcare;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Domains.Tests;
public class IndustryTests
{
    private static string Data(string file){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir!=null){string p=Path.Combine(dir.FullName,"src","Bing.MockData.Catalogs","Data",file);if(File.Exists(p))return p;dir=dir.Parent;}throw new FileNotFoundException(file);}
    [Fact]public void Device_models_resolve_real_manufacturers_and_identifiers_have_checksums()
    {
        using var c=new MockContext(new MockOptions{Seed=13}.UseCatalogFile("devices",Data("pci-ids.sqlite3")));
        var rows=c.Devices().Device().Build().GenerateMany(20);foreach(var d in rows){Assert.StartsWith("device|"+d.Manufacturer.Key.Split('|')[1]+"|",d.Model.Key);Assert.Equal(15,d.Imei.Length);int sum=d.Imei.Select((x,i)=>{int n=x-'0';if(i%2==1){n*=2;if(n>9)n-=9;}return n;}).Sum();Assert.Equal(0,sum%10);Assert.False(d.HasVerifiedTacMapping);}
    }
    [Fact]public void Airports_and_file_metadata_use_offline_catalog_records()
    {
        using var mime=SqliteMockCatalog.Open(Data("mime-db.sqlite3"));using var c=new MockContext(new MockOptions{Seed=12}.UseCatalogFile("airports",Data("ourairports.sqlite3")).UseMimeTypeCatalog(mime));
        Assert.Equal("CN",c.Transport().Airport("CN").CountryCode);var file=c.Files.Metadata("json");Assert.Equal("json",file.Extension);Assert.Equal("application/json",file.MimeType);Assert.True(file.CreatedAt<=file.ModifiedAt);
        for(int i=0;i<50;i++){Assert.Equal(17,c.Transport().Vin().Length);Assert.Equal(7,c.Transport().ChinaPlate().Length);Assert.Equal(8,c.Transport().ChinaPlate(newEnergy:true).Length);var isbn=c.Education().Isbn13();Assert.Equal(CommerceData.BarcodeCheckDigit(isbn[..^1]),isbn[^1]);}
    }
    [Fact]public void Explicit_missing_industry_catalogs_fail_instead_of_fabricating_records()
    {using var c=new MockContext();Assert.Throws<InvalidOperationException>(()=>c.Education().Institution());}
    [Fact]public void Full_industry_snapshots_support_related_templates()
    {
        using var c=new MockContext(new MockOptions{Seed=24}
            .UseCatalogFile("vehicles",Data("vehicles.sqlite3")).UseCatalogFile("airports",Data("ourairports.sqlite3")).UseCatalogFile("airlines",Data("wikidata-airlines.sqlite3"))
            .UseCatalogFile("institutions",Data("ror.sqlite3")).UseCatalogFile("books",Data("crossref-books.sqlite3"))
            .UseCatalogFile("diagnoses",Data("icd10cm.sqlite3")).UseCatalogFile("medications",Data("rxnorm-cpc.sqlite3")));
        var vehicle=c.Transport().Vehicle("US").Build().At(7);Assert.NotNull(vehicle);
        var flight=c.Transport().Flight().Build().At(7);Assert.NotNull(flight);
        var students=c.Education().Student("US").Build().GenerateMany(3);var courses=c.Education().Course().Build().GenerateMany(2);
        var enrollment=c.Education().Enrollment(students,courses).Build().At(2);Assert.Contains(enrollment.Student,students);Assert.Contains(enrollment.Course,courses);Assert.True(enrollment.EnrolledAt<=enrollment.AssessedAt);
        var book=c.Education().Book();Assert.Equal(book.Key,((string)JObject.Parse(book.PayloadJson)["DOI"]!).ToLowerInvariant());
        var encounter=c.Healthcare().Encounter(c.Healthcare().Patient().Build().At(1)).Build().At(1);Assert.All(encounter.Observations,o=>Assert.InRange(o.Timestamp,encounter.StartedAt,encounter.EndedAt));
        Assert.Equal("FY2027",(string)JObject.Parse(encounter.Diagnosis.PayloadJson)["version"]!);Assert.Equal("medication",c.Healthcare().Medication().Kind);
    }
    [Fact]public void Phone_currency_and_geography_preserve_source_evidence()
    {
        using var phone=SqliteMockCatalog.Open(Data("libphonenumber.sqlite3"));using var currency=SqliteMockCatalog.Open(Data("cldr.sqlite3"));
        using var postal0=SqliteMockCatalog.Open(Data("geonames-postal-0.sqlite3"));using var postal1=SqliteMockCatalog.Open(Data("geonames-postal-1.sqlite3"));
        using var c=new MockContext(new MockOptions{Seed=39}.UsePhoneMetadataCatalog(phone).UseCurrencyCatalog(currency).UseGeographicCatalogs(new[]{postal0,postal1}));
        Assert.Equal(0,c.Finance.Currency("JPY").DecimalPlaces);Assert.Equal(3,c.Finance.Currency("KWD").DecimalPlaces);
        var cnPattern=new CatalogPhoneMetadataProvider(phone).GetPattern("CN",PhoneNumberType.Mobile);var cnNumber=c.Text.Regex(cnPattern.NationalPattern);
        Assert.True(System.Text.RegularExpressions.Regex.IsMatch(cnNumber,"\\A(?:"+cnPattern.NationalPattern+")\\z"),cnPattern.NationalPattern+" => "+cnNumber);
        foreach(string country in new[]{"CN","US","GB","DE","FR","JP","IN","BR","AU","ZA"})foreach(var type in new[]{PhoneNumberType.Mobile,PhoneNumberType.FixedLine})
        {var number=c.Phone.Number(country,type,true);Assert.StartsWith("+"+number.CallingCode,number.E164);Assert.Equal("+"+number.CallingCode+number.NationalNumber,number.E164);Assert.Equal(4,number.Extension.Length);}
        var place=c.Geography.Postal("US","90210");Assert.Equal("90210",place.PostalCode);Assert.Equal("US",place.CountryCode);Assert.NotNull(place.Coordinates);
        Assert.Throws<CatalogCoverageException>(()=>c.Geography.Postal("CN","unknown"));
    }
}
