using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bing.MockData.Models;

namespace Bing.MockData
{
    public sealed partial class MockTemplates
    {
        private static void GraphBudget(MockContext context,long entries,int extraDepth)
        {
            context.Check();
            if(entries>context.Budget.MaxCollectionSize)throw new MockLimitException(nameof(MockLimits.MaxCollectionSize),context.Budget.MaxCollectionSize);
            if(context.Depth+extraDepth>context.Budget.MaxDepth)throw new MockLimitException(nameof(MockLimits.MaxDepth),context.Budget.MaxDepth);
        }

        /// <summary>生成可配置权限的组织及完整关联表；配置在调用时冻结。</summary>
        public MockBuilder<Organization> Organization(OrganizationOptions options)
        {
            if(options==null)throw new ArgumentNullException(nameof(options));
            int departments=options.Departments,employees=options.Employees,rolesPerEmployee=options.RolesPerEmployee;
            DomainGuard.Count(_context,departments);DomainGuard.Count(_context,employees);
            if(departments<1||rolesPerEmployee<0)throw new ArgumentOutOfRangeException(nameof(options));
            var permissionInput=options.Permissions??new[]{new Permission{Code="read",Name="Read"},new Permission{Code="write",Name="Write"}};
            var roleInput=options.Roles??new[]{new Role{Id=1,Name="reader",Permissions=new[]{"read"}},new Role{Id=2,Name="editor",Permissions=new[]{"read","write"}}};
            DomainGuard.Count(_context,permissionInput.Count);DomainGuard.Count(_context,roleInput.Count);
            long entries=(long)departments+employees+permissionInput.Count+roleInput.Count+(long)employees*rolesPerEmployee;
            foreach(var role in roleInput)
            {
                _context.Check();if(role==null||role.Permissions==null)throw new ArgumentException("角色及权限列表不能为空。");
                entries+=role.Permissions.Count;GraphBudget(_context,entries,2);
            }
            GraphBudget(_context,entries,2);
            var permissions=permissionInput.Select(p=>p==null?throw new ArgumentException("权限不能为 null。"):new Permission{Code=DomainGuard.Text(_context,p.Code??""),Name=DomainGuard.Text(_context,p.Name??"")}).ToArray();
            if(permissions.Any(p=>string.IsNullOrWhiteSpace(p.Code))||permissions.Select(p=>p.Code).Distinct(StringComparer.Ordinal).Count()!=permissions.Length)throw new ArgumentException("权限代码必须非空且唯一。");
            var roles=roleInput.Select(r=>
            {
                if(r==null||r.Permissions==null)throw new ArgumentException("角色及权限列表不能为空。");
                DomainGuard.Count(_context,r.Permissions.Count);
                return new Role{Id=r.Id,Name=DomainGuard.Text(_context,r.Name??""),Permissions=Array.AsReadOnly(r.Permissions.ToArray())};
            }).ToArray();
            var permissionKeys=new HashSet<string>(permissions.Select(p=>p.Code),StringComparer.Ordinal);
            if(roles.Select(r=>r.Id).Distinct().Count()!=roles.Length||roles.Any(r=>r.Id<1||string.IsNullOrWhiteSpace(r.Name)||r.Permissions.Distinct().Count()!=r.Permissions.Count||r.Permissions.Any(p=>!permissionKeys.Contains(p))))throw new ArgumentException("角色标识或权限引用无效。");
            if(rolesPerEmployee>roles.Length)throw new ArgumentException("每位员工的角色数量超过可用角色。");
            return _context.Builder<Organization>().ConstructUsing(c=>
            {
                GraphBudget(c,entries,1);
                var ds=new List<Department>();var depths=new List<int>();
                for(int i=0;i<departments;i++)
                {
                    var field=c.ForField("department["+i+"]");field.Check();int parent=0;
                    if(i>0)
                    {
                        var eligible=Enumerable.Range(0,i).Where(n=>depths[n]<c.Budget.MaxDepth-c.Depth).ToArray();
                        if(eligible.Length==0)throw new MockLimitException(nameof(MockLimits.MaxDepth),c.Budget.MaxDepth);
                        parent=field.Collections.Pick(eligible);
                    }
                    ds.Add(new Department{Id=i+1,ParentId=i==0?(long?)null:parent+1,Name=field.Companies.Department()});depths.Add(i==0?1:depths[parent]+1);
                }
                var roleCopies=roles.Select(r=>new Role{Id=r.Id,Name=r.Name,Permissions=Array.AsReadOnly(r.Permissions.ToArray())}).ToArray();
                var staff=new List<Employee>();var employeeRoles=new List<EmployeeRole>();
                for(int i=0;i<employees;i++)
                {
                    var f=c.ForField("employee["+i+"]");f.Check();var selected=f.ForField("roles").Collections.Sample(roleCopies,rolesPerEmployee).Select(r=>r.Id).ToArray();
                    staff.Add(new Employee{Id=i+1,DepartmentId=f.ForField("department").Collections.Pick(ds).Id,Name=f.ForField("name").Text.Name(),Job=f.ForField("job").People.Job(),RoleIds=Array.AsReadOnly(selected)});
                    foreach(long role in selected)employeeRoles.Add(new EmployeeRole{EmployeeId=i+1,RoleId=role});
                }
                return new Organization
                {
                    Departments=ds.AsReadOnly(),Employees=staff.AsReadOnly(),Roles=Array.AsReadOnly(roleCopies),
                    Permissions=Array.AsReadOnly(permissions.Select(p=>new Permission{Code=p.Code,Name=p.Name}).ToArray()),EmployeeRoles=employeeRoles.AsReadOnly(),
                    RolePermissions=Array.AsReadOnly(roleCopies.SelectMany(r=>r.Permissions.Select(p=>new RolePermission{RoleId=r.Id,PermissionCode=p})).ToArray())
                };
            });
        }

