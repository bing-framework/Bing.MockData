#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Bing.MockData.Catalogs;
using Bing.MockData.Tests.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Domains.Tests;

public class PhoneCoverageTests
{
    private static MockOptions Options() => new MockOptions { Seed = 39, ReferenceTime = new DateTimeOffset(2026,10,27,0,0,0,TimeSpan.Zero) };
    private static string Signature(MockPhone p) => p.CountryCode+"|"+p.E164+"|"+p.NationalFormat+"|"+p.Extension;

    [Theory]
    [InlineData("870")]
    [InlineData("881")]
    [InlineData("882")]
    public void Non_geographic_rules_are_addressable_and_keep_region_001(string callingCode)
    {
        using var catalog = SqliteMockCatalog.Open(CatalogData.FilePath("libphonenumber.sqlite3"));
        using var c = new MockContext(Options().UsePhoneMetadataCatalog(catalog));
        var p = c.Phone.NonGeographicNumber(callingCode, extension:true);
        Assert.Equal("001", p.CountryCode); Assert.Equal(callingCode, p.CallingCode);
        Assert.StartsWith("+"+callingCode, p.E164); Assert.Equal(4, p.Extension.Length);
        Assert.Throws<CatalogCoverageException>(() => c.Phone.NonGeographicNumber(callingCode, PhoneNumberType.FixedLine));
    }

