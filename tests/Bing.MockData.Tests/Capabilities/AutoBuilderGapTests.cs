#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class AutoBuilderGapTests
{
    public enum State { Open, Closed }
    public sealed class Constrained
    {
        [Required,EmailAddress,MaxLength(10)] public string Email {get;set;}
        [Url,StringLength(30,MinimumLength=24)] public string Url {get;set;}
        [Phone,StringLength(8,MinimumLength=8)] public string Phone {get;set;}
        [CreditCard,StringLength(19,MinimumLength=19)] public string Card {get;set;}
        [EnumDataType(typeof(State))] public string Status {get;set;}
        [MinLength(2),MaxLength(3)] public bool[] Flags {get;set;}
        [MinLength(2),MaxLength(3)] public Guid[] Ids {get;set;}
        [MinLength(2),MaxLength(3)] public decimal[] Amounts {get;set;}
        public IReadOnlyList<DateTimeOffset> Dates {get;set;}
        public State[] States {get;set;}
        [Range(typeof(DateTimeOffset),"2000-01-01T00:00:00+00:00","2001-01-01T00:00:00+00:00")] public DateTimeOffset At {get;set;}
    }
    [Fact]
    public void Format_lengths_scalar_arrays_and_enum_annotations_are_composed()
    {
        using var context=new MockContext(new MockOptions{Seed=42});
        var generator=context.AutoBuilder<Constrained>().Build();
        var values=generator.GenerateMany(30);
        Assert.All(values,value=>
        {
            Assert.True(Validator.TryValidateObject(value,new ValidationContext(value),null,true));
            Assert.InRange(value.Flags.Length,2,3);Assert.InRange(value.Ids.Length,2,3);
            Assert.All(value.Ids,id=>Assert.NotEqual(Guid.Empty,id));
        });
        Assert.Equal(values.Select(JsonConvert.SerializeObject),generator.EnumerateParallel(30,4).Select(JsonConvert.SerializeObject));
        Assert.Equal(JsonConvert.SerializeObject(values[21]),JsonConvert.SerializeObject(generator.At(21)));
    }
    public sealed class InvalidExplicit { [EmailAddress] public string Email {get;set;} }
    [Fact]
    public void Constraint_failure_identifies_member_attribute_and_record()
    {
        using var context=new MockContext(new MockOptions{Seed=1,Limits=new MockLimits{MaxAttempts=2}});
        var failure=Assert.Throws<MockGenerationException>(()=>context.AutoBuilder<InvalidExplicit>().RuleFor(x=>x.Email,_=>"invalid").Build().At(12));
        Assert.EndsWith("Email",failure.FieldPath);Assert.Contains("EmailAddress",failure.RuleId);Assert.Equal(12,failure.RecordIndex);
    }
    public sealed class Conflict { [MinLength(20),MaxLength(2)] public string Name {get;set;} }
    [Fact]
    public void Impossible_lengths_fail_during_configuration_with_member_name()
    {
        using var context=new MockContext();
        var failure=Assert.Throws<InvalidOperationException>(()=>context.AutoBuilder<Conflict>().Build());
        Assert.Contains("Conflict.Name",failure.Message);Assert.Contains("MinLength",failure.Message);
    }
    public sealed class RegisteredValue { public int Value {get;set;} }
    public sealed class RegisteredArray { [MinLength(1),MaxLength(2)] public RegisteredValue[] Items {get;set;} }
    [Fact]
    public void Registered_array_elements_use_frozen_factory_and_budget()
    {
        using var context=new MockContext(new MockOptions().Register("value",c=>new RegisteredValue{Value=c.Numbers.Int(1,3)}));
        Assert.All(context.AutoBuilder<RegisteredArray>().Build().At(0).Items,item=>Assert.InRange(item.Value,1,3));
        using var limited=new MockContext(new MockOptions{Limits=new MockLimits{MaxCollectionSize=1}});
        Assert.Throws<MockLimitException>(()=>limited.AutoBuilder<Constrained>().Build());
    }
}