        /// <summary>生成模拟仓库及属于该仓库的库位，不查询真实仓储设施。</summary>
        public MockBuilder<Warehouse> Warehouse(int locations=5,string code=null)
        {
            if(locations<1)throw new ArgumentOutOfRangeException(nameof(locations));
            GraphBudget(_context,(long)locations+1,2);if(code!=null)DomainGuard.Text(_context,code);
            if(code!=null&&string.IsNullOrWhiteSpace(code))throw new ArgumentException("仓库代码不能为空。",nameof(code));
            return _context.Builder<Warehouse>().ConstructUsing(c=>
            {
                GraphBudget(c,(long)locations+1,1);string key=code??c.Identifiers.Business("WH-");var bins=new List<StorageLocation>();
                for(int i=0;i<locations;i++){c.Check();bins.Add(new StorageLocation{Code=DomainGuard.Text(c,key+"-L"+(i+1).ToString("D3",CultureInfo.InvariantCulture)),WarehouseCode=key});}
                return new Warehouse{Code=key,Name=DomainGuard.Text(c,c.Locale=="zh-CN"?"模拟仓库 "+key:"Simulated warehouse "+key),Locations=bins.AsReadOnly()};
            });
        }

        /// <summary>将订单明细分配到包裹及运单，每条明细只分配一次；实体图受整体预算约束。</summary>
        public MockBuilder<Fulfillment> Fulfillment(Order order,Warehouse warehouse,int maxLinesPerParcel=3,bool delivered=true)
        {
            if(order==null)throw new ArgumentNullException(nameof(order));if(warehouse==null)throw new ArgumentNullException(nameof(warehouse));
            if(order.ShippedAt==null||order.ShippedAt>_context.ReferenceTime||order.Status==OrderStatus.Pending||order.Status==OrderStatus.Cancelled)throw new ArgumentException("需要已经发货的订单。",nameof(order));
            if(maxLinesPerParcel<1)throw new ArgumentOutOfRangeException(nameof(maxLinesPerParcel));
            if(order.Items==null||order.Items.Count==0||order.Items.Any(i=>i==null||i.Quantity<1||i.Product==null))throw new ArgumentException("订单明细无效。");
            DomainGuard.Count(_context,order.Items.Count);DomainGuard.Count(_context,maxLinesPerParcel);
            var frozenWarehouse=CopyWarehouse(_context,warehouse);var items=order.Items.ToArray();var quantities=items.Select(i=>i.Quantity).ToArray();
            var from=order.ShippedAt.Value;int parcels=(items.Length-1)/maxLinesPerParcel+1;
            long entries=1L+frozenWarehouse.Locations.Count+items.Length+(long)parcels*(delivered?5:4);
            GraphBudget(_context,entries,4);
            return _context.Builder<Fulfillment>().ConstructUsing(c=>
            {
                GraphBudget(c,entries,3);var site=CopyWarehouse(c,frozenWarehouse);var packages=new List<Parcel>();var bills=new List<Waybill>();
                for(int i=0;i<parcels;i++)
                {
                    var field=c.ForField("parcel["+i+"]");field.Check();var lines=new List<ParcelLine>();
                    for(int j=i*maxLinesPerParcel;j<Math.Min(items.Length,(long)(i+1)*maxLinesPerParcel);j++){field.Check();lines.Add(new ParcelLine{OrderItemIndex=j,Item=items[j],Quantity=quantities[j]});}
                    string suffix="-"+(i+1).ToString(CultureInfo.InvariantCulture);
                    var parcel=new Parcel{Number=DomainGuard.Text(field,c.Identifiers.Business("PKG-")+suffix),Origin=field.Collections.Pick(site.Locations),Lines=lines.AsReadOnly()};
                    DateTimeOffset end=c.ReferenceTime,middle=from.AddTicks((end-from).Ticks/2);
                    var events=new List<ShipmentEvent>{new ShipmentEvent{Status="collected",Timestamp=from},new ShipmentEvent{Status="in_transit",Timestamp=middle}};
                    if(delivered)events.Add(new ShipmentEvent{Status="delivered",Timestamp=end});
                    packages.Add(parcel);bills.Add(new Waybill{Number=DomainGuard.Text(field,c.Identifiers.Business("WAY-")+suffix),Parcel=parcel,Events=events.AsReadOnly(),DeliveredAt=delivered?(DateTimeOffset?)end:null});
                }
                return new Fulfillment{Order=order,Warehouse=site,Parcels=packages.AsReadOnly(),Waybills=bills.AsReadOnly()};
            });
        }

