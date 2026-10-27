#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Schema.Tests;

public sealed class FormatAndDialectRegressionTests
{
    private const string Standard = "https://json-schema.org/draft/2020-12/schema";
    private const string CoreOnly = "https://example.com/core-only";

    private static Dictionary<string, string> Documents(bool formats = false)
    {
        var vocabularies = new JObject { ["https://json-schema.org/draft/2020-12/vocab/core"] = true };
        if (formats) vocabularies["https://json-schema.org/draft/2020-12/vocab/format-assertion"] = true;
        return new Dictionary<string, string>
        {
            [CoreOnly] = new JObject { ["$id"] = CoreOnly, ["$schema"] = Standard, ["$vocabulary"] = vocabularies }.ToString()
        };
    }

    [Fact]
    public void Inactive_applicators_are_annotations_during_compilation_and_generation()
    {
        string source = new JObject
        {
            ["$schema"] = CoreOnly, ["type"] = "not-a-type", ["allOf"] = new JArray(false),
            ["properties"] = new JObject { ["ignored"] = new JObject { ["$ref"] = "https://example.com/missing" } },
            ["unevaluatedProperties"] = new JObject { ["$ref"] = "https://example.com/also-missing" }
        }.ToString();
        using var schema = CompiledSchema.Compile(source, Documents());
        Assert.True(schema.Validate("{}"));
        Assert.True(schema.Validate("42"));
        using var context = new MockContext(new MockOptions { Seed = 91 });
        Assert.True(schema.Validate(schema.Generate(context).Json));
    }

    [Fact]
    public void Active_references_still_require_loaded_documents()
    {
        var error = Assert.Throws<SchemaGenerationException>(() => CompiledSchema.Compile(
            new JObject { ["$schema"] = CoreOnly, ["$ref"] = "https://example.com/missing" }.ToString(), Documents()));
        Assert.Equal(SchemaFailure.MissingReference, error.Failure);
    }

    [Fact]
    public void Inactive_ids_do_not_replace_active_resources_and_embedded_dialects_remain_active()
    {
        var actual = new JObject { ["$id"] = "https://example.com/actual", ["$schema"] = Standard, ["const"] = 7 };
        string source = new JObject
        {
            ["$schema"] = CoreOnly, ["$ref"] = "https://example.com/actual", ["$defs"] = new JObject { ["actual"] = actual },
            ["properties"] = new JObject { ["ignored"] = new JObject { ["$id"] = "https://example.com/actual", ["$ref"] = "https://example.com/missing" } }
        }.ToString();
        using var schema = CompiledSchema.Compile(source, Documents());
        Assert.True(schema.Validate("7"));
        Assert.False(schema.Validate("8"));
        using var context = new MockContext(new MockOptions { Seed = 92 });
        Assert.Equal("7", schema.Generate(context).Json);
    }

    [Fact]
    public void Private_format_constraints_preserve_inactive_keywords_in_custom_dialects()
    {
        string source = new JObject
        {
            ["$schema"] = CoreOnly, ["format"] = "regex", ["type"] = "not-a-type", ["allOf"] = new JArray(false),
            ["properties"] = new JObject { ["ignored"] = new JObject { ["$ref"] = "https://example.com/missing" } }
        }.ToString();
        using var schema = CompiledSchema.Compile(source, Documents(true));
        Assert.True(schema.Validate(JsonConvert.SerializeObject("[[]")));
        Assert.False(schema.Validate(JsonConvert.SerializeObject(@"\a")));
        using var context = new MockContext(new MockOptions { Seed = 93 });
        Assert.True(schema.Validate(schema.Generate(context).Json));
        using var annotation = CompiledSchema.Compile(source, Documents(true), new SchemaOptions { AssertFormat = false });
        Assert.True(annotation.Validate(JsonConvert.SerializeObject(@"\a")));
    }

    [Theory]
    [InlineData("{\"anyOf\":[{\"type\":\"string\",\"format\":\"email\"},{\"type\":\"integer\"}]}", "7", true)]
    [InlineData("{\"not\":{\"format\":\"email\"}}", "\"not-an-email\"", true)]
    [InlineData("{\"not\":{\"format\":\"email\"}}", "\"a@example.com\"", false)]
    [InlineData("{\"oneOf\":[{\"format\":\"email\"},{\"format\":\"regex\"}]}", "\"a@example.com\"", false)]
    [InlineData("{\"oneOf\":[{\"format\":\"email\"},{\"format\":\"regex\"}]}", "\"[[]\"", true)]
    [InlineData("{\"propertyNames\":{\"format\":\"email\"}}", "{\"a@example.com\":\"not-an-email\"}", true)]
    [InlineData("{\"propertyNames\":{\"format\":\"email\"}}", "{\"invalid\":\"a@example.com\"}", false)]
    [InlineData("{\"contains\":{\"format\":\"email\"},\"unevaluatedItems\":false}", "[\"a@example.com\",\"invalid\"]", false)]
    [InlineData("{\"contains\":{\"format\":\"email\"},\"unevaluatedItems\":false}", "[\"a@example.com\"]", true)]
    public void Shared_constraints_keep_composition_and_evaluated_location_semantics(string source, string instance, bool valid)
    {
        using var schema = CompiledSchema.Compile(source, options: new SchemaOptions { AssertFormat = true });
        Assert.Equal(valid, schema.Validate(instance));
        Assert.Equal(valid, schema.Validate(instance));
    }

