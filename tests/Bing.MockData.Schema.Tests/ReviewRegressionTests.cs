#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Schema.Tests;

public class ReviewRegressionTests
{
    [Theory]
    [InlineData("[[]",true)]
    [InlineData(@"[\[]",true)]
    [InlineData("[]",true)]
    [InlineData("[^]",true)]
    [InlineData(@"\(\?\>a\)",true)]
    [InlineData("[(?>]",true)]
    [InlineData("(?<name>a)\\k<name>",true)]
    [InlineData("(?>a)",false)]
    [InlineData("(?n:a)",false)]
    [InlineData("(?<x>a)(?<-x>b)",false)]
    [InlineData("(?<1>a)",false)]
    [InlineData("(?i)a",false)]
    [InlineData("(?#comment)a",false)]
    public void Regex_assertions_preserve_ecma_character_classes_and_group_syntax(string pattern,bool valid)
    {
        using var schema=CompiledSchema.Compile("{\"format\":\"regex\"}",options:new SchemaOptions{AssertFormat=true});
        Assert.Equal(valid,schema.Validate(JsonConvert.SerializeObject(pattern)));
        using var context=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxAttempts=1}});
        using var constant=CompiledSchema.Compile(new JObject{["type"]="string",["format"]="regex",["const"]=pattern}.ToString(),options:new SchemaOptions{AssertFormat=true});
        if(valid)Assert.Equal(pattern,JsonConvert.DeserializeObject<string>(constant.Generate(context).Json));
        else Assert.Equal(SchemaFailure.BudgetExhausted,Assert.Throws<SchemaGenerationException>(()=>constant.Generate(context)).Failure);
        using var annotation=CompiledSchema.Compile("{\"format\":\"regex\"}",options:new SchemaOptions{AssertFormat=false});
        Assert.True(annotation.Validate(JsonConvert.SerializeObject(pattern)));
    }

    [Theory]
    [InlineData("$ref","https://example.com/child","{\"type\":\"string\",\"minLength\":-1}")]
    [InlineData("$dynamicRef","https://example.com/child","{\"type\":\"string\",\"minLength\":-1}")]
    [InlineData("$ref","https://example.com/child#/data","{\"data\":{\"type\":\"string\",\"minLength\":-1}}")]
    public void Referenced_invalid_schema_is_rejected_with_its_location(string keyword,string reference,string document)
    {
        var error=Assert.Throws<SchemaGenerationException>(()=>CompiledSchema.Compile(new JObject{[keyword]=reference}.ToString(),new Dictionary<string,string>{{"https://example.com/child",document}}));
        Assert.Equal(SchemaFailure.InvalidSchema,error.Failure);
        Assert.StartsWith("https://example.com/child#",error.SchemaLocation);
        Assert.Contains("minLength",error.SchemaLocation);
        Assert.NotEmpty(error.SchemaIdentity);
    }

    [Fact] public void Unused_documents_and_instance_data_are_not_meta_validated_as_schemas()
    {
        var docs=new Dictionary<string,string>{{"https://example.com/unused","{\"type\":42}"},{"https://example.com/child","{\"const\":{\"type\":42,\"minLength\":-1}}"}};
        using var schema=CompiledSchema.Compile("{\"$ref\":\"https://example.com/child\",\"$defs\":{\"unused\":{\"$ref\":\"https://example.com/not-loaded\"}}}",docs);
        Assert.True(schema.Validate("{\"type\":42,\"minLength\":-1}"));
    }

    [Theory]
    [InlineData("^[a-z]{1,50000}$",10000)]
    [InlineData("^[a-z]{0,100001}$",2)]
    [InlineData("^(?:😀){1,50000}$",4)]
    [InlineData("^(?:😀|a){1,50000}$",7)]
    public void Schema_regex_plans_code_point_length_in_one_attempt(string pattern,int length)
    {
        using var schema=CompiledSchema.Compile(new JObject{["type"]="string",["pattern"]=pattern,["minLength"]=length,["maxLength"]=length}.ToString());
        using var context=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxAttempts=1}});
        var generator=schema.Generator(context);
        var serial=generator.Enumerate(5).Select(x=>x.Json).ToArray();
        Assert.All(serial,value=>Assert.True(schema.Validate(value)));
        Assert.Equal(serial,generator.EnumerateParallel(5,4).Select(x=>x.Json));
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>generator.At(0,cancel.Token));
    }

    [Fact] public void Regex_resource_failure_has_schema_instance_and_replay_locations()
    {
        var child=new JObject{["type"]="string",["pattern"]=new string('a',1025)};
        using var schema=CompiledSchema.Compile(new JObject{["type"]="object",["properties"]=new JObject{["value"]=child},["required"]=new JArray("value")}.ToString());
        using var context=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxTextLength=64,MaxAttempts=1}});
        var error=Assert.Throws<SchemaGenerationException>(()=>schema.Generate(context.ForRecord(17)));
        Assert.Equal(SchemaFailure.BudgetExhausted,error.Failure);
        Assert.Equal("#/properties/value",error.SchemaLocation);Assert.Equal("/value",error.InstanceLocation);
        Assert.Equal(17,error.Replay.RecordIndex);Assert.Equal(schema.Identity,error.SchemaIdentity);
    }
}