        private static Warehouse CopyWarehouse(MockContext context,Warehouse source)
        {
            if(string.IsNullOrWhiteSpace(source.Code)||source.Locations==null||source.Locations.Count==0)throw new ArgumentException("仓库代码和库位不能为空。");
            DomainGuard.Count(context,source.Locations.Count);DomainGuard.Text(context,source.Code);DomainGuard.Text(context,source.Name??"");
            if(source.Locations.Any(l=>l==null||string.IsNullOrWhiteSpace(l.Code)||l.WarehouseCode!=source.Code)||source.Locations.Select(l=>l.Code).Distinct(StringComparer.Ordinal).Count()!=source.Locations.Count)throw new ArgumentException("库位必须唯一且属于指定仓库。");
            return new Warehouse{Code=source.Code,Name=source.Name,Locations=Array.AsReadOnly(source.Locations.Select(l=>new StorageLocation{Code=DomainGuard.Text(context,l.Code),WarehouseCode=source.Code}).ToArray())};
        }

        /// <summary>生成指定商品、库位的入出库流水，余额不为负。期初量和事件数量在配置时冻结。</summary>
        public MockBuilder<InventoryLedger> Inventory(Product product,StorageLocation location,int opening=100,int movements=20)
        {
            if(product==null)throw new ArgumentNullException(nameof(product));if(location==null)throw new ArgumentNullException(nameof(location));
            if(opening<0)throw new ArgumentOutOfRangeException(nameof(opening));DomainGuard.Count(_context,movements);GraphBudget(_context,(long)movements+2,2);
            if(string.IsNullOrWhiteSpace(location.Code)||string.IsNullOrWhiteSpace(location.WarehouseCode))throw new ArgumentException("库位引用无效。",nameof(location));
            var frozen=new StorageLocation{Code=DomainGuard.Text(_context,location.Code),WarehouseCode=DomainGuard.Text(_context,location.WarehouseCode)};
            return _context.Builder<InventoryLedger>().ConstructUsing(c=>
            {
                GraphBudget(c,(long)movements+2,1);long balance=opening;var rows=new List<StockMovement>();var bin=new StorageLocation{Code=frozen.Code,WarehouseCode=frozen.WarehouseCode};
                for(int i=0;i<movements;i++)
                {
                    var field=c.ForField("movement["+i+"]");field.Check();bool receipt=balance==0||field.Numbers.Boolean();
                    int quantity=receipt?field.Numbers.Int(1,100):-field.Numbers.Int(1,(int)Math.Min(balance,100));balance=checked(balance+quantity);
                    rows.Add(new StockMovement(c.ReferenceTime.AddMinutes(i-movements),quantity,balance,product,bin));
                }
                return new InventoryLedger(opening,rows,product,bin);
            });
        }

