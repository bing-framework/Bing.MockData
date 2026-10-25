#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Schema;
using Newtonsoft.Json;
using Xunit;

namespace Bing.MockData.Schema.Tests;
public class SchemaTests
{
    public static IEnumerable<object[]> Schemas()
    {
        foreach(var schema in new[]{
            "{\"type\":\"integer\",\"minimum\":10000000000000000000000000000000000000001,\"maximum\":10000000000000000000000000000000000000009}",
            "{\"type\":\"number\",\"minimum\":0.000000000000000000000000000000001,\"maximum\":0.000000000000000000000000000000002}",
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"minLength\":2},\"id\":{\"type\":\"integer\"}},\"required\":[\"name\",\"id\"],\"additionalProperties\":false}",
            "{\"allOf\":[{\"type\":\"number\",\"minimum\":10},{\"maximum\":20,\"multipleOf\":3}]}",
            "{\"oneOf\":[{\"type\":\"string\",\"pattern\":\"^[A-Z]{3}$\"},{\"type\":\"integer\",\"minimum\":1}]}",
            "{\"type\":\"array\",\"minItems\":2,\"maxItems\":4,\"items\":{\"type\":\"integer\"},\"contains\":{\"const\":7},\"minContains\":1,\"uniqueItems\":true}",
            "{\"type\":\"object\",\"properties\":{\"a\":{\"const\":1}},\"required\":[\"a\"],\"dependentRequired\":{\"a\":[\"b\"]},\"dependentSchemas\":{\"a\":{\"properties\":{\"b\":{\"const\":2}}}},\"unevaluatedProperties\":false}",
            "{\"$defs\":{\"value\":{\"type\":\"integer\",\"minimum\":3}},\"$ref\":\"#/$defs/value\",\"maximum\":5}",
            "{\"type\":\"integer\",\"if\":{\"minimum\":0},\"then\":{\"maximum\":5},\"else\":{\"minimum\":-5},\"not\":{\"const\":0}}",
            "{\"const\":\"😀\",\"minLength\":1,\"maxLength\":1}",
            "{\"type\":\"object\",\"minProperties\":2,\"maxProperties\":3,\"propertyNames\":{\"pattern\":\"^[A-Z]{3}$\"},\"additionalProperties\":{\"type\":\"integer\"}}",
            "{\"$id\":\"https://example.com/tree\",\"$dynamicAnchor\":\"node\",\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"integer\"},\"child\":{\"$dynamicRef\":\"#node\"}},\"required\":[\"value\"],\"unevaluatedProperties\":false}",
            "{\"type\":\"array\",\"prefixItems\":[{\"const\":1},{\"type\":\"string\"}],\"minItems\":2,\"unevaluatedItems\":false}",
            "{\"type\":\"string\",\"contentEncoding\":\"base64\",\"contentMediaType\":\"application/json\"}"
        })yield return new object[]{schema};
    }
    [Theory,MemberData(nameof(Schemas))] public void Generated_candidates_pass_whole_schema(string text)
    {using var schema=CompiledSchema.Compile(text);using var c=new MockContext(new MockOptions{Seed=91});var g=schema.Generator(c);for(int i=0;i<10;i++){var sample=g.At(i);Assert.True(schema.Validate(sample.Json));Assert.Equal(sample.Json,JsonConvert.SerializeObject(sample));}}
    [Fact]public void References_are_offline_and_owned_schemas_replay()
    {var docs=new Dictionary<string,string>{{"https://example.com/value","{\"type\":\"integer\",\"minimum\":10}"}};var options=new MockOptions{Seed=11}.UseSchema("value","{\"$ref\":\"https://example.com/value\"}",docs);using var c=new MockContext(options);var g=c.Schema("value");Assert.Equal(g.Enumerate(20).Select(x=>x.Json),g.EnumerateParallel(20,4).Select(x=>x.Json));Assert.Single(c.Replay.ExtensionVersions);}
    [Fact]public void Contradiction_and_budget_are_distinct_and_disposal_is_enforced()
    {using var c=new MockContext();using var never=CompiledSchema.Compile("false");Assert.Equal(SchemaFailure.ProvenContradiction,Assert.Throws<SchemaGenerationException>(()=>never.Generate(c)).Failure);using var hard=CompiledSchema.Compile("{\"type\":\"integer\",\"minimum\":3,\"maximum\":1}",options:new SchemaOptions{MaxSearchSteps=20});Assert.Equal(SchemaFailure.BudgetExhausted,Assert.Throws<SchemaGenerationException>(()=>hard.Generate(c)).Failure);var disposed=CompiledSchema.Compile("true");disposed.Dispose();Assert.Throws<ObjectDisposedException>(()=>disposed.Validate("null"));}
    [Fact]public void Invalid_schema_and_schema_capacity_are_rejected_before_generation()
    {Assert.Equal(SchemaFailure.InvalidSchema,Assert.Throws<SchemaGenerationException>(()=>CompiledSchema.Compile("{\"type\":\"nonsense\"}")).Failure);var options=new MockOptions{Limits=new MockLimits{MaxCompiledSchemas=1}}.UseSchema("first","true");Assert.Throws<MockLimitException>(()=>options.UseSchema("second","true"));var lowered=new MockOptions().UseSchema("first","true").UseSchema("second","true");lowered.Limits.MaxCompiledSchemas=1;Assert.Throws<MockLimitException>(()=>new MockContext(lowered));}
    [Fact]public void Data_is_not_treated_as_schema_and_missing_references_are_distinct()
    {
        using var c=new MockContext(new MockOptions{Seed=7});using var schema=CompiledSchema.Compile("{\"$id\":\"https://example.com/root\",\"$defs\":{\"v\":{\"const\":7}},\"const\":{\"$id\":\"https://example.com/root\",\"v\":8}}");
        Assert.True(schema.Validate(schema.Generate(c).Json));
        Assert.Equal(SchemaFailure.MissingReference,Assert.Throws<SchemaGenerationException>(()=>CompiledSchema.Compile("{\"$ref\":\"https://example.com/missing\"}")).Failure);
        Assert.Equal(SchemaFailure.BudgetExhausted,Assert.Throws<SchemaGenerationException>(()=>CompiledSchema.Compile(new string('x',100),options:new SchemaOptions{MaxDocumentBytes=10})).Failure);
    }
    [Fact]public void Format_vocabulary_and_large_integer_constraints_keep_standard_semantics()
    {
        const string meta="https://example.com/asserted";
        var documents=new Dictionary<string,string>{{meta,"{\"$id\":\"https://example.com/asserted\",\"$schema\":\"https://json-schema.org/draft/2020-12/schema\",\"$vocabulary\":{\"https://json-schema.org/draft/2020-12/vocab/core\":true,\"https://json-schema.org/draft/2020-12/vocab/format-assertion\":true}}"}};
        string source="{\"$schema\":\""+meta+"\",\"format\":\"regex\"}";
        using var asserted=CompiledSchema.Compile(source,documents);Assert.False(asserted.Validate("\"\\\\a\""));
        using var annotation=CompiledSchema.Compile(source,documents,new SchemaOptions{AssertFormat=false});Assert.True(annotation.Validate("\"\\\\a\""));
        using var c=new MockContext(new MockOptions{Seed=4});using var huge=CompiledSchema.Compile("{\"type\":\"array\",\"minItems\":1e100}",options:new SchemaOptions{MaxSearchSteps=20});Assert.Equal(SchemaFailure.BudgetExhausted,Assert.Throws<SchemaGenerationException>(()=>huge.Generate(c)).Failure);
        using var exponent=CompiledSchema.Compile("{\"type\":\"number\",\"minimum\":1e100001}");var failure=Assert.Throws<SchemaGenerationException>(()=>exponent.Generate(c.ForRecord(13)));Assert.Equal(13,failure.Replay.RecordIndex);Assert.Equal(exponent.Identity,failure.SchemaIdentity);
    }
}
