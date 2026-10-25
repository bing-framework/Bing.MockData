using System;
using System.Linq;
using System.Net;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;
using Bing.MockData.Internals.Generators;
using Xunit;

namespace Bing.MockData.Tests.Regression
{
    public class RangeRegressionTests
    {
        // 可控随机源直接覆盖端点，而不是依靠大量抽样碰巧命中。
        private sealed class EndpointRandom : Random
        {
            private readonly bool _upper;
            public EndpointRandom(bool upper) { _upper = upper; }
            public override int Next(int maxValue) => _upper ? maxValue - 1 : 0;
            public override int Next(int minValue, int maxValue) => _upper ? maxValue - 1 : minValue;
            public override void NextBytes(byte[] buffer) => Array.Fill(buffer, _upper ? byte.MaxValue : (byte)0);
        }

        [Fact]
        public void Numeric_endpoints_and_extreme_ranges_are_reachable_without_overflow()
        {
            var low = new EndpointRandom(false);
            var high = new EndpointRandom(true);
            Assert.Equal(byte.MaxValue, RandomValueGenerator.Next(high, (byte)0, byte.MaxValue));
            Assert.Equal(short.MaxValue, RandomValueGenerator.Next(high, short.MinValue, short.MaxValue));
            Assert.Equal(int.MinValue, RandomValueGenerator.Next(low, int.MinValue, int.MaxValue));
            Assert.Equal(int.MaxValue, RandomValueGenerator.Next(high, int.MinValue, int.MaxValue));
            Assert.Equal(long.MaxValue, RandomValueGenerator.Next(high, long.MaxValue - 1, long.MaxValue));
            Assert.Equal(long.MinValue, RandomValueGenerator.Next(low, long.MinValue, long.MinValue + 1));
            Assert.Equal(double.MinValue, RandomValueGenerator.Next(low, double.MinValue, double.MaxValue));
            Assert.Equal(double.MaxValue, RandomValueGenerator.Next(high, double.MinValue, double.MaxValue));
            Assert.Equal(float.MaxValue, RandomValueGenerator.Next(high, float.MinValue, float.MaxValue));
            Assert.False(RandomValueGenerator.Next(low, false, true));
            Assert.True(RandomValueGenerator.Next(high, false, true));
            var random = new Random(71);
            for (var i = 0; i < 1000; i++)
            {
                Assert.True(double.IsFinite(RandomValueGenerator.Next(random, double.MinValue, double.MaxValue)));
                Assert.True(float.IsFinite(RandomValueGenerator.Next(random, float.MinValue, float.MaxValue)));
                _ = RandomValueGenerator.Next(random, long.MinValue, long.MaxValue);
            }
            Assert.Equal(long.MaxValue, new NumberRandomizer<long>(new LongFieldOptions { Min = long.MaxValue, Max = long.MaxValue }).Generate());
            Assert.Throws<ArgumentOutOfRangeException>(() => new NumberRandomizer<int>(new IntFieldOptions { Min = 2, Max = 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NumberRandomizer<double>(new DoubleFieldOptions { Min = double.NaN }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NumberRandomizer<float>(new FloatFieldOptions { Max = float.PositiveInfinity }));
            Assert.Throws<NotSupportedException>(() => new NumberRandomizer<char>(new NumberFieldOptions<char>()));
        }

        [Fact]
        public void Text_supports_space_only_empty_and_both_length_endpoints()
        {
            var spaces = new TextRandomizer(new TextFieldOptions { Min = 3, Max = 3, UseLetter = false, UseNumber = false, UseSpecial = false, UseSpace = true });
            Assert.Equal("   ", spaces.Generate());
            var empty = new TextRandomizer(new TextFieldOptions { Min = 0, Max = 0, UseLetter = false, UseNumber = false, UseSpecial = false, UseSpace = false });
            Assert.Equal("", empty.Generate());
            var digits = new TextRandomizer(new TextFieldOptions { Seed = 9, Min = 1, Max = 2, UseLetter = false, UseNumber = true, UseSpecial = false, UseSpace = false });
            var values = Enumerable.Range(0, 100).Select(_ => digits.Generate()).ToArray();
            Assert.All(values, value => Assert.Matches("^[0-9]{1,2}$", value));
            Assert.Contains(values, value => value.Length == 1);
            Assert.Contains(values, value => value.Length == 2);
            var longText = new RandomStringGenerator(new EndpointRandom(true), true, false, false, false, false);
            Assert.Equal(new string('Z', 70000), longText.Generate(1, 70000, TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentException>(() => new TextRandomizer(new TextFieldOptions { UseLetter = false, UseNumber = false, UseSpecial = false, UseSpace = false }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextRandomizer(new TextFieldOptions { Min = -1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextWordsRandomizer(new TextWordsFieldOptions { Min = 5, Max = 4 }));
        }

        [Fact]
        public void Date_and_time_bounds_keep_kind_and_only_return_aligned_values_inside_the_interval()
        {
            var maximum = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
            Assert.Equal(maximum, new DateTimeRandomizer(new DateTimeFieldOptions { From = maximum, To = maximum }).Generate());
            var day = new DateTime(2024, 2, 28, 0, 0, 0, DateTimeKind.Utc);
            var dates = new DateTimeRandomizer(new DateTimeFieldOptions { Seed = 3, From = day.AddHours(12), To = day.AddDays(2).AddHours(12), IncludeTime = false });
            for (var i = 0; i < 20; i++)
            {
                var value = dates.Generate();
                Assert.Equal(DateTimeKind.Utc, value.Kind);
                Assert.Contains(value, new[] { day.AddDays(1), day.AddDays(2) });
            }
            Assert.Throws<ArgumentException>(() => new DateTimeRandomizer(new DateTimeFieldOptions { From = day.AddHours(1), To = day.AddHours(2), IncludeTime = false }));
            Assert.Throws<ArgumentException>(() => new DateTimeRandomizer(new DateTimeFieldOptions { From = day, To = DateTime.SpecifyKind(day, DateTimeKind.Local) }));
            Assert.Equal(TimeSpan.MinValue, new TimeSpanRandomizer(new TimeSpanFieldOptions { From = TimeSpan.MinValue, To = TimeSpan.MinValue }).Generate());
            Assert.Equal(TimeSpan.MaxValue, new TimeSpanRandomizer(new TimeSpanFieldOptions { From = TimeSpan.MaxValue, To = TimeSpan.MaxValue }).Generate());
            var spans = new TimeSpanRandomizer(new TimeSpanFieldOptions { Seed = 9, From = TimeSpan.FromMilliseconds(-1999), To = TimeSpan.FromMilliseconds(-1), IncludeMilliseconds = false });
            Assert.Equal(TimeSpan.FromSeconds(-1), spans.Generate());
            Assert.Throws<ArgumentException>(() => new TimeSpanRandomizer(new TimeSpanFieldOptions { From = TimeSpan.FromTicks(1), To = TimeSpan.FromTicks(2), IncludeMilliseconds = false }));
            Assert.Equal(-1, TickRange.Ceiling(-1999, 1000));
            Assert.Equal(-2, TickRange.Floor(-1999, 1000));
            Assert.Equal(1, TickRange.Ceiling(1, 1000));
            Assert.Equal(new DateTime(2024, 12, 31), new RandomGenerator(new EndpointRandom(true)).GenerateDate(2024, 2024));
        }

        [Fact]
        public void Addresses_use_full_numeric_intervals_and_parse_hex_and_compressed_forms()
        {
            var ipv4 = new IPv4AddressRandomizer(new IPv4AddressFieldOptions { Seed = 16, Min = "10.0.0.255", Max = "10.0.1.0" });
            var v4 = Enumerable.Range(0, 40).Select(_ => ipv4.Generate()).Distinct().OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "10.0.0.255", "10.0.1.0" }, v4);
            Assert.Equal("10.0.0.1", new IPv4AddressRandomizer(new IPv4AddressFieldOptions { Min = "010.0.0.1", Max = "010.0.0.1" }).Generate());
            var ipv6 = new IPv6AddressRandomizer(new IPv6AddressFieldOptions { Seed = 16, Min = "::ffff", Max = "::1:0" });
            var v6 = Enumerable.Range(0, 40).Select(_ => IPAddress.Parse(ipv6.Generate())).Distinct().ToArray();
            Assert.Contains(IPAddress.Parse("::ffff"), v6);
            Assert.Contains(IPAddress.Parse("::1:0"), v6);
            Assert.Equal("::1", new IPv6AddressRandomizer(new IPv6AddressFieldOptions { Min = "::1", Max = "::1" }).Generate());
            Assert.Equal("aa-bb-cc-dd-ee-ff", new MACAddressRandomizer(new MACAddressFieldOptions { Min = "AA:BB:CC:DD:EE:FF", Max = "AABBCCDDEEFF", Separator = "-", Uppercase = false }).Generate());
            var mac = new MACAddressRandomizer(new MACAddressFieldOptions { Seed = 16, Min = "00000000FFFF", Max = "000000010000", Separator = "" });
            Assert.Equal(new[] { "00000000FFFF", "000000010000" }, Enumerable.Range(0, 40).Select(_ => mac.Generate()).Distinct().OrderBy(x => x));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IPv4AddressRandomizer(new IPv4AddressFieldOptions { Min = "10.0.1.0", Max = "10.0.0.255" }));
            Assert.Throws<ArgumentException>(() => new IPv4AddressRandomizer(new IPv4AddressFieldOptions { Min = "127.1" }));
            Assert.Throws<ArgumentException>(() => new IPv4AddressRandomizer(new IPv4AddressFieldOptions { Min = "::1" }));
            Assert.Throws<ArgumentException>(() => new IPv6AddressRandomizer(new IPv6AddressFieldOptions { Min = "fe80::1%2" }));
            Assert.Throws<ArgumentException>(() => new MACAddressRandomizer(new MACAddressFieldOptions { Min = "GG:00:00:00:00:00" }));
        }
    }
}