        /// <summary>生成共享 TraceId 的调用链、父子 Span 和时间有序事件；不保留跨记录状态。</summary>
        public MockBuilder<RequestTrace> RequestTrace(int spans=5,string domain="example.com")
        {
            if(spans<1)throw new ArgumentOutOfRangeException(nameof(spans));TextData.ValidateDomain(domain);GraphBudget(_context,3L*spans,4);
            return _context.Builder<RequestTrace>().ConstructUsing(c=>
            {
                GraphBudget(c,3L*spans,3);string trace=c.ForField("trace").Text.Random(32,32,"0123456789abcdef");
                var logs=new List<RequestLog>();var depths=new List<int>();var events=new List<TraceEvent>();
                for(int i=0;i<spans;i++)
                {
                    var field=c.ForField("span["+i+"]");field.Check();RequestLog parent=null;int depth=1;
                    if(i>0){var eligible=Enumerable.Range(0,i).Where(n=>depths[n]<c.Budget.MaxDepth-c.Depth).ToArray();int index=field.Collections.Pick(eligible);parent=logs[index];depth=depths[index]+1;}
                    var exchange=field.Network.Exchange(domain);
                    exchange.RequestedAt=parent==null?c.ReferenceTime.AddMinutes(-5):parent.Exchange.RequestedAt.AddTicks((parent.Exchange.RespondedAt-parent.Exchange.RequestedAt).Ticks/4);
                    exchange.RespondedAt=parent==null?c.ReferenceTime.AddMinutes(-1):parent.Exchange.RespondedAt.AddTicks(-(parent.Exchange.RespondedAt-parent.Exchange.RequestedAt).Ticks/4);
                    string span=(i+1).ToString("x16",CultureInfo.InvariantCulture);
                    logs.Add(new RequestLog{TraceId=trace,SpanId=span,ParentSpanId=parent?.SpanId,Exchange=exchange,Message=DomainGuard.Text(field,exchange.Method+" "+exchange.Url+" "+exchange.StatusCode)});depths.Add(depth);
                    events.Add(new TraceEvent{TraceId=trace,SpanId=span,Timestamp=exchange.RequestedAt,Name="request.started"});
                    events.Add(new TraceEvent{TraceId=trace,SpanId=span,Timestamp=exchange.RespondedAt,Name="request.completed"});
                }
                return new RequestTrace{TraceId=trace,Logs=logs.AsReadOnly(),Events=Array.AsReadOnly(events.OrderBy(e=>e.Timestamp).ThenBy(e=>e.SpanId,StringComparer.Ordinal).ToArray())};
            });
        }
    }
}
