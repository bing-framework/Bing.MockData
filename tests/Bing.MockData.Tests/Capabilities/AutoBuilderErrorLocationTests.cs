using System;
using Bing.MockData;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public sealed class AutoBuilderErrorLocationTests
{
    public sealed class RuleValue
    {
        public string Name { get; set; }
    }

    public sealed class SetterValue
    {
        public static readonly InvalidOperationException Failure = new InvalidOperationException("setter failed");
        public string Name { get => string.Empty; set => throw Failure; }
    }

    public sealed class NestedValue
    {
        public string Name { get; set; }
    }

    public sealed class ParentValue
    {
        public NestedValue Child { get; set; }
    }

    [Fact]
    public void RuleFunctionFailureIncludesFieldRecordAndAttemptPath()
    {
        using var context = new MockContext(new MockOptions { Seed = 42 });
        var cause = new InvalidOperationException("rule failed");
        var generator = context.AutoBuilder<RuleValue>()
            .RuleFor(x => x.Name, _ => throw cause)
            .Build();

        var failure = Assert.Throws<MockGenerationException>(() => generator.At(42, TestContext.Current.CancellationToken));

        Assert.Equal("attempt[0].Name", failure.FieldPath);
        Assert.Equal(42, failure.RecordIndex);
        Assert.Equal("DataAnnotations:RuleValue.Name", failure.RuleId);
        Assert.Same(cause, failure.InnerException);
    }

    [Fact]
    public void PropertySetterFailureIncludesFieldRecordAndAttemptPath()
    {
        using var context = new MockContext(new MockOptions { Seed = 42 });
        var generator = context.AutoBuilder<SetterValue>()
            .RuleFor(x => x.Name, _ => "value")
            .Build();

        var failure = Assert.Throws<MockGenerationException>(() => generator.At(42, TestContext.Current.CancellationToken));

        Assert.Equal("attempt[0].Name", failure.FieldPath);
        Assert.Equal(42, failure.RecordIndex);
        Assert.Equal("DataAnnotations:SetterValue.Name", failure.RuleId);
        Assert.Same(SetterValue.Failure, failure.InnerException);
    }

    [Fact]
    public void CancellationBudgetAndExistingGenerationExceptionsPassThroughUnchanged()
    {
        using var context = new MockContext(new MockOptions { Seed = 42 });
        var canceled = new OperationCanceledException("cancel");
        var limit = new MockLimitException("explicit-rule", 1);
        var existing = new MockGenerationException("existing", context.ForRecord(7, TestContext.Current.CancellationToken).ForField("Already.Name"),
            new InvalidOperationException("already wrapped"));

        var canceledResult = Assert.Throws<OperationCanceledException>(() => ThrowingGenerator(context, canceled).At(42, TestContext.Current.CancellationToken));
        var limitResult = Assert.Throws<MockLimitException>(() => ThrowingGenerator(context, limit).At(42, TestContext.Current.CancellationToken));
        var existingResult = Assert.Throws<MockGenerationException>(() => ThrowingGenerator(context, existing).At(42, TestContext.Current.CancellationToken));

        Assert.Same(canceled, canceledResult);
        Assert.Same(limit, limitResult);
        Assert.Same(existing, existingResult);
    }

    [Fact]
    public void NestedGenerationFailureRetainsItsOriginalFieldAndRule()
    {
        using var context = new MockContext(new MockOptions { Seed = 42 });
        var cause = new InvalidOperationException("nested rule failed");
        var child = context.AutoBuilder<NestedValue>()
            .RuleFor(x => x.Name, _ => throw cause)
            .Build();
        var parent = context.AutoBuilder<ParentValue>()
            .RuleFor(x => x.Child, field => child.Evaluate(field))
            .Build();

        var failure = Assert.Throws<MockGenerationException>(() => parent.At(42, TestContext.Current.CancellationToken));

        Assert.Equal("attempt[0].Child.attempt[0].Name", failure.FieldPath);
        Assert.Equal(42, failure.RecordIndex);
        Assert.Equal("DataAnnotations:NestedValue.Name", failure.RuleId);
        Assert.Same(cause, failure.InnerException);
    }

    private static MockGenerator<RuleValue> ThrowingGenerator(MockContext context, Exception error) =>
        context.AutoBuilder<RuleValue>()
            .RuleFor(x => x.Name, _ => throw error)
            .Build();
}
