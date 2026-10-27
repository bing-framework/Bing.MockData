#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;
using Bing.MockData.Datas;
using Bing.MockData.Datas.Models.Banks;
using Bing.MockData.Internals.Generators;
using Newtonsoft.Json;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class BankCardCapabilityTests
{
    private static MockOptions Options() => new MockOptions { Seed = 39, ReferenceTime = new DateTimeOffset(2026,10,27,0,0,0,TimeSpan.Zero) };

    [Fact]
    public void Every_source_rule_generates_twenty_valid_replayable_records()
    {
        using var context = new MockContext(Options());
        using var replay = new MockContext(context.Replay.ToOptions());
        var results = new List<object>(); int samples = 0;
        foreach (var bank in CommonData.Instance.Banks)
        foreach (var rule in bank.AllRules)
        {
            string key = $"{bank.Code}|{rule.CardPrefix}|{rule.Length}|{(int)rule.Type}";
            for (int i = 0; i < 20; i++)
            {
                var c = context.ForRecord(i).ForField(key);
                string value = c.WithRandom(r => BankCardGenerator.GenerateRule(r, rule, c.CancellationToken));
                var restored = replay.ForRecord(i).ForField(key);
                Assert.Equal(value, restored.WithRandom(r => BankCardGenerator.GenerateRule(r, rule, restored.CancellationToken)));
                Assert.StartsWith(rule.CardPrefix, value);
                Assert.Equal(rule.CardPrefix.Length + rule.Length, value.Length);
                Assert.InRange(value.Length, 15, 19); Assert.True(ValidLuhn(value), key); samples++;
            }
            results.Add(new { Key = key, Passed = 20 });
        }
        string asset = ((TargetFrameworkAttribute)Attribute.GetCustomAttribute(typeof(MockContext).Assembly, typeof(TargetFrameworkAttribute))).FrameworkName;
        string directory = Environment.GetEnvironmentVariable("MOCKDATA_EVIDENCE_DIRECTORY") ?? Path.Combine(AppContext.BaseDirectory, "bank-evidence");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "banks-matrix-"+(asset.Contains("Standard")?"netstandard2.0":"net10.0")+".json"),
            JsonConvert.SerializeObject(new { Asset=asset, context.Seed, context.ReferenceTime, Version=context.Replay.DataVersion, Rules=results.Count, Samples=samples, Results=results }, Formatting.Indented));
        Assert.Equal(845, results.Count); Assert.Equal(16900, samples);
        Assert.Equal(new[]{31,381,60,145,80,41,57,50}, CommonData.Instance.Banks.Select(b=>b.AllRules.Count));
        var cmb = CommonData.Instance.Banks.Single(b=>b.Code=="308");
        Assert.Equal(CardType.CreditCard,cmb.AllRules.Single(r=>r.CardPrefix=="356885").Type);
        Assert.DoesNotContain(cmb.AllRules,r=>r.CardPrefix=="622580" && r.Length==11);
    }

    [Fact]
    public void Public_filters_and_legacy_entry_produce_matching_source_rules()
    {
        using var c = new MockContext(Options());
        foreach (var bank in CommonData.Instance.Banks)
        foreach (var type in bank.AllRules.Select(r=>r.Type).Distinct())
        {
            var rules=bank.AllRules.Where(r=>r.Type==type).ToArray();
            for(int i=0;i<20;i++)
            {
                var value=c.Finance.BankCard(bank.Code,type);
                Assert.True(ValidLuhn(value));
                Assert.Contains(rules,r=>value.StartsWith(r.CardPrefix,StringComparison.Ordinal)&&value.Length==r.CardPrefix.Length+r.Length);
            }
        }
        var legacy = new BankCardRandomizer(new BankCardFieldOptions { Seed=39 });
        for(int i=0;i<20;i++) Assert.True(ValidLuhn(legacy.Generate()));
    }

    [Fact]
    public void Invalid_filters_coverage_and_budget_fail_without_consuming_randomness()
    {
        using var c = new MockContext(Options()); using var untouched = new MockContext(Options());
        foreach(string code in new[]{"", "  ", "１０２", "1020", "a02"}) Assert.Throws<ArgumentException>(()=>c.Finance.BankCard(code));
        Assert.Throws<ArgumentOutOfRangeException>(()=>c.Finance.BankCard(cardType:CardType.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(()=>c.Finance.BankCard(cardType:(CardType)8));
        var error=Assert.Throws<InvalidOperationException>(()=>c.Finance.BankCard("999",CardType.DebitCard));
        Assert.Contains("999",error.Message);Assert.Contains("bankcardinfo@2.0.4",error.Message);
        Assert.Throws<InvalidOperationException>(()=>c.Finance.BankCard("100",CardType.PrepaidCard));
        Assert.Equal(untouched.Numbers.Int(),c.Numbers.Int());
        // 18 仍能容纳部分规则：必须拒绝整个请求，不能默默过滤掉 19 位规则。
        foreach(int maximum in new[]{14,18})
        {
            var small=Options();small.Limits.MaxTextLength=maximum;
            using var limited = new MockContext(small);using var original=new MockContext(small);
            Assert.Throws<MockLimitException>(()=>limited.Finance.BankCard());
            Assert.Equal(original.Numbers.Int(),limited.Numbers.Int());
            Assert.Throws<MockLimitException>(()=>new BankCardRandomizer(new BankCardFieldOptions{Seed=39,MaxTextLength=maximum}).Generate());
        }
    }

    [Fact]
    public void Context_replay_field_isolation_parallelism_and_cancellation_are_preserved()
    {
        using var c = new MockContext(Options());
        var g=c.Generator(x=>x.ForField("card").Finance.BankCard());
        var expected=g.Enumerate(100).ToArray();
        Assert.Equal(expected,g.Enumerate(100));Assert.Equal(expected,g.EnumerateParallel(100,4));
        Assert.Equal(expected[37],g.At(37));
        Assert.Equal(expected,c.Generator(x=>{x.ForField("unrelated").Text.Random(5,5);return x.ForField("card").Finance.BankCard();}).Enumerate(100));
        using var restored=new MockContext(JsonConvert.DeserializeObject<ReplayInfo>(JsonConvert.SerializeObject(c.Replay)).ToOptions());
        Assert.Equal(expected,restored.Generator(x=>x.ForField("card").Finance.BankCard()).Enumerate(100));
        using var cancel=new CancellationTokenSource();
        using(var iterator=g.Enumerate(100,cancel.Token).GetEnumerator())
        {Assert.True(iterator.MoveNext());cancel.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>iterator.MoveNext());}
        var failure=Assert.Throws<MockGenerationException>(()=>c.ForField("payment").Generator(x=>x.Finance.BankCard("999"),"bank-rule").At(17));
        Assert.Equal(17,failure.Replay.RecordIndex);Assert.Equal("payment",failure.Replay.FieldPath);
        c.Dispose();Assert.Throws<ObjectDisposedException>(()=>c.Finance.BankCard());
    }

    [Fact]
    public void Previous_bank_data_or_contract_is_not_silently_replayed()
    {
        using var c=new MockContext(Options());var r=c.Replay;
        foreach(var old in new[]{(Contract:"domain-v5",Data:r.DataVersion),(Contract:r.GenerationContractVersion,Data:"china-2025-12-31-domain-v4")})
        {
            var previous=new ReplayInfo(r.Seed,r.ReferenceTime,r.Locale,r.AlgorithmVersion,old.Data,r.LibraryVersion,r.RuntimeVersion,0,"",r.Limits,old.Contract,r.ExtensionVersions);
            var error=Assert.Throws<InvalidOperationException>(()=>previous.ToOptions());
            Assert.Contains("domain-v6",error.Message);Assert.Contains("china-2025-12-31-domain-v5",error.Message);
        }
    }

    private static bool ValidLuhn(string value)
    {
        int sum=0;bool doubleDigit=false;
        for(int i=value.Length-1;i>=0;i--)
        {int n=value[i]-'0';if(n<0||n>9)return false;if(doubleDigit){n*=2;if(n>9)n-=9;}sum+=n;doubleDigit=!doubleDigit;}
        return sum%10==0;
    }
}
