using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bing.MockData;
using Bing.MockData.Abstractions.Options;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Regression
{
    public class StateRegressionTests
    {
        private static FieldOptionsBase[] Configurations() => new FieldOptionsBase[]
        {
            new BooleanFieldOptions(), new ByteFieldOptions(), new ShortFieldOptions(), new IntFieldOptions(),
            new LongFieldOptions(), new FloatFieldOptions(), new DoubleFieldOptions(),
            new NumberFieldOptions<DateTime> { Min = new DateTime(2020, 1, 1), Max = new DateTime(2025, 1, 1) },
            new NumberFieldOptions<TimeSpan> { Min = TimeSpan.MinValue, Max = TimeSpan.MaxValue },
            new TextFieldOptions(), new TextRegexFieldOptions { Pattern = "^[A-Z]{4}[0-9]{4}$" },
            new TextWordsFieldOptions(), new TextLipsumFieldOptions(),
            new StringListFieldOptions { Values = new List<string> { "alpha", "Beta", "gamma" } },
            new FirstNameFieldOptions(), new LastNameFieldOptions(), new FullNameFieldOptions(),
            new EmailAddressFieldOptions(), new CityFieldOptions(), new CountryFieldOptions(),
            new ChineseNameFieldOptions(), new IBANFieldOptions(), new IPv4AddressFieldOptions(),
            new IPv6AddressFieldOptions(), new MACAddressFieldOptions(), new GuidFieldOptions(),
            new DateTimeFieldOptions { From = new DateTime(2020, 1, 1), To = new DateTime(2025, 1, 1) },
            new TimeSpanFieldOptions(), new MobileFieldOptions(), new ChineseIdCardFieldOptions(),
            new ChineseAddressFieldOptions(), new BankCardFieldOptions()
        };

        private static object Create(FieldOptionsBase options) => RandomizerFactory.Create(options);
        private static object Next(object generator) => generator.GetType().GetMethod("Generate", Type.EmptyTypes).Invoke(generator, null);

        [Fact]
        public void All_builtin_generators_replay_seeded_sequences_without_cross_instance_interference()
        {
            foreach (var options in Configurations())
            {
                options.Seed = 12345;
                var first = Create(options);
                var second = Create(options);
                var noise = Create(options);
                Assert.NotSame(first, second);
                for (var i = 0; i < 20; i++)
                {
                    var expected = Next(first);
                    _ = Next(noise);
                    _ = Next(noise);
                    Assert.Equal(expected, Next(second));
                }
            }
        }

        [Fact]
        public void Shared_instances_serialize_complete_generation()
        {
            // 请求并行度为 4；实例锁内实际生成并行度为 1，比较无序输出集合。
            foreach (var options in Configurations())
            {
                options.Seed = 2468;
                var sequential = Create(options);
                var concurrent = Create(options);
                var expected = Enumerable.Range(0, 64).Select(_ => Next(sequential).ToString()).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var actual = new string[64];
                Parallel.For(0, actual.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i => actual[i] = Next(concurrent).ToString());
                Assert.Equal(expected, actual.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            }
        }

        [Fact]
        public void String_casing_extensions_preserve_seeded_streams()
        {
            foreach (var options in Configurations())
            {
                options.Seed = 17;
                var generator = Create(options);
                if (generator is IStringRandomizer)
                {
                    var plain = (IStringRandomizer)Create(options);
                    var upper = (IStringRandomizer)Create(options);
                    var lower = (IStringRandomizer)Create(options);
                    for (var i = 0; i < 10; i++)
                    {
                        var value = plain.Generate();
                        Assert.Equal(value.ToUpperInvariant(), upper.Generate(true));
                        Assert.Equal(value.ToLowerInvariant(), lower.Generate(false));
                    }
                }
            }
        }

        [Fact]
        public void Nullable_wrappers_control_null_probability_for_values_and_strings()
        {
            var number = RandomizerFactory.GetRandomizer<int>(new IntFieldOptions { Seed = 17, Min = 5, Max = 5 });
            Assert.Null(number.Nullable(1).Generate());
            Assert.Equal(5, number.Nullable(0).Generate());

            var text = RandomizerFactory.GetRandomizer((IStringFieldOptions)new TextFieldOptions { Seed = 17, Min = 4, Max = 4 });
            Assert.Null(text.OrNull(1).Generate());
            Assert.NotNull(text.OrNull(0).Generate());
        }

        [Fact]
        public void Constructors_snapshot_scalars_lists_and_arrays()
        {
            var textOptions = new TextFieldOptions { Min = 3, Max = 3, Seed = 2 };
            var text = new TextRandomizer(textOptions);
            textOptions.Min = textOptions.Max = 0;
            Assert.Equal(3, text.Generate().Length);
            var listOptions = new StringListFieldOptions { Values = new List<string> { "original" } };
            var list = new StringListRandomizer(listOptions);
            listOptions.Values[0] = "changed";
            listOptions.Values.Clear();
            Assert.Equal("original", list.Generate());
            var mobileOptions = new MobileFieldOptions { MobilePrefixs = new[] { 123 }, Length = 3 };
            var mobile = new MobileRandomizer(mobileOptions);
            mobileOptions.MobilePrefixs[0] = 999;
            mobileOptions.Length = 2;
            Assert.Equal("123", mobile.Generate());
            var dateOptions = new DateTimeFieldOptions { From = new DateTime(2020, 1, 1), To = new DateTime(2020, 1, 1), Format = "yyyy" };
            var date = new DateTimeRandomizer(dateOptions);
            dateOptions.Format = "invalid";
            dateOptions.To = DateTime.MinValue;
            Assert.Equal("2020", date.GenerateAsString());
        }

        [Fact]
        public void Typed_factories_work_and_unknown_configuration_fails_clearly()
        {
            Assert.IsType<NumberRandomizer<int>>(RandomizerFactory.GetRandomizer<int>(new IntFieldOptions()));
            Assert.IsType<TextRandomizer>(RandomizerFactory.GetRandomizer((IStringFieldOptions)new TextFieldOptions()));
            Assert.IsType<GuidRandomizer>(RandomizerFactory.GetRandomizer(new GuidFieldOptions()));
            Assert.IsType<DateTimeRandomizer>(RandomizerFactory.GetRandomizer(new DateTimeFieldOptions()));
            Assert.IsType<TimeSpanRandomizer>(RandomizerFactory.GetRandomizer(new TimeSpanFieldOptions()));
            Assert.IsType<MobileRandomizer>(RandomizerFactory.GetRandomizer(new MobileFieldOptions()));
            Assert.IsType<ChineseIdCardRandomizer>(RandomizerFactory.GetRandomizer(new ChineseIdCardFieldOptions()));
            Assert.IsType<ChineseAddressRandomizer>(RandomizerFactory.GetRandomizer(new ChineseAddressFieldOptions()));
            Assert.IsType<BankCardRandomizer>(RandomizerFactory.GetRandomizer(new BankCardFieldOptions()));
            Assert.Throws<ArgumentNullException>(() => RandomizerFactory.Create(null));
            Assert.Throws<NotSupportedException>(() => RandomizerFactory.Create(new object()));
            Assert.Throws<NotSupportedException>(() => RandomizerFactory.GetRandomizer(new UnknownStringOptions()));
            Assert.Throws<ArgumentNullException>(() => new TextRandomizer(null));
        }

        [Fact]
        public void Factory_has_no_hash_collision_aliasing_or_lifetime_retention()
        {
            var a = new CollidingTextOptions { Min = 2, Max = 2 };
            var b = new CollidingTextOptions { Min = 5, Max = 5 };
            Assert.Equal(2, RandomizerFactory.GetRandomizer(a).Generate().Length);
            Assert.Equal(5, RandomizerFactory.GetRandomizer(b).Generate().Length);
            var references = CreateWeakReferences();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Assert.All(references, reference => Assert.False(reference.IsAlive));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] CreateWeakReferences()
        {
            var options = new TextFieldOptions();
            var generator = RandomizerFactory.GetRandomizer(options);
            return new[] { new WeakReference(options), new WeakReference(generator) };
        }

        private sealed class CollidingTextOptions : TextFieldOptions { public override int GetHashCode() => 1; }
        private sealed class UnknownStringOptions : IStringFieldOptions { }

        [Fact]
        public void Live_text_generators_do_not_retain_previously_generated_strings()
        {
            var generator = new TextRandomizer(new TextFieldOptions { Seed = 7, Min = 128, Max = 128 });
            var references = GenerateWeakStrings(generator);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Assert.All(references, reference => Assert.False(reference.IsAlive));
            GC.KeepAlive(generator);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] GenerateWeakStrings(TextRandomizer generator) =>
            Enumerable.Range(0, 100).Select(_ => new WeakReference(generator.Generate())).ToArray();

        [Fact]
        public void Name_and_email_order_options_reverse_the_same_parts()
        {
            var forward = new FullNameRandomizer(new FullNameFieldOptions { Seed = 18, Left2Right = true });
            var reverse = new FullNameRandomizer(new FullNameFieldOptions { Seed = 18, Left2Right = false });
            Assert.Equal(string.Join(" ", forward.Generate().Split(' ').Reverse()), reverse.Generate());
            var forwardEmail = new EmailAddressRandomizer(new EmailAddressFieldOptions { Seed = 18, Left2Right = true }).Generate().Split('@');
            var reverseEmail = new EmailAddressRandomizer(new EmailAddressFieldOptions { Seed = 18, Left2Right = false }).Generate().Split('@');
            Assert.Equal(string.Join(".", forwardEmail[0].Split('.').Reverse()), reverseEmail[0]);
            Assert.Equal(forwardEmail[1], reverseEmail[1]);
        }

        [Fact]
        public void Seeded_guids_keep_version_and_variant_bits()
        {
            var generator = new GuidRandomizer(new GuidFieldOptions { Seed = 31 });
            for (var i = 0; i < 20; i++)
            {
                var bytes = generator.Generate().ToByteArray();
                Assert.Equal(4, bytes[7] >> 4);
                Assert.Equal(2, bytes[8] >> 6);
            }
        }
    }
}
