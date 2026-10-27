using System.ComponentModel.DataAnnotations;
using Bing.MockData;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public sealed class AutoBuilderOrderAndBudgetTests
{
    public sealed class OrderedRules
    {
        [Required]
        public string AutoValue { get; set; }

        public string AlphaDerived { get; set; }
        public string BetaSource { get; set; }
        public string Confirmation { get; set; }
        public string LateValue { get; set; }
    }

    public sealed class CompareChain
    {
        [Compare(nameof(B))]
        public string A { get; set; }

        [Compare(nameof(Z))]
        public string B { get; set; }

        public string Z { get; set; }
    }

    [Fact]
    public void Automatic_fields_precede_explicit_rules_and_explicit_rules_keep_registration_order()
    {
        using var context = new MockContext(new MockOptions { Seed = 91 });
        var value = context.AutoBuilder<OrderedRules>()
            .RuleFor(x => x.BetaSource, _ => "ready")
            .RuleFor(x => x.AlphaDerived, (_, x) => x.AutoValue + "|" + x.BetaSource)
            .RuleFor(x => x.Confirmation, (_, x) => x.BetaSource)
            .Build()
            .At(4, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(value.AutoValue));
        Assert.Equal(value.AutoValue + "|ready", value.AlphaDerived);
        Assert.Equal("ready", value.Confirmation);
    }

    [Fact]
    public void Compare_chains_run_after_their_source_fields_are_generated()
    {
        using var context = new MockContext(new MockOptions { Seed = 92 });
        var value = context.AutoBuilder<CompareChain>()
            .RuleFor(x => x.Z, _ => "source")
            .Build()
            .At(8, TestContext.Current.CancellationToken);

        Assert.Equal("source", value.Z);
        Assert.Equal(value.Z, value.B);
        Assert.Equal(value.B, value.A);
    }

    [Fact]
    public void Built_generator_keeps_the_configuration_snapshot_created_by_build()
    {
        using var context = new MockContext(new MockOptions { Seed = 93 });
        var builder = context.AutoBuilder<OrderedRules>()
            .RuleFor(x => x.BetaSource, _ => "ready")
            .RuleFor(x => x.AlphaDerived, (_, x) => x.AutoValue + "|" + x.BetaSource);
        var built = builder.Build();
        var before = built.At(11, TestContext.Current.CancellationToken);

        builder.RuleFor(x => x.LateValue, _ => "configured later");

        var after = built.At(11, TestContext.Current.CancellationToken);
        Assert.Equal(before.LateValue, after.LateValue);
        Assert.Equal("configured later", builder.Build().At(11, TestContext.Current.CancellationToken).LateValue);
    }

    [Fact]
    public void Company_and_color_sample_lists_enforce_text_budget_for_each_value()
    {
        using var context = new MockContext(new MockOptions
        {
            Seed = 94,
            Limits = new MockLimits { MaxTextLength = 1 }
        });

        Assert.Throws<MockLimitException>(() => context.Companies.Industries());
        Assert.Throws<MockLimitException>(() => context.Companies.Departments("信息技术"));
        Assert.Throws<MockLimitException>(() => context.Colors.Palette());
    }
}
