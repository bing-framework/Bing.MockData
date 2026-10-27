#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class AutoCompletionTests
{
    public class TextRecord
    {
        [Required,StringLength(10,MinimumLength=10),RegularExpression("^[A-Z]{1,10000}$")]
        public string Value {get;set;}
    }
    public class Child { [Range(18,25)] public int Age {get;set;} [Compare(nameof(Name))] public string Confirmation {get;set;} [StringLength(8,MinimumLength=8)] public string Name {get;set;} }
    public class Graph
    {
        public Child Child {get;set;}
        [MinLength(3),MaxLength(3)] public Dictionary<string,Child> Map {get;set;}
        [MinLength(5),MaxLength(5)] public HashSet<int> Codes {get;set;}
        [MinLength(2),MaxLength(2)] public IList<Child> Children {get;set;}
    }
    public class Cycle { public Cycle Next {get;set;} }
    public class Exhausted { [MinLength(3),MaxLength(3)] public ISet<bool> Values {get;set;} }
    public class ReadOnlyGraph { [MinLength(2),MaxLength(2)] public IReadOnlyDictionary<int,Child> Map {get;set;} }
    [Fact] public void Regex_length_is_constructed_in_one_attempt()
    {
        using var c=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxAttempts=1}});
        Assert.All(c.AutoBuilder<TextRecord>().Build().GenerateMany(30),x=>{Assert.Equal(10,x.Value.Length);Assert.Matches("^[A-Z]{10}$",x.Value);});
    }
    [Theory]
    [InlineData("^(AB){0,100}(CD|EF){1,100}$",10)]
    [InlineData("^(X{1,9000}|Y{1,9000})Z{0,20}$",17)]
    [InlineData("^(?:😀){1,100}$",8)]
    [InlineData("^(?:a?){0,100}$",5)]
    public void Regex_interval_handles_sequences_branches_and_surrogates(string pattern,int length)
    {
        using var c=new MockContext(new MockOptions{Seed=91});for(int i=0;i<20;i++){var value=c.ForRecord(i).Text.Regex(pattern,length,length);Assert.Equal(length,value.Length);Assert.Matches(pattern,value);}
    }
    [Fact] public void Nested_objects_maps_and_sets_preserve_constraints_and_replay()
    {
        using var c=new MockContext(new MockOptions{Seed=91});var generator=c.AutoBuilder<Graph>().Build();var value=generator.At(7);
        Assert.Equal(3,value.Map.Count);Assert.Equal(5,value.Codes.Count);Assert.Equal(2,value.Children.Count);
        foreach(var child in value.Map.Values.Concat(value.Children).Append(value.Child)){Assert.InRange(child.Age,18,25);Assert.Equal(child.Name,child.Confirmation);}
        Assert.Equal(generator.Enumerate(20).Select(JsonConvert.SerializeObject),generator.EnumerateParallel(20,4).Select(JsonConvert.SerializeObject));
        Assert.Equal(JsonConvert.SerializeObject(value),JsonConvert.SerializeObject(generator.At(7)));
    }
    [Fact] public void Cycles_exhaustion_limits_and_cancellation_are_bounded()
    {
        using var c=new MockContext(new MockOptions{Seed=91});Assert.Throws<InvalidOperationException>(()=>c.AutoBuilder<Cycle>().Build());
        Assert.Null(c.AutoBuilder<Cycle>().RuleFor(x=>x.Next,_=>(Cycle)null).Build().Generate().Next);
        Assert.Throws<MockGenerationException>(()=>c.AutoBuilder<Exhausted>().Build().Generate());
        Assert.Throws<InvalidOperationException>(()=>c.Text.Regex("^(AB){1,20}$",3,3));
        using var shallow=new MockContext(new MockOptions{Limits=new MockLimits{MaxDepth=1}});Assert.Throws<MockLimitException>(()=>shallow.AutoBuilder<Graph>().Build().Generate());
        using var cancel=new CancellationTokenSource();cancel.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>c.AutoBuilder<Graph>().Build().At(0,cancel.Token));
    }

    [Theory]
    [InlineData("domain-v2")]
    [InlineData("domain-v3")]
    [InlineData("domain-v4")]
    public void Registered_nested_types_take_precedence_and_old_replays_are_rejected(string oldContract)
    {
        var options=new MockOptions{Seed=91};options.Register<Child>("child",_=>new Child{Age=21,Name="fixed",Confirmation="fixed"});
        using var c=new MockContext(options);var graph=c.AutoBuilder<ReadOnlyGraph>().Build().At(0);
        Assert.Equal(2,graph.Map.Count);Assert.All(graph.Map.Values,child=>Assert.Equal("fixed",child.Name));
        var r=c.Replay;var old=new ReplayInfo(r.Seed,r.ReferenceTime,r.Locale,r.AlgorithmVersion,r.DataVersion,r.LibraryVersion,r.RuntimeVersion,0,"",r.Limits,oldContract,r.ExtensionVersions);
        Assert.Throws<InvalidOperationException>(()=>old.ToOptions());
        Assert.Equal(c.Seed,r.ToOptions().Seed);
    }

    [Fact] public void Product_template_freezes_compatible_category_and_brand()
    {
        using var c=new MockContext(new MockOptions{Seed=91});var options=new ProductSampleOptions{CategoryCode="electronics"};
        var generator=c.Templates.ConfiguredProduct(options).Build();options.CategoryCode="home";
        foreach(var product in generator.GenerateMany(30))
        {
            var definition=c.Commerce.Categories().Single(x=>x.Code==product.Category.Code);
            Assert.Contains(product.Brand,definition.Brands);Assert.Contains(product.Category.Specification,definition.Specifications);
            Assert.Contains(product.Brand,c.Commerce.Brands("electronics"));
        }
    }

    [Fact] public void Named_business_values_respect_text_budgets()
    {
        using var c=new MockContext(new MockOptions{Seed=91,Limits=new MockLimits{MaxTextLength=1}});
        Assert.Throws<MockLimitException>(()=>c.People.Job());Assert.Throws<MockLimitException>(()=>c.Companies.Industry());
        Assert.Throws<MockLimitException>(()=>c.Companies.Department());Assert.Throws<MockLimitException>(()=>c.Colors.Named());
    }
}