    [Fact]
    public void Optional_provider_and_formatting_preserve_existing_custom_provider_contract()
    {
        using var c = new MockContext(Options().UseExtension<IPhoneMetadataProvider>("phone-metadata", new FixedProvider(), "fixture"));
        var p = c.Phone.Number("GB", PhoneNumberType.FixedLine, true);
        Assert.Equal("+442012345678", p.E164); Assert.Equal("020 1234 5678", p.NationalFormat);
        Assert.Throws<NotSupportedException>(() => c.Phone.NonGeographicNumber("870"));
        foreach (string code in new[]{"", "+870", "087", "1234", "８７０", " 870"})
            Assert.Throws<ArgumentException>(() => c.Phone.NonGeographicNumber(code));
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Phone.Number("GB", (PhoneNumberType)9));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => c.Generator(x => x.Phone.Number("GB")).At(0, cancel.Token));
        c.Dispose(); Assert.Throws<ObjectDisposedException>(() => c.Phone.NonGeographicNumber("870"));
    }

    [Fact]
    public void Non_geographic_generation_cancels_and_owned_catalog_is_released_after_early_end()
    {
        string path=Path.Combine(Path.GetTempPath(),"mockdata-phone-"+Guid.NewGuid().ToString("N")+".sqlite3");
        File.Copy(CatalogData.FilePath("libphonenumber.sqlite3"),path);
        try
        {
            var catalog=SqliteMockCatalog.Open(path);
            var context=new MockContext(Options().UsePhoneMetadataCatalog(catalog,ownsCatalog:true));
            var generator=context.Generator(x=>x.Phone.NonGeographicNumber("870"));
            using var cancel=new CancellationTokenSource();
            using(var e=generator.Enumerate(20,cancel.Token).GetEnumerator())
            {Assert.True(e.MoveNext());Assert.Null(e.Current.Extension);cancel.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>e.MoveNext());}
            context.Dispose();context.Dispose();
            Assert.Throws<ObjectDisposedException>(()=>generator.At(0));
            using(var exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Assert.True(exclusive.Length>0);
        }
        finally {File.Delete(path);}
    }

    [Fact]
    public void Every_imported_phone_rule_generates_and_replays_twenty_records()
    {
        using var catalog = SqliteMockCatalog.Open(CatalogData.FilePath("libphonenumber.sqlite3"));
        using var c = new MockContext(Options().UsePhoneMetadataCatalog(catalog));
        var restoredInfo=JsonConvert.DeserializeObject<ReplayInfo>(JsonConvert.SerializeObject(c.Replay))!;
        using var replay=new MockContext(restoredInfo.ToOptions().UsePhoneMetadataCatalog(catalog));
        var summaries = new List<object>(); var failures = new List<string>(); int rules = 0, samples = 0;
        using var keyStream=typeof(PhoneCoverageTests).Assembly.GetManifestResourceStream("phone-rule-keys")!;
        using var keyReader=new StreamReader(keyStream);
        var keys=keyReader.ReadToEnd().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
        foreach (var row in catalog.Query(new CatalogQuery { Kind = "phone-pattern" }))
        {
            Assert.Equal(keys[rules],row.Key);
            rules++; int passed = 0;
            try
            {
                var payload = JObject.Parse(row.PayloadJson);
                string region = (string)payload["countryCode"]!, calling = (string)payload["callingCode"]!;
                var type = (string)payload["type"] == "mobile" ? PhoneNumberType.Mobile : PhoneNumberType.FixedLine;
                var pattern = new Regex("\\A(?:"+(string)payload["sourcePattern"]!+")\\z", RegexOptions.IgnorePatternWhitespace|RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                var g = c.ForField(row.Key).Generator(x => region == "001" ? x.Phone.NonGeographicNumber(calling,type,true) : x.Phone.Number(region,type,true));
                var replayed = replay.ForField(row.Key).Generator(x => region == "001" ? x.Phone.NonGeographicNumber(calling,type,true) : x.Phone.Number(region,type,true));
                using var serial = g.Enumerate(20).GetEnumerator();
                using var repeated = g.Enumerate(20).GetEnumerator();
                using var parallel = g.EnumerateParallel(20,4).GetEnumerator();
                for (int i=0;i<20;i++)
                {
                    Assert.True(serial.MoveNext()); Assert.True(repeated.MoveNext()); Assert.True(parallel.MoveNext());
                    var p=serial.Current;
                    Assert.Matches(pattern,p.NationalNumber);
                    Assert.Matches("^[0-9]+$",p.NationalNumber); Assert.Equal(region,p.CountryCode); Assert.Equal(calling,p.CallingCode);
                    Assert.Equal("+"+calling+p.NationalNumber,p.E164); Assert.InRange(p.E164.Length,2,16);
                    Assert.Matches("^[0-9]{4}$",p.Extension); Assert.DoesNotContain("$",p.NationalFormat);
                    string formattedDigits = new string(p.NationalFormat.Where(ch=>ch>='0'&&ch<='9').ToArray());
                    // AR 的本地移动格式去掉国际号码开头的 9，并在区号后插入 15。
                    if(region=="AR" && p.NationalNumber.StartsWith("9",StringComparison.Ordinal))
                    {
                        Assert.StartsWith("0",p.NationalFormat);
                        Assert.Contains(" 15-",p.NationalFormat);
                        string local = new string(p.NationalFormat.Replace(" 15-", "").Where(char.IsDigit).ToArray());
                        Assert.Equal("0"+p.NationalNumber.Substring(1),local);
                    }
                    else Assert.True(formattedDigits.EndsWith(p.NationalNumber,StringComparison.Ordinal),row.Key+": "+p.NationalFormat);
                    Assert.Equal(Signature(p),Signature(repeated.Current)); Assert.Equal(Signature(p),Signature(parallel.Current));
                    Assert.Equal(Signature(p),Signature(replayed.At(i))); passed++; samples++;
                }
                Assert.False(serial.MoveNext()); Assert.False(parallel.MoveNext());
            }
            catch(Exception e) { failures.Add(row.Key+" record="+passed+": "+e.Message); }
            summaries.Add(new { Key=row.Key, Passed=passed });
        }
        string asset=typeof(MockContext).Assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute),false)
            .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single().FrameworkName.Contains("Standard")?"netstandard2.0":"net10.0";
        string output=Environment.GetEnvironmentVariable("MOCKDATA_EVIDENCE_DIRECTORY")??Path.Combine(AppContext.BaseDirectory,"phone-evidence");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"phone-coverage-"+asset+".json"),JsonConvert.SerializeObject(new { Asset=asset,Seed=c.Seed,ReferenceTime=c.ReferenceTime,Version=catalog.Info.Version,Rules=rules,Samples=samples,Failures=failures,Results=summaries },Formatting.Indented));
        Assert.Equal(492,rules); Assert.True(failures.Count==0,string.Join(Environment.NewLine,failures)); Assert.Equal(9840,samples);
    }

    private sealed class FixedProvider : IPhoneMetadataProvider
    {
        public PhonePattern GetPattern(string countryCode, PhoneNumberType type) => new PhonePattern("GB","44",type,"2012345678","0",
            new[]{new PhoneNumberFormat("([0-9]{2})([0-9]{4})([0-9]{4})","$1 $2 $3",nationalPrefixRule:"$NP$FG")});
    }
}
