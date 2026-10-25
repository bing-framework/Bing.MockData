// 此文件同时验证省略可选取消参数的公开契约；取消用例显式传入独立令牌。
#pragma warning disable xUnit1051
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class FoundationTests
{
    [Fact]
    public void Selected_assembly_matches_requested_target()
    {
        var target = typeof(MockContext).Assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName;
#if MOCKDATA_NETSTANDARD
        Assert.Equal(".NETStandard,Version=v2.0", target);
#else
        Assert.Equal(".NETCoreApp,Version=v10.0", target);
#endif
    }
    [Fact]
    public void Options_are_snapshotted_and_actual_seed_and_reference_time_replay()
    {
        var options = new MockOptions { Locale = "en-US", Limits = new MockLimits { MaxTextLength = 15 } };
        using var first = new MockContext(options);
        options.Locale = "zh-CN"; options.Limits.MaxTextLength = 1;
        using var second = new MockContext(first.Replay.ToOptions());
        Assert.Equal("en-US", first.Locale); Assert.Equal(15, first.Limits.MaxTextLength);
        first.Limits.MaxTextLength = 0;
        Assert.Equal(first.ReferenceTime, second.ReferenceTime);
        for (int i = 0; i < 20; i++) Assert.Equal(first.Text.Random(), second.Text.Random());
        Assert.Throws<ArgumentException>(() => new MockContext(new MockOptions { Locale = "xx" }));
    }
    [Fact]
    public void Persisted_replay_restores_a_single_record_and_rejects_other_versions()
    {
        using var mock = new MockContext(new MockOptions { Seed = 52 });
        var record = mock.ForRecord(963).ForField("User.Email");
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(record.Replay);
        var replay = Newtonsoft.Json.JsonConvert.DeserializeObject<ReplayInfo>(json);
        using var restored = new MockContext(replay.ToOptions());
        Assert.Equal(record.Text.Email(), restored.ForRecord(replay.RecordIndex).ForField(replay.FieldPath).Text.Email());
        var invalid = Newtonsoft.Json.JsonConvert.DeserializeObject<ReplayInfo>(json.Replace("fnv64-mix-v1", "unknown"));
        Assert.Throws<InvalidOperationException>(() => invalid.ToOptions());
    }
    [Fact]
    public void All_generated_strings_obey_the_text_budget()
    {
        using var mock = new MockContext(new MockOptions { Limits = new MockLimits { MaxTextLength = 2 } });
        Assert.Equal(2, mock.Text.Random(2, 2).Length);
        Assert.Throws<MockLimitException>(() => mock.Generator(_ => "abc").Generate());
        Assert.Throws<MockLimitException>(() => mock.Boundaries.Text(TextBoundary.InvalidGuid));
        Assert.Throws<MockLimitException>(() => mock.Network.IPv4());
        Assert.Throws<MockLimitException>(() => mock.Network.Url());
    }
    [Fact]
    public void Decimal_grid_is_inclusive_precise_and_supports_extremes()
    {
        using var mock = new MockContext(new MockOptions { Seed = 4 });
        Assert.Equal(decimal.MaxValue, mock.Numbers.Decimal(decimal.MaxValue, decimal.MaxValue, 0));
        Assert.Equal(decimal.MinValue, mock.Numbers.Decimal(decimal.MinValue, decimal.MinValue, 0));
        Assert.Equal(0.10m, mock.Numbers.Decimal(0.099m, 0.101m));
        Assert.Equal(-0.10m, mock.Numbers.Decimal(-0.101m, -0.099m));
        Assert.Throws<ArgumentException>(() => mock.Numbers.Decimal(0.001m, 0.009m));
        Assert.Throws<ArgumentException>(() => mock.Numbers.Decimal(0, 1, 2, 0.001m));
        var samples = Enumerable.Range(0, 100).Select(_ => mock.Numbers.Decimal(-1m, 1m, 2, 0.25m)).ToArray();
        Assert.All(samples, x => { Assert.InRange(x, -1m, 1m); Assert.Equal(0, x % 0.25m); });
        Assert.Contains(-1m, samples); Assert.Contains(1m, samples);
        for (int i = 0; i < 10; i++) Assert.InRange(mock.Numbers.Between(decimal.MinValue, decimal.MaxValue), decimal.MinValue, decimal.MaxValue);
    }
    [Fact]
    public void Large_decimal_grids_never_round_off_the_step_or_bounds()
    {
        using var mock = new MockContext(new MockOptions { Seed = 5 });
        for (int i = 0; i < 100; i++)
        {
            var positive = mock.Numbers.Decimal(decimal.MaxValue - 30, decimal.MaxValue, 2, 0.03m);
            var negative = mock.Numbers.Decimal(decimal.MinValue, decimal.MinValue + 30, 2, 0.03m);
            Assert.InRange(positive, decimal.MaxValue - 30, decimal.MaxValue); Assert.Equal(0, positive % 0.03m);
            Assert.InRange(negative, decimal.MinValue, decimal.MinValue + 30); Assert.Equal(0, negative % 0.03m);
        }
        Assert.Equal(decimal.MaxValue, mock.Numbers.Decimal(decimal.MaxValue, decimal.MaxValue, 2, 0.03m));
        Assert.Throws<ArgumentException>(() => mock.Numbers.Decimal(decimal.MaxValue - 1, decimal.MaxValue - 1, 2, 0.03m));
    }
    [Fact]
    public void Unsigned_ranges_and_probability_endpoints_work()
    {
        using var mock = new MockContext(new MockOptions { Seed = 99 });
        Assert.Equal(ulong.MaxValue, mock.Numbers.ULong(ulong.MaxValue, ulong.MaxValue));
        Assert.Equal(uint.MaxValue, mock.Numbers.UInt(uint.MaxValue, uint.MaxValue));
        for (int i = 0; i < 100; i++) { Assert.InRange(mock.Numbers.ULong(), 0UL, ulong.MaxValue); Assert.False(mock.Numbers.Boolean(0)); Assert.True(mock.Numbers.Boolean(1)); }
        Assert.Throws<ArgumentOutOfRangeException>(() => mock.Numbers.Boolean(double.NaN));
        Assert.Equal("selected", mock.Collections.Weighted(new[] { "ignored", "selected" }, new[] { 0d, 1d }));
        Assert.Throws<ArgumentOutOfRangeException>(() => mock.Collections.Weighted(new[] { 1 }, new[] { 0d }));
        Assert.True(Enum.IsDefined(mock.Numbers.Enum<DayOfWeek>()));
    }
    [Fact]
    public void Sampling_has_no_replacement_and_does_not_mutate_candidates()
    {
        using var mock = new MockContext(new MockOptions { Seed = 22 });
        var original = Enumerable.Range(0, 100).ToArray();
        var sample = mock.Collections.Sample(original, 100);
        Assert.Equal(original, sample.OrderBy(x => x)); Assert.Equal(original, Enumerable.Range(0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => mock.Collections.Sample(original, 101));
        Assert.Throws<ArgumentException>(() => mock.Collections.Pick(Array.Empty<string>()));
    }
    [Fact]
    public void Unicode_alphabets_preserve_surrogate_pairs_and_UTF16_limits()
    {
        using var mock = new MockContext(new MockOptions { Seed = 17 });
        Assert.Equal("😀😀", mock.Text.Random(4, 4, "😀"));
        Assert.Throws<ArgumentException>(() => mock.Text.Random(3, 3, "😀"));
        Assert.Throws<ArgumentException>(() => mock.Text.Random(1, 1, "\ud800"));
        var text = mock.Text.Random(101, 101, "😀中A");
        Assert.Equal(101, text.Length);
        for (int i = 0; i < text.Length; i++) if (char.IsSurrogate(text[i])) { Assert.True(char.IsHighSurrogate(text[i])); Assert.True(char.IsLowSurrogate(text[++i])); }
    }
    [Fact]
    public void Explicit_nullable_wrappers_and_condition_attempts_are_bounded()
    {
        using var mock = new MockContext(new MockOptions { Seed = 7, Limits = new MockLimits { MaxAttempts = 3 } });
        var number = mock.Generator(c => c.Numbers.Int(5, 5));
        Assert.Null(number.Nullable(1).Generate()); Assert.Equal(5, number.Nullable(0).Generate());
        Assert.Null(mock.Generator(_ => "value").OrNull(1).Generate());
        Assert.Throws<MockLimitException>(() => number.Where(x => x != 5).Generate());
        var values = number.Nullable(0.5).GenerateMany(100);
        Assert.Contains(values, x => !x.HasValue); Assert.Contains(values, x => x.HasValue);
    }
    [Fact]
    public void Registered_factories_preserve_record_context_and_registration_is_frozen()
    {
        var options = new MockOptions { Seed = 42 };
        options.RegisterFactory("ordinal", c => c.Generator(r => r.RecordIndex));
        using var mock = new MockContext(options);
        options.Register("later", _ => 1);
        Assert.Equal(17, mock.Registered<long>("ordinal").At(17));
        Assert.Equal(Enumerable.Range(0, 10).Select(i => (long)i), mock.Registered<long>("ordinal").GenerateMany(10));
        Assert.Throws<ArgumentException>(() => mock.Registered<int>("later"));
        using var next = new MockContext(options); Assert.Equal(1, next.Registered<int>("later").Generate());
    }
    [Fact]
    public void Date_offsets_preserve_instants_and_reference_time()
    {
        using var mock = new MockContext(new MockOptions { Seed = 9, ReferenceTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        var a = new DateTimeOffset(2020, 1, 1, 8, 0, 0, TimeSpan.FromHours(8));
        var b = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = mock.Dates.Between(a, b); Assert.Equal(a, result); Assert.Equal(a.Offset, result.Offset);
        Assert.Equal(mock.ReferenceTime, mock.Dates.Past(TimeSpan.Zero));
#if !MOCKDATA_NETSTANDARD
        Assert.Equal(new DateOnly(2020, 1, 1), mock.Dates.Between(new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 1)));
        Assert.Equal(TimeOnly.MinValue, mock.Dates.Between(TimeOnly.MinValue, TimeOnly.MinValue));
#endif
    }
}