    [Fact]
    public void Corrected_formats_work_through_external_and_dynamic_references()
    {
        const string tree = "https://example.com/tree";
        var docs = new Dictionary<string, string>
        {
            [tree] = new JObject
            {
                ["$id"] = tree, ["$dynamicAnchor"] = "node", ["type"] = "object", ["required"] = new JArray("email"),
                ["properties"] = new JObject { ["email"] = new JObject { ["format"] = "email" }, ["child"] = new JObject { ["$dynamicRef"] = "#node" } },
                ["unevaluatedProperties"] = false
            }.ToString()
        };
        using var schema = CompiledSchema.Compile(new JObject { ["$ref"] = tree }.ToString(), docs, new SchemaOptions { AssertFormat = true });
        Assert.True(schema.Validate("{\"email\":\"a@example.com\",\"child\":{\"email\":\"b@example.com\"}}"));
        Assert.False(schema.Validate("{\"email\":\"a@example.com\",\"child\":{\"email\":\"invalid\"}}"));
        Assert.True(schema.Validate("{\"email\":\"a@example.com\",\"child\":{\"email\":\"c@example.com\"}}"));
    }

    [Fact]
    public void Wide_schema_and_cache_replacement_keep_correct_results()
    {
        var properties = new JObject(); var instance = new JObject();
        for (int i = 0; i < 512; i++)
        {
            properties["field" + i] = new JObject { ["type"] = "string", ["format"] = "email" };
            instance["field" + i] = "user" + i + "@example.com";
        }
        using var schema = CompiledSchema.Compile(new JObject { ["type"] = "object", ["properties"] = properties }.ToString(), options: new SchemaOptions { AssertFormat = true });
        Assert.True(schema.Validate(instance.ToString()));
        instance["field0"] = "invalid-one";
        Assert.False(schema.Validate(instance.ToString()));
        instance["field0"] = "invalid-two";
        Assert.False(schema.Validate(instance.ToString()));
        instance["field0"] = "valid@example.com";
        Assert.True(schema.Validate(instance.ToString()));
        schema.Dispose(); schema.Dispose();
        Assert.Throws<ObjectDisposedException>(() => schema.Validate("{}"));
    }

    [Fact]
    public void Format_processing_obeys_steps_input_bytes_and_cancellation()
    {
        using var schema = CompiledSchema.Compile("{\"format\":\"email\"}", options: new SchemaOptions { AssertFormat = true, MaxSearchSteps = 5, MaxDocumentBytes = 128 });
        var budget = Assert.Throws<SchemaGenerationException>(() => schema.Validate("[\"a\",\"b\",\"c\"]"));
        Assert.Equal(SchemaFailure.BudgetExhausted, budget.Failure);
        Assert.Equal(schema.Identity, budget.SchemaIdentity);
        var bytes = Assert.Throws<SchemaGenerationException>(() => schema.Validate(JsonConvert.SerializeObject(new string('a', 129))));
        Assert.Equal(SchemaFailure.BudgetExhausted, bytes.Failure);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => schema.Validate("\"a@example.com\"", cancellation.Token));
    }

    [Fact]
    public void A_failed_or_canceled_validation_does_not_poison_the_owned_cache()
    {
        using var schema = CompiledSchema.Compile("{\"format\":\"email\"}", options: new SchemaOptions { AssertFormat = true, MaxSearchSteps = 20 });
        Assert.False(schema.Validate("\"invalid\""));
        Assert.Equal(SchemaFailure.BudgetExhausted, Assert.Throws<SchemaGenerationException>(() =>
            schema.Validate(JsonConvert.SerializeObject(Enumerable.Range(0, 30).Select(x => "value" + x)))).Failure);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => schema.Validate("\"different-invalid\"", cancellation.Token));
        Assert.False(schema.Validate("\"different-invalid\""));
        Assert.True(schema.Validate("\"valid@example.com\""));
        Assert.False(schema.Validate("\"invalid\""));
    }

    [Fact]
    public void Format_processing_replay_and_parallel_generation_are_deterministic()
    {
        const string source = "{\"type\":\"object\",\"properties\":{\"email\":{\"type\":\"string\",\"format\":\"email\"}},\"required\":[\"email\"],\"additionalProperties\":false}";
        var options = new MockOptions { Seed = 97 }.UseSchema("people", source, limits: new SchemaOptions { AssertFormat = true });
        using var context = new MockContext(options);
        var generator = context.Schema("people");
        var serial = generator.Enumerate(20).Select(x => x.Json).ToArray();
        Assert.Equal(serial, generator.EnumerateParallel(20, 4).Select(x => x.Json));
        using var replay = new MockContext(context.Replay.ToOptions().UseSchema("people", source, limits: new SchemaOptions { AssertFormat = true }));
        Assert.Equal(serial[13], replay.Schema("people").At(13).Json);
    }
}
