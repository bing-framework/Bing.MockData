using System.Linq;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;
using Bing.MockData.Datas;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class LegacyEmbeddedResourceTests
{
    [Fact]
    public void Projected_legacy_resources_keep_documented_coverage_and_valid_characters()
    {
        var data=CommonData.Instance;
        Assert.Equal(381,data.FemaleNames.Count());Assert.Equal(322,data.MaleNames.Count());Assert.Equal(1000,data.LastNames.Count());
        Assert.Equal(257,data.CountryNames.Count());Assert.Equal(34180,data.CityNames.Count());
        Assert.Equal(34,data.RegionList.Count);Assert.Equal(337,data.CityNameList.Count);Assert.Equal(2845,data.ProvinceCityList.Count);
        Assert.Equal(34,data.AreaCodeDict.Count);Assert.All(data.AreaCodeDict,x=>Assert.Matches("^[0-9]{2}$",x.Key));
        Assert.Equal(708,data.SimplifiedChinese.Length);
        Assert.All(data.SimplifiedChinese,ch=>Assert.InRange((int)ch,0x4e00,0x9fff));
        Assert.All(data.ProvinceCityList,Assert.NotEmpty);
    }

    [Fact]
    public void Legacy_emails_use_reserved_domains_with_frozen_seeded_sequence()
    {
        var a=new EmailAddressRandomizer(new EmailAddressFieldOptions{Seed=39});
        var b=new EmailAddressRandomizer(new EmailAddressFieldOptions{Seed=39});
        for(int i=0;i<100;i++)
        {
            string email=a.Generate();Assert.Equal(email,b.Generate());
            Assert.Contains(email.Split('@')[1],new[]{"example.com","example.net","example.org"});
            Assert.DoesNotContain(" ",email);
        }
    }
}
