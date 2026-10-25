using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Bing.MockData;
using Bing.MockData.Internals.Generators;
using Xunit;

namespace Bing.MockData.Tests.Capabilities
{
    public class BoundedRegexTests
    {
        [Theory]
        [InlineData(@"^[0-9]{4}[A-Z]{2}$")]
        [InlineData(@"\A(?:[a-c]|XY){2,4}\z")]
        [InlineData(@"^(?:[A-Z]\.\d{2}|[d-f]??x{2,4}?)$")]
        [InlineData(@"\A\w\d\s\cA\x41\u0042\z")]
        public void Generated_values_match_supported_patterns(string pattern)
        {
            var generator = new BoundedRegexGenerator(pattern, 32);
            var regex = new Regex(pattern);

            for (var index = 0; index < 64; index++)
            {
                var value = generator.Generate(new Random(index), CancellationToken.None);
                Assert.True(regex.IsMatch(value), $"Generated value did not match '{pattern}'.");
                Assert.True(value.Length <= 32);
            }
        }

        [Fact]
        public void Escape_semantics_and_step_budget_match_the_supported_contract()
        {
            using var mock = new MockContext(new MockOptions { Seed = 4, Limits = new MockLimits { MaxTextLength = 20 } });
            Assert.Equal(" ac", mock.Text.Regex(@"\A\x20ac\z"));
            Assert.Equal("😀😀", mock.Text.Regex("^(?:😀){2}$"));
            Assert.Throws<ArgumentException>(() => mock.Text.Regex("^😀{2}$"));
            var tooManySteps = "(?:" + string.Concat(Enumerable.Repeat("(?:)", 1100)) + "a)";
            Assert.Throws<MockLimitException>(() => mock.Text.Regex(tooManySteps));
        }

        [Fact]
        public void Bounded_quantifiers_include_both_endpoints_and_obey_text_budget()
        {
            var generator = new BoundedRegexGenerator("a{0,4}", 4);
            Assert.Equal("aaaa", generator.Generate(new MaximumValueRandom(), CancellationToken.None));
            Assert.Throws<MockLimitException>(() => new BoundedRegexGenerator("a{0,4}", 3));
        }

        [Theory]
        [InlineData("a*")]
        [InlineData("a+")]
        [InlineData("a{2,}")]
        [InlineData("(?=a)a")]
        [InlineData("(?<name>a)")]
        [InlineData("(?i)a")]
        [InlineData(@"\bword")]
        [InlineData(@"(a)\1")]
        [InlineData("(^a)")]
        public void Unbounded_or_unsupported_constructs_are_rejected(string pattern)
        {
            Assert.Throws<ArgumentException>(() => new BoundedRegexGenerator(pattern, 100));
        }

        [Fact]
        public void Quantifier_integer_overflow_is_rejected()
        {
            Assert.Throws<ArgumentException>(() => new BoundedRegexGenerator("a{2147483648}", int.MaxValue));
        }

        [Fact]
        public void Negated_character_classes_never_emit_isolated_surrogates()
        {
            var generator = new BoundedRegexGenerator("[^a]", 1);
            for (var index = 0; index < 128; index++)
            {
                var value = generator.Generate(new Random(index), CancellationToken.None);
                Assert.Equal(1, value.Length);
                Assert.False(char.IsSurrogate(value[0]));
            }

            Assert.Throws<ArgumentException>(() => new BoundedRegexGenerator(@"[\uD800-\uDFFF]", 1));
        }

        [Fact]
        public void Cancellation_is_checked_inside_repeat_generation()
        {
            using (var source = new CancellationTokenSource())
            {
                var generator = new BoundedRegexGenerator("a{2,4}", 4);
                var random = new CancellingRandom(source);
                Assert.Throws<OperationCanceledException>(() => generator.Generate(random, source.Token));
            }
        }

        private sealed class MaximumValueRandom : Random
        {
            public override int Next(int maxValue) => maxValue - 1;
            public override int Next(int minValue, int maxValue) => maxValue - 1;
        }

        private sealed class CancellingRandom : Random
        {
            private readonly CancellationTokenSource _source;

            public CancellingRandom(CancellationTokenSource source) { _source = source; }

            public override int Next(int minValue, int maxValue)
            {
                _source.Cancel();
                return minValue;
            }
        }
    }
}
