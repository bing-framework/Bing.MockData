#pragma warning disable xUnit1051
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Xml.Linq;
using Bing.MockData.Models;
using Newtonsoft.Json;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;
public class DomainTests
{
    private static MockContext Context()=>new(new MockOptions{Seed=190,ReferenceTime=new DateTimeOffset(2026,10,25,12,0,0,TimeSpan.Zero)});
    [Fact] public void Identifiers_checksums_svg_and_coordinates_obey_formats()
    {
        using var c=Context();
        for(int i=0;i<100;i++)
        {
            var f=c.ForRecord(i);var id=f.Identifiers.Ulid(DateTimeOffset.FromUnixTimeMilliseconds(0));Assert.Equal(26,id.Length);Assert.StartsWith("0000000000",id);
            var ean=f.Commerce.Ean13();Assert.Equal(CommerceData.BarcodeCheckDigit(ean[..^1]),ean[^1]);
            var center=new GeoPoint(89.9,179.99);Assert.InRange(center.DistanceMeters(f.Geography.Nearby(center,10000)),0,10000.00001);
            var svg=f.Images.Placeholder(text:"<&\"测试>");Assert.Equal("<&\"测试>",XDocument.Parse(svg).Root!.Elements().Last().Value);
            var company=f.Templates.Company().Build().At(i);if(company.CreditCode!=null){Assert.Equal(company.Address.CountyCode,company.CreditCode.Substring(2,6));const string chars="0123456789ABCDEFGHJKLMNPQRTUWXY";int weight=1,sum=0;for(int n=0;n<17;n++){sum+=chars.IndexOf(company.CreditCode[n])*weight;weight=weight*3%31;}Assert.Equal(chars[(31-sum%31)%31],company.CreditCode[17]);}
        }
        Assert.Equal(120,new MockColor(0,255,0).Hue);Assert.Equal(1,new MockColor(255,0,0).Saturation);
    }
    [Fact] public void Business_scenarios_keep_relations_amounts_and_event_order()
    {
        using var c=Context();var users=c.Templates.User().Build().GenerateMany(5);var products=c.Templates.Product().Build().GenerateMany(5);
        Assert.All(users,u=>{Assert.InRange(u.LastLoginAt,u.RegisteredAt,c.ReferenceTime);Assert.NotEmpty(u.AvatarSvg);});
        foreach(OrderStatus status in Enum.GetValues(typeof(OrderStatus)))
        {
            var order=c.Templates.Order(users,products,status:status).Build().At(3);
            Assert.Equal(order.Items.Sum(x=>x.Subtotal)-order.Discount+order.Shipping+order.Tax,order.Total);
            Assert.InRange(order.RefundedAmount,0,order.PaidAmount);Assert.All(order.Payments.Concat(order.Refunds),x=>Assert.InRange(x.Timestamp,order.CreatedAt,c.ReferenceTime));
            if(order.ShippedAt!=null){var shipment=c.Templates.Shipment(order).Build().At(2);Assert.Equal(shipment.Events.OrderBy(x=>x.Timestamp),shipment.Events);Assert.All(shipment.Items,x=>Assert.Contains(x,order.Items));}
        }
        var org=c.Templates.Organization().Build().At(0);Assert.All(org.Departments,d=>Assert.True(d.ParentId==null||d.ParentId<d.Id));Assert.All(org.Employees,e=>Assert.Contains(org.Departments,d=>d.Id==e.DepartmentId));
        var inventory=c.Templates.Inventory(opening:0).Build().At(0);long balance=0;foreach(var row in inventory.Movements){balance+=row.Quantity;Assert.Equal(balance,row.Balance);Assert.True(balance>=0);}
        var ledger=c.Finance.Ledger(currency:new CurrencyInfo("JPY",0));Assert.Equal(ledger.OpeningBalance+ledger.Entries.Sum(x=>x.Amount),ledger.ClosingBalance);Assert.All(ledger.Entries,e=>Assert.Equal(decimal.Truncate(e.Amount),e.Amount));
    }
    [Fact] public void Time_series_is_indexed_parallel_replayable_and_freezes_configuration()
    {
        using var c=Context();var options=new TimeSeriesOptions{MissingProbability=.2,OutlierProbability=.1,Trend=.02,Amplitude=4};var generator=c.TimeSeries.Create(options);options.Trend=100;
        var serial=generator.Enumerate(200).Select(JsonConvert.SerializeObject).ToArray();Assert.Equal(serial,generator.EnumerateParallel(200,4).Select(JsonConvert.SerializeObject));Assert.Equal(serial[37],JsonConvert.SerializeObject(generator.At(37)));
        var logs=c.Templates.RequestLog().Build();Assert.Equal(logs.Enumerate(100).Select(JsonConvert.SerializeObject),logs.EnumerateParallel(100,4).Select(JsonConvert.SerializeObject));
    }
    public sealed class Annotated
    { [Required,StringLength(12,MinimumLength=5)] public string Name {get;set;} [Range(18,30)]public int Age {get;set;} [Compare(nameof(Name))]public string Repeat {get;set;} [EmailAddress]public string Email {get;set;} [RegularExpression("[A-Z]{3}[0-9]{2}")]public string Code {get;set;} }
    [Fact] public void Auto_builder_obeys_constraints_and_explicit_rules()
    {
        using var c=Context();var generator=c.AutoBuilder<Annotated>().RuleFor(x=>x.Age,_=>21).Build();foreach(var item in generator.Enumerate(20)){Assert.Equal(21,item.Age);Assert.Equal(item.Name,item.Repeat);Assert.InRange(item.Name.Length,5,12);Assert.True(Validator.TryValidateObject(item,new ValidationContext(item),null,true));}
    }
    private sealed class Resource:IDisposable { public int Disposals;public void Dispose()=>Disposals++; }
    public sealed class ComparisonChain
    { [MinLength(50)] public string Z {get;set;} [Compare(nameof(Z))]public string B {get;set;} [Compare(nameof(B))]public string A {get;set;} [Range(typeof(decimal),"0.0001","0.0009")]public decimal Amount {get;set;} }
    [Fact]public void Auto_constraints_and_currency_precision_support_non_default_scales()
    {
        using var c=Context();var value=c.AutoBuilder<ComparisonChain>().Build().At(0);Assert.True(value.Z.Length>=50);Assert.Equal(value.Z,value.A);Assert.InRange(value.Amount,.0001m,.0009m);
        var products=c.Templates.Product(new CurrencyInfo("JPY",0)).Build().GenerateMany(5);var users=c.Templates.User().Build().GenerateMany(2);
        var order=c.Templates.Order(users,products,status:OrderStatus.PartiallyRefunded).Build().At(0);Assert.Equal(decimal.Truncate(order.RefundedAmount),order.RefundedAmount);
        Assert.Throws<MockGenerationException>(()=>c.Templates.Order(users,products).Override(x=>x.ShippedAt,(_,_)=>DateTimeOffset.UtcNow).Build().At(0));
    }
    [Fact] public void Extension_factory_ownership_replay_and_construction_failure_are_explicit()
    {
        var borrowed=new Resource();Resource created=null;var options=new MockOptions().UseExtension("borrowed",borrowed,"v1").UseExtensionFactory("owned",()=>created=new Resource(),"v2");
        var c=new MockContext(options);var replay=c.Replay;Assert.Same(borrowed,c.Extension<Resource>("borrowed"));c.Dispose();c.Dispose();Assert.Equal(0,borrowed.Disposals);Assert.Equal(1,created.Disposals);Assert.Throws<ObjectDisposedException>(()=>c.Extension<Resource>("owned"));
        Assert.Throws<InvalidOperationException>(()=>new MockContext(replay.ToOptions()));
        Resource failed=null;var broken=new MockOptions().UseExtensionFactory("first",()=>failed=new Resource(),"v1").UseExtensionFactory<Resource>("second",()=>throw new InvalidOperationException("failure"),"v1");Assert.Throws<InvalidOperationException>(()=>new MockContext(broken));Assert.Equal(1,failed.Disposals);
    }
}
