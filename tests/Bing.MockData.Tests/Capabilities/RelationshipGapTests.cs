#pragma warning disable xUnit1051
using System;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Bing.MockData.Models;
using Newtonsoft.Json;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class RelationshipGapTests
{
    private static MockContext Context(string locale="zh-CN")=>new(new MockOptions{Seed=73,Locale=locale,ReferenceTime=new DateTimeOffset(2026,10,25,0,0,0,TimeSpan.Zero)});

    [Theory]
    [InlineData("a","a")]
    [InlineData("😀abc","😀a")]
    [InlineData("e\u0301abc","e\u0301a")]
    [InlineData("","")]
    public void Overridden_short_or_unicode_username_produces_valid_avatar(string username,string initials)
    {
        using var c=Context();var user=c.Templates.User().Override(x=>x.Username,(_,_)=>username).Build().At(0);
        Assert.Equal(initials,XDocument.Parse(user.AvatarSvg).Root!.Elements().Last().Value);
    }
    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    public void Business_text_combines_slots_and_company_has_short_name(string locale)
    {
        using var c=Context(locale);var companies=c.Templates.Company().Build().GenerateMany(10);
        Assert.All(companies,company=>{Assert.NotEmpty(company.ShortName);Assert.True(company.ShortName.Length<company.Name.Length);});
        foreach(var kind in Enum.GetValues<BusinessTextKind>())
        {
            var generator=c.Generator(x=>x.Text.Business(kind,200));var values=generator.GenerateMany(50);
            Assert.True(values.Distinct().Count()>10);Assert.All(values,s=>Assert.InRange(s.Length,1,200));
            Assert.Equal(values,generator.EnumerateParallel(50,4));
        }
        Assert.Throws<ArgumentException>(()=>c.Text.Business(maxLength:1));
    }
    [Fact]
    public void Organization_freezes_definitions_and_generates_consistent_join_tables()
    {
        using var c=Context();var codes=new[]{"view","edit"};
        var options=new OrganizationOptions
        {
            Departments=4,Employees=12,RolesPerEmployee=2,
            Permissions=new[]{new Permission{Code="view",Name="View"},new Permission{Code="edit",Name="Edit"}},
            Roles=new[]{new Role{Id=7,Name="editor",Permissions=codes},new Role{Id=8,Name="viewer",Permissions=new[]{"view"}}}
        };
        var generator=c.Templates.Organization(options).Build();codes[0]="broken";options.Employees=1000;
        var org=generator.At(2);Assert.Equal(12,org.Employees.Count);Assert.Equal(24,org.EmployeeRoles.Count);
        foreach(var link in org.EmployeeRoles){var employee=Assert.Single(org.Employees,e=>e.Id==link.EmployeeId);Assert.Contains(link.RoleId,employee.RoleIds);Assert.Contains(org.Roles,r=>r.Id==link.RoleId);}
        foreach(var link in org.RolePermissions){Assert.Contains(org.Permissions,p=>p.Code==link.PermissionCode);Assert.Contains(link.PermissionCode,Assert.Single(org.Roles,r=>r.Id==link.RoleId).Permissions);}
        Assert.All(org.Departments,d=>Assert.True(d.ParentId==null||d.ParentId<d.Id));
        Assert.Equal(generator.Enumerate(10).Select(JsonConvert.SerializeObject),generator.EnumerateParallel(10,4).Select(JsonConvert.SerializeObject));
        org.Permissions[0].Code="mutated";Assert.Contains(generator.At(2).Permissions,p=>p.Code=="view");
        using var limited=new MockContext(new MockOptions{Limits=new MockLimits{MaxCollectionSize=10}});
        Assert.Throws<MockLimitException>(()=>limited.Templates.Organization(2,4));
        Assert.Throws<ArgumentException>(()=>c.Templates.Organization(new OrganizationOptions{Roles=new[]{new Role{Id=1,Name="bad",Permissions=new[]{"missing"}}}}));
    }
    [Fact]
    public void Fulfillment_allocates_each_order_line_once_and_inventory_is_linked()
    {
        using var c=Context();var users=c.Templates.User().Build().GenerateMany(2);var products=c.Templates.Product().Build().GenerateMany(3);
        var order=c.Templates.Order(users,products,7,7,OrderStatus.Shipped).Build().At(0);
        var warehouse=c.Templates.Warehouse(3,"WEST").Build().At(0);
        var generator=c.Templates.Fulfillment(order,warehouse,2).Build();warehouse.Locations[0].Code="mutated";
        var shipment=generator.At(3);Assert.Equal(4,shipment.Parcels.Count);Assert.Equal(4,shipment.Waybills.Count);
        var lines=shipment.Parcels.SelectMany(p=>p.Lines).ToArray();Assert.Equal(7,lines.Length);Assert.Equal(7,lines.Select(l=>l.OrderItemIndex).Distinct().Count());
        foreach(var line in lines){Assert.Same(order.Items[line.OrderItemIndex],line.Item);Assert.Equal(line.Item.Quantity,line.Quantity);}
        Assert.All(shipment.Parcels,p=>Assert.Contains(p.Origin,shipment.Warehouse.Locations));
        foreach(var bill in shipment.Waybills){Assert.Contains(bill.Parcel,shipment.Parcels);Assert.Equal(bill.Events.OrderBy(e=>e.Timestamp),bill.Events);Assert.Equal(bill.Events.Last().Timestamp,bill.DeliveredAt);Assert.True(bill.DeliveredAt<=c.ReferenceTime);}
        var inventory=c.Templates.Inventory(products[0],shipment.Warehouse.Locations[0],0,40).Build().At(3);long balance=0;
        foreach(var row in inventory.Movements){balance+=row.Quantity;Assert.True(balance>=0);Assert.Equal(balance,row.Balance);Assert.Same(products[0],row.Product);Assert.Same(inventory.Location,row.Location);Assert.Equal(row.Quantity>0?StockMovementKind.Receipt:StockMovementKind.Issue,row.Kind);}
        Assert.Equal(generator.Enumerate(5).Select(JsonConvert.SerializeObject),generator.EnumerateParallel(5,4).Select(JsonConvert.SerializeObject));
        var pending=c.Templates.Fulfillment(order,warehouse,3,false).Build().At(0);Assert.All(pending.Waybills,b=>Assert.Null(b.DeliveredAt));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>generator.At(0,cancellation.Token));
    }
    [Fact]
    public void Request_trace_has_valid_parent_links_shared_trace_and_ordered_events()
    {
        using var c=Context();var generator=c.Templates.RequestTrace(20).Build();var trace=generator.At(8);
        Assert.Null(trace.Logs[0].ParentSpanId);Assert.Equal(20,trace.Logs.Select(l=>l.SpanId).Distinct().Count());
        foreach(var log in trace.Logs)
        {
            Assert.Equal(trace.TraceId,log.TraceId);Assert.True(log.Exchange.RespondedAt<=c.ReferenceTime);
            if(log.ParentSpanId!=null){var parent=Assert.Single(trace.Logs,p=>p.SpanId==log.ParentSpanId);Assert.True(parent.Exchange.RequestedAt<=log.Exchange.RequestedAt);Assert.True(log.Exchange.RespondedAt<=parent.Exchange.RespondedAt);}
        }
        Assert.Equal(trace.Events.OrderBy(e=>e.Timestamp),trace.Events);Assert.All(trace.Events,e=>{Assert.Equal(trace.TraceId,e.TraceId);Assert.Contains(trace.Logs,l=>l.SpanId==e.SpanId);});
        Assert.Equal(generator.Enumerate(10).Select(JsonConvert.SerializeObject),generator.EnumerateParallel(10,4).Select(JsonConvert.SerializeObject));
        using var small=new MockContext(new MockOptions{Limits=new MockLimits{MaxCollectionSize=5}});
        Assert.Throws<MockLimitException>(()=>small.Templates.RequestTrace(2));
    }
}
