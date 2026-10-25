#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Schema.Tests;

public class ObjectConstraintTests
{
    public static IEnumerable<object[]> ObjectSchemas()
    {
        yield return new object[]{"{\"type\":\"object\",\"properties\":{\"\":{\"const\":0},\"a/b~c\":{\"const\":7},\"a.b\":{\"const\":9}},\"required\":[\"\",\"a/b~c\",\"a.b\"],\"additionalProperties\":false}"};
        yield return new object[]{"{\"type\":\"object\",\"properties\":{\"a\":{}},\"required\":[\"a\"],\"dependentSchemas\":{\"a\":{\"const\":{\"a\":1,\"b\":2}}}}"};
        yield return new object[]{"{\"type\":\"object\",\"properties\":{\"a\":{\"const\":1}},\"required\":[\"a\"],\"dependentSchemas\":{\"a\":{\"properties\":{\"b\":{\"const\":2}},\"required\":[\"b\"],\"dependentSchemas\":{\"b\":{\"properties\":{\"c\":{\"const\":3}},\"required\":[\"c\"]}}}},\"unevaluatedProperties\":false}"};
        yield return new object[]{"{\"type\":\"object\",\"properties\":{\"a\":{},\"b\":{},\"c\":{}},\"required\":[\"a\"],\"dependentSchemas\":{\"a\":{\"required\":[\"b\"],\"dependentRequired\":{\"b\":[\"c\"],\"c\":[\"a\"]}}},\"additionalProperties\":false}"};
        yield return new object[]{"{\"type\":\"object\",\"properties\":{\"a\":{},\"b\":{},\"c\":{}},\"minProperties\":3,\"maxProperties\":3,\"additionalProperties\":false}"};
        yield return new object[]{"{\"type\":\"object\",\"required\":[\"a\"],\"allOf\":[{\"properties\":{\"a\":{\"const\":\"text\"}}}],\"unevaluatedProperties\":{\"type\":\"integer\"}}"};
        yield return new object[]{"{\"type\":\"object\",\"properties\":{\"a\":{},\"b\":{}},\"required\":[\"a\"],\"dependentSchemas\":{\"a\":{\"minProperties\":2,\"maxProperties\":2,\"dependentSchemas\":{\"b\":{\"properties\":{\"b\":{\"const\":9}}}}}},\"additionalProperties\":false}"};
        yield return new object[]{"{\"type\":\"object\",\"minProperties\":2,\"maxProperties\":2,\"propertyNames\":{\"enum\":[\"left\",\"right\"]},\"additionalProperties\":{\"type\":\"boolean\"}}"};
        var properties=new JObject{["required"] = new JObject{["const"]=42}};
        for(int i=0;i<80;i++)properties["optional"+i]=new JObject{["type"]="boolean"};
        yield return new object[]{new JObject{["type"]="object",["properties"]=properties,["required"]=new JArray("required"),["maxProperties"]=1,["additionalProperties"]=false}.ToString()};
    }

    [Theory,MemberData(nameof(ObjectSchemas))]
    public void Dependency_closure_and_object_cardinality_construct_valid_samples(string json)
    {
        using var schema=CompiledSchema.Compile(json);using var c=new MockContext(new MockOptions{Seed=408});
        var generator=schema.Generator(c);var serial=generator.Enumerate(12).Select(x=>x.Json).ToArray();
        Assert.All(serial,value=>Assert.True(schema.Validate(value),value));
        Assert.Equal(serial,generator.EnumerateParallel(12,4).Select(x=>x.Json));Assert.Equal(serial[9],generator.At(9).Json);
    }

