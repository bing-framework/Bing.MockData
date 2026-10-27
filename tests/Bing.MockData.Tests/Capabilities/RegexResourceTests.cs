using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using Bing.MockData;
using Bing.MockData.Internals.Generators;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public sealed class RegexResourceTests
{
    private static readonly AsyncLocal<CancellationTokenSource> InspectionCancellation = new AsyncLocal<CancellationTokenSource>();

    public sealed class CancelDuringInspectionAttribute : ValidationAttribute
    {
        public CancelDuringInspectionAttribute() { InspectionCancellation.Value?.Cancel(); }
        public override bool IsValid(object value) => true;
    }

    public sealed class CancelledPlan
    {
        [CancelDuringInspection, RegularExpression("(?")]
        public string Value { get; set; }
    }

    [Fact]
    public void Auto_builder_stops_when_cancelled_during_attribute_inspection_before_regex_parsing()
    {
        using var context = new MockContext();
        using var source = new CancellationTokenSource();
        InspectionCancellation.Value = source;
        try
        {
            // Cancellation happens after Build's entry check, while reading the attributes.
            // Parsing the intentionally invalid regex instead would throw ArgumentException.
            Assert.Throws<OperationCanceledException>(() => context.ForRecord(0, source.Token).AutoBuilder<CancelledPlan>().Build());
        }
        finally { InspectionCancellation.Value = null; }
    }

    [Fact]
    public void Repeated_and_distinct_negated_classes_keep_compact_interval_sets()
    {
        var repeated = string.Concat(Enumerable.Repeat("(?:[^a]){0}", 128));
        var repeatedGenerator = new BoundedRegexGenerator(repeated, 100000);
        Assert.Equal(string.Empty, repeatedGenerator.Generate(new Random(1), CancellationToken.None));
        Assert.Equal(128L * 3, repeatedGenerator.CharacterIntervalCount);

        var distinct = string.Concat(Enumerable.Range(0, 26).Select(index => "[^" + (char)('A' + index) + "]"));
        var distinctGenerator = new BoundedRegexGenerator(distinct, 100000);
        Assert.InRange(distinctGenerator.CharacterIntervalCount, 1, 26 * 3);
        var value = distinctGenerator.Generate(new Random(2), CancellationToken.None);

        Assert.Equal(26, value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            Assert.NotEqual((char)('A' + index), value[index]);
            Assert.False(char.IsSurrogate(value[index]));
        }
    }

    [Fact]
    public void Character_class_random_indices_follow_ascending_range_order()
    {
        var generator = new BoundedRegexGenerator("[A-Cx-z]", 1);

        Assert.Equal("A", generator.Generate(new IndexRandom(0), CancellationToken.None));
        Assert.Equal("B", generator.Generate(new IndexRandom(1), CancellationToken.None));
        Assert.Equal("C", generator.Generate(new IndexRandom(2), CancellationToken.None));
        Assert.Equal("x", generator.Generate(new IndexRandom(3), CancellationToken.None));
        Assert.Equal("y", generator.Generate(new IndexRandom(4), CancellationToken.None));
        Assert.Equal("z", generator.Generate(new IndexRandom(5), CancellationToken.None));
    }

    [Fact]
    public void Zero_repeat_is_elided_after_its_body_is_validated()
    {
        Assert.Equal(string.Empty, new BoundedRegexGenerator("(?:[^a]){0}", 1).Generate(new Random(3), CancellationToken.None));
        Assert.Throws<ArgumentException>(() => new BoundedRegexGenerator("(?:a*){0}", 1));
    }

    [Fact]
    public void Parse_budget_and_cancellation_are_checked_before_regex_validation()
    {
        Assert.Throws<MockLimitException>(() => new BoundedRegexGenerator(new string('a', 1025), 64));

        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => new BoundedRegexGenerator("a", 32, cancellationToken: source.Token));
    }

    [Fact]
    public void Code_point_length_can_generate_supplementary_literals_within_utf16_budget()
    {
        using var context = new MockContext(new MockOptions
        {
            Seed = 95,
            Limits = new MockLimits { MaxTextLength = 4 }
        });

        Assert.Equal("😀😀", context.Text.RegexCodePoints("(?:😀){2}", 2, 2));
    }

    [Fact]
    public void Code_point_length_filters_alternatives_and_counts_mixed_literals()
    {
        var supplementaryAlternatives = new BoundedRegexGenerator("(?:😀|🙂)", 2, constrainOutput: true);
        var mixedAlternatives = new BoundedRegexGenerator("(?:😀|A)", 2, constrainOutput: true);

        var firstSupplementary = supplementaryAlternatives.Generate(new IndexRandom(0), CancellationToken.None, 1, 1, 4, codePoints: true);
        var secondSupplementary = supplementaryAlternatives.Generate(new IndexRandom(1), CancellationToken.None, 1, 1, 4, codePoints: true);
        var supplementary = mixedAlternatives.Generate(new IndexRandom(0), CancellationToken.None, 1, 1, 4, codePoints: true);
        var ascii = mixedAlternatives.Generate(new IndexRandom(1), CancellationToken.None, 1, 1, 4, codePoints: true);

        Assert.Equal("😀", firstSupplementary);
        Assert.Equal("🙂", secondSupplementary);
        Assert.Equal("😀", supplementary);
        Assert.Equal("A", ascii);
    }

    private sealed class IndexRandom : Random
    {
        private readonly int _index;
        public IndexRandom(int index) { _index = index; }
        public override int Next(int maxValue) => _index;
    }
}
