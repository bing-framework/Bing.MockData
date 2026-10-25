#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Schema.Tests;

public class GenerationGapTests
{
    [Fact]
    public void Dynamic_reference_to_plain_anchor_does_not_use_matching_dynamic_scope()
    {
        const string root="{\"$id\":\"https://example.com/root\",\"$dynamicAnchor\":\"node\",\"type\":\"object\",\"properties\":{\"child\":{\"$dynamicRef\":\"https://example.com/other#node\"}},\"required\":[\"child\"]}";
        using var schema=CompiledSchema.Compile(root,new Dictionary<string,string>{{"https://example.com/other","{\"$id\":\"https://example.com/other\",\"$anchor\":\"node\",\"const\":7}"}});
        using var c=new MockContext(new MockOptions{Seed=9});
        foreach(var sample in schema.Generator(c).Enumerate(5)){Assert.True(schema.Validate(sample.Json));Assert.Equal(7,(int)JObject.Parse(sample.Json)["child"]!);}
    }

    [Theory]
    [InlineData("uuid")]
    [InlineData("date-time")]
    [InlineData("email")]
    [InlineData("uri")]
    [InlineData("ipv6")]
    [InlineData("date")]
    [InlineData("time")]
    [InlineData("duration")]
    [InlineData("hostname")]
    [InlineData("json-pointer")]
    public void Format_generation_has_no_implicit_sixteen_character_limit(string format)
    {
        using var schema=CompiledSchema.Compile(new JObject { ["type"]="string",["format"]=format }.ToString(),options:new SchemaOptions{AssertFormat=true});
        using var context=new MockContext(new MockOptions{Seed=91});
        for(int i=0;i<5;i++)Assert.True(schema.Validate(schema.Generate(context.ForRecord(i)).Json));
    }

    [Theory]
    [InlineData("{\"allOf\":[{\"type\":\"string\"},{\"pattern\":\"^[A-Z]{40}$\"}]}")]
    [InlineData("{\"allOf\":[{\"type\":\"string\",\"minLength\":40},{\"maxLength\":50}]}")]
    [InlineData("{\"type\":\"array\",\"contains\":{\"const\":1},\"minContains\":5}")]
    [InlineData("{\"type\":\"array\",\"prefixItems\":[{\"const\":0}],\"contains\":{\"const\":1},\"minItems\":2,\"maxItems\":2,\"uniqueItems\":true}")]
    [InlineData("{\"type\":\"array\",\"minItems\":8,\"maxItems\":8,\"items\":{\"enum\":[0,1,2,3,4,5,6,7]},\"uniqueItems\":true}")]
    [InlineData("{\"type\":\"array\",\"prefixItems\":[{\"const\":0},{\"const\":1},{\"const\":2},{\"const\":3},{\"const\":4}],\"items\":false,\"minItems\":5}")]
    [InlineData("{\"type\":\"string\",\"pattern\":\"^(😀){4}$\",\"minLength\":4,\"maxLength\":4}")]
    [InlineData("{\"type\":\"string\",\"format\":\"uuid\",\"maxLength\":3}")]
    public void Combined_constraints_generate_valid_samples(string json)
    {
        using var schema=CompiledSchema.Compile(json,options:new SchemaOptions{AssertFormat=false});
        using var context=new MockContext(new MockOptions{Seed=91});
        var generator=schema.Generator(context);
        var serial=generator.Enumerate(10).Select(x=>x.Json).ToArray();
        Assert.All(serial,value=>Assert.True(schema.Validate(value)));
        Assert.Equal(serial,generator.EnumerateParallel(10,4).Select(x=>x.Json));
        Assert.Equal(serial[7],generator.At(7).Json);
    }

    [Theory]
    [InlineData("{\"type\":\"string\",\"minLength\":3,\"maxLength\":1}")]
    [InlineData("{\"const\":1,\"not\":{\"const\":1}}")]
    public void Nested_failure_reports_schema_and_instance_pointers(string child)
    {
        var json=new JObject { ["type"]="object",["properties"]=new JObject{["a/b"]=JToken.Parse(child)},["required"]=new JArray("a/b") }.ToString();
        using var schema=CompiledSchema.Compile(json);
        using var context=new MockContext(new MockOptions{Seed=1});
        var failure=Assert.Throws<SchemaGenerationException>(()=>schema.Generate(context.ForRecord(23)));
        Assert.Equal(SchemaFailure.BudgetExhausted,failure.Failure);
        Assert.Contains("a~1b",failure.SchemaLocation);
        Assert.EndsWith("/a~1b",failure.InstanceLocation);
        Assert.Equal(23,failure.Replay.RecordIndex);
        Assert.Equal(schema.Identity,failure.SchemaIdentity);
    }

    [Fact]
    public void Explicit_limits_and_cancellation_remain_enforced()
    {
        using var context=new MockContext(new MockOptions{Seed=1,Limits=new MockLimits{MaxCollectionSize=4}});
        using var schema=CompiledSchema.Compile("{\"type\":\"array\",\"contains\":true,\"minContains\":5}");
        Assert.Equal(SchemaFailure.BudgetExhausted,Assert.Throws<SchemaGenerationException>(()=>schema.Generate(context)).Failure);
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>schema.Generator(context).At(0,cancelled.Token));
    }
}
