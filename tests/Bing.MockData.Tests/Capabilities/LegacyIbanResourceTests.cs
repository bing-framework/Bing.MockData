using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;
using Bing.MockData.Datas;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class LegacyIbanResourceTests
{
    [Fact]
    public void Chinese_surnames_use_unique_fixed_faker_projection()
    {
        var names=CommonData.Instance.ChineseFirstNames;
        Assert.Equal(399,names.Count);Assert.Equal(names.Count,names.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("李",names);Assert.All(names,name=>Assert.False(string.IsNullOrWhiteSpace(name)));
    }

    [Fact]
    public void Old_embedded_data_identity_is_rejected_even_with_current_generation_contract()
    {
        using var c=new MockContext(new MockOptions{Seed=39});var r=c.Replay;
        var old=new ReplayInfo(r.Seed,r.ReferenceTime,r.Locale,r.AlgorithmVersion,"china-2025-12-31-domain-v3",r.LibraryVersion,r.RuntimeVersion,0,"",r.Limits,r.GenerationContractVersion,r.ExtensionVersions);
        Assert.Throws<InvalidOperationException>(()=>old.ToOptions());
    }

    [Fact]
    public void All_fifty_four_country_rules_keep_format_and_mod97_semantics()
    {
        var rules=CommonData.Instance.IBANs.ToArray();
        Assert.Equal(54,rules.Length);Assert.Equal(54,CommonData.Instance.BBANs.Count());
        foreach(var rule in rules)
        {
            var iban=new IBANRandomizer(new IBANFieldOptions{CountryCode=rule.CountryCode,Type="IBAN"});
            var bbanRule=CommonData.Instance.BBANs.Single(x=>x.CountryCode==rule.CountryCode);
            var bban=new IBANRandomizer(new IBANFieldOptions{CountryCode=rule.CountryCode,Type="BBAN"});
            for(int i=0;i<20;i++)
            {
                string value=iban.Generate();
                Assert.Matches(new Regex(rule.Pattern,RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1)),value);
                string digits=string.Concat((value.Substring(4)+value.Substring(0,4)).Select(ch=>ch>='A'&&ch<='Z'?(ch-'A'+10).ToString():ch.ToString()));
                Assert.Equal(BigInteger.One,BigInteger.Parse(digits)%97);
                Assert.Matches(new Regex(bbanRule.Pattern,RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1)),bban.Generate());
            }
        }
    }
}
