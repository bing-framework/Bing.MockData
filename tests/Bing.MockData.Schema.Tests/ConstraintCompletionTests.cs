#pragma warning disable xUnit1051
using System;
using System.Linq;
using System.Threading;
using Bing.MockData.Schema;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Schema.Tests;

public class ConstraintCompletionTests
{
    [Theory]
    [InlineData("email", 8)]
    [InlineData("email", 80)]
    [InlineData("email", 254)]
    [InlineData("idn-email", 10)]
    [InlineData("hostname", 70)]
    [InlineData("uri", 4)]
    [InlineData("uri", 60)]
    [InlineData("uri-reference", 0)]
    [InlineData("ipv4", 7)]
    [InlineData("ipv4", 15)]
    [InlineData("ipv6", 2)]
    [InlineData("ipv6", 12)]
    [InlineData("ipv6", 39)]
    [InlineData("date-time", 20)]
    [InlineData("date-time", 32)]
    [InlineData("time", 12)]
    [InlineData("duration", 8)]
    [InlineData("json-pointer", 1)]
    [InlineData("relative-json-pointer", 5)]
    [InlineData("regex", 0)]
    public void Format_and_length_are_constructed_together(string format,int length)
    {
        using var c=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxAttempts=1}});
        using var schema=CompiledSchema.Compile(new JObject{["type"]="string",["format"]=format,["minLength"]=length,["maxLength"]=length}.ToString(),options:new SchemaOptions{AssertFormat=true});
        for(int i=0;i<5;i++){var sample=schema.Generator(c).At(i);Assert.True(schema.Validate(sample.Json));Assert.Equal(length,((string)ExactJson.Parse(sample.Json)).Length);}
    }

    [Theory]
    [InlineData("{\"type\":\"array\",\"minItems\":100,\"maxItems\":100,\"items\":{\"enum\":[0,1]},\"contains\":{\"const\":0},\"minContains\":0,\"maxContains\":0}")]
    [InlineData("{\"type\":\"array\",\"minItems\":30,\"items\":{\"type\":\"boolean\"},\"contains\":{\"const\":true},\"minContains\":4,\"maxContains\":4}")]
    [InlineData("{\"type\":\"array\",\"minItems\":30,\"items\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":10},\"contains\":{\"minimum\":5},\"minContains\":4,\"maxContains\":4}")]
    [InlineData("{\"type\":\"array\",\"prefixItems\":[{\"const\":1},{\"const\":1}],\"minItems\":10,\"items\":{\"enum\":[0,1]},\"contains\":{\"const\":1},\"minContains\":1,\"maxContains\":2}")]
    public void Contains_upper_bounds_are_planned(string source)
    {
        using var schema=CompiledSchema.Compile(source);using var c=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxAttempts=1}});
        var generator=schema.Generator(c);Assert.All(generator.GenerateMany(10),x=>Assert.True(schema.Validate(x.Json)));
        Assert.Equal(generator.Enumerate(20).Select(x=>x.Json),generator.EnumerateParallel(20,4).Select(x=>x.Json));
    }

    [Theory]
    [InlineData("{\"type\":\"integer\",\"minimum\":0,\"maximum\":1000000,\"not\":{\"anyOf\":[{\"maximum\":777776},{\"minimum\":777778}]} }", "777777")]
    [InlineData("{\"type\":\"number\",\"minimum\":0,\"maximum\":1,\"multipleOf\":0.01,\"not\":{\"anyOf\":[{\"exclusiveMaximum\":0.45},{\"exclusiveMinimum\":0.45}]} }", "0.45")]
    [InlineData("{\"enum\":[0,1,2,3],\"not\":{\"enum\":[0,1,2]}}", "3")]
    [InlineData("{\"type\":\"integer\",\"minimum\":0,\"maximum\":1000000,\"not\":{\"not\":{\"const\":777777}}}", "777777")]
    [InlineData("{\"type\":\"integer\",\"not\":{\"maximum\":1000000}}", "1000001")]
    [InlineData("{\"type\":\"number\",\"not\":{\"anyOf\":[{\"exclusiveMaximum\":0.000007},{\"exclusiveMinimum\":0.000007}]}}", "0.000007")]
    public void Negated_finite_sets_and_numeric_ranges_are_solved(string source,string expected)
    {
        using var schema=CompiledSchema.Compile(source);using var c=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxAttempts=1}});
        var sample=schema.Generate(c);Assert.Equal(expected,sample.Json);Assert.True(schema.Validate(sample.Json));
    }

    [Fact] public void Impossible_and_cancelled_search_remain_explicit()
    {
        using var c=new MockContext(new MockOptions{Seed=91});
        using var schema=CompiledSchema.Compile("{\"type\":\"integer\",\"minimum\":0,\"maximum\":1,\"not\":{\"enum\":[0,1]}}");
        Assert.Equal(SchemaFailure.BudgetExhausted,Assert.Throws<SchemaGenerationException>(()=>schema.Generate(c)).Failure);
        using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>schema.Generator(c).At(1,cancel.Token));
    }

    [Fact] public void Format_annotations_do_not_restrict_length()
    {
        using var c=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxAttempts=1}});
        using var annotation=CompiledSchema.Compile("{\"type\":\"string\",\"format\":\"email\",\"minLength\":2,\"maxLength\":2}",options:new SchemaOptions{AssertFormat=false});
        Assert.True(annotation.Validate(annotation.Generate(c).Json));
        using var asserted=CompiledSchema.Compile("{\"type\":\"string\",\"format\":\"email\",\"maxLength\":2}",options:new SchemaOptions{AssertFormat=true});
        Assert.Equal(SchemaFailure.BudgetExhausted,Assert.Throws<SchemaGenerationException>(()=>asserted.Generate(c)).Failure);
    }

    [Fact] public void Email_format_prefers_example_domains_when_length_allows()
    {
        using var c=new MockContext(new MockOptions{Seed=91});
        foreach(var bounds in new[]{"",",\"maxLength\":10",",\"minLength\":80,\"maxLength\":80"})
        {
            using var schema=CompiledSchema.Compile("{\"type\":\"string\",\"format\":\"email\""+bounds+"}",options:new SchemaOptions{AssertFormat=true});
            foreach(var sample in schema.Generator(c).GenerateMany(10))
            {
                var email=(string)ExactJson.Parse(sample.Json);Assert.True(email.EndsWith("@example.com",StringComparison.Ordinal)||email.EndsWith(".test",StringComparison.Ordinal));
                Assert.True(schema.Validate(sample.Json));
            }
        }
    }
}