    [Theory]
    [InlineData("{\"allOf\":[{\"const\":1},{\"enum\":[1.0,2]}]}")]
    [InlineData("{\"allOf\":[{\"const\":{\"n\":1,\"a\":[2,3]}},{\"enum\":[{\"a\":[2.0,3e0],\"n\":1.0}]}]}")]
    [InlineData("{\"allOf\":[{\"enum\":[\"a\",\"b\",\"c\"]},{\"enum\":[\"b\"]}]}")]
    public void Finite_candidates_intersect_with_mathematical_numeric_equality(string json)
    {
        using var schema=CompiledSchema.Compile(json);using var c=new MockContext(new MockOptions{Seed=408});
        foreach(var sample in schema.Generator(c).Enumerate(10))Assert.True(schema.Validate(sample.Json));
    }

    [Fact]
    public void Thousand_declared_properties_fit_the_default_search_budget()
    {
        var properties=new JObject();for(int i=0;i<1000;i++)properties["p"+i]=new JObject{["const"]=i};
        string json=new JObject{["type"]="object",["properties"]=properties,["minProperties"]=1000,["additionalProperties"]=false}.ToString();
        using var schema=CompiledSchema.Compile(json);using var c=new MockContext(new MockOptions{Seed=408,Limits=new MockLimits{MaxAttempts=1}});
        var sample=schema.Generate(c);Assert.Equal(1000,JObject.Parse(sample.Json).Count);Assert.True(schema.Validate(sample.Json));
    }

    [Fact]
    public void Narrow_intersection_of_large_enum_succeeds_with_one_attempt()
    {
        var values=new JArray(Enumerable.Range(0,1000));
        string json=new JObject{["allOf"]=new JArray(new JObject{["enum"]=values},new JObject{["enum"]=new JArray(998)})}.ToString();
        using var schema=CompiledSchema.Compile(json);using var c=new MockContext(new MockOptions{Seed=408,Limits=new MockLimits{MaxAttempts=1}});
        Assert.Equal("998",schema.Generate(c).Json);
    }

    [Theory]
    [InlineData("{\"allOf\":[{\"enum\":[1,2]},{\"enum\":[3,4]}]}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"a\":{}},\"required\":[\"a\"],\"dependentSchemas\":{\"a\":{\"required\":[\"b\"]}},\"maxProperties\":1}")]
    [InlineData("{\"const\":[1,2,3]}")]
    [InlineData("{\"enum\":[{\"nested\":{\"deep\":{\"value\":1}}}]}")]
    public void Conflicts_and_literal_budget_limits_remain_bounded(string json)
    {
        using var schema=CompiledSchema.Compile(json,options:new SchemaOptions{MaxSearchSteps=80});
        using var c=new MockContext(new MockOptions{Seed=408,Limits=new MockLimits{MaxCollectionSize=2,MaxDepth=2}});
        var failure=Assert.Throws<SchemaGenerationException>(()=>schema.Generate(c.ForRecord(19)));
        Assert.Equal(SchemaFailure.BudgetExhausted,failure.Failure);Assert.Equal(19,failure.Replay.RecordIndex);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>schema.Generator(c).At(19,cancellation.Token));
    }

    [Fact]
    public void Schema_identity_changes_when_generator_contract_changes()
    {
        const string json="{\"type\":\"object\"}";
        string legacyIdentity;
        using(var sha=SHA256.Create())legacyIdentity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new{Schema=json,Documents=Array.Empty<KeyValuePair<string,string>>(),Options=new SchemaOptions()})))).Replace("-","").ToLowerInvariant();
        using var schema=CompiledSchema.Compile(json);Assert.NotEqual(legacyIdentity,schema.Identity);
        using var c=new MockContext(new MockOptions{Seed=408}.UseSchema("sample",json));var replay=c.Replay;
        var legacy=new ReplayInfo(replay.Seed,replay.ReferenceTime,replay.Locale,replay.AlgorithmVersion,replay.DataVersion,replay.LibraryVersion,replay.RuntimeVersion,0,"",replay.Limits,replay.GenerationContractVersion,new Dictionary<string,string>{{"schema:sample",legacyIdentity}});
        Assert.Throws<InvalidOperationException>(()=>new MockContext(legacy.ToOptions().UseSchema("sample",json)));
    }
}
