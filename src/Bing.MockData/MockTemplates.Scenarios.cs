using System;
using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Models;

namespace Bing.MockData
{
    public sealed partial class MockTemplates
    {
        private static void ValidateOrder(MockContext c,Order order)
        {
            if(order.Items==null||order.Items.Any(i=>i.Product==null||i.Quantity<1||i.UnitPrice<0||i.Product.Currency!=order.Currency)||order.Discount<0||order.Shipping<0||order.Tax<0||order.Discount>order.Items.Sum(x=>x.Subtotal))throw new InvalidOperationException("订单金额或币种约束冲突。");
            if(order.CurrencyDecimalPlaces<0||order.CurrencyDecimalPlaces>28||order.Items.Any(i=>i.Product.CurrencyDecimalPlaces!=order.CurrencyDecimalPlaces)||new[]{order.Total,order.Discount,order.Tax,order.Shipping}.Concat(order.Items.Select(i=>i.UnitPrice)).Concat(order.Payments.Concat(order.Refunds).Select(p=>p.Amount)).Any(value=>decimal.Round(value,order.CurrencyDecimalPlaces)!=value))throw new InvalidOperationException("订单金额精度与币种不一致。");
            bool shipped=order.Status==OrderStatus.Shipped||order.Status==OrderStatus.Completed||order.Status==OrderStatus.PartiallyRefunded||order.Status==OrderStatus.Refunded;
            bool completed=order.Status==OrderStatus.Completed||order.Status==OrderStatus.PartiallyRefunded||order.Status==OrderStatus.Refunded;
            if(!Enum.IsDefined(typeof(OrderStatus),order.Status)||shipped!=order.ShippedAt.HasValue||completed!=order.CompletedAt.HasValue||(order.Status==OrderStatus.Cancelled)!=order.CancelledAt.HasValue)throw new InvalidOperationException("订单状态与事件不一致。");
            if(order.Status==OrderStatus.Refunded?order.RefundedAmount!=order.PaidAmount:order.Status==OrderStatus.PartiallyRefunded?order.RefundedAmount<=0||order.RefundedAmount>=order.PaidAmount:order.Refunds.Count!=0)throw new InvalidOperationException("退款记录与订单状态不一致。");
            if(order.Refunds.Any(r=>order.Payments.Count==0||r.Timestamp<order.Payments.Max(p=>p.Timestamp))||order.ShippedAt.HasValue&&order.Payments.Any(p=>p.Timestamp>order.ShippedAt.Value)||order.CancelledAt<order.CreatedAt)throw new InvalidOperationException("订单事件顺序冲突。");
            bool unpaid=order.Status==OrderStatus.Pending||order.Status==OrderStatus.Cancelled;
            if(unpaid?order.Payments.Count!=0:order.PaidAmount!=order.Total)throw new InvalidOperationException("支付金额与订单状态不一致。");
            if(order.RefundedAmount<0||order.RefundedAmount>order.PaidAmount)throw new InvalidOperationException("退款金额越界。");
            if(order.Payments.Concat(order.Refunds).Any(x=>x.Amount<0||x.Currency!=order.Currency||x.Timestamp<order.CreatedAt||x.Timestamp>c.ReferenceTime))throw new InvalidOperationException("支付或退款事件无效。");
            if(order.CreatedAt>c.ReferenceTime||order.ShippedAt>c.ReferenceTime||order.CompletedAt>c.ReferenceTime||order.CancelledAt>c.ReferenceTime||order.ShippedAt<order.CreatedAt||order.CompletedAt<order.ShippedAt)throw new InvalidOperationException("订单时间顺序冲突。");
        }
        private static List<PaymentRecord> PaymentRecords(MockContext c, Order order)
        {
            if(order.Total<0)throw new InvalidOperationException("订单总额不得为负数。");
            if(order.Status==OrderStatus.Pending||order.Status==OrderStatus.Cancelled)return new List<PaymentRecord>();
            return new List<PaymentRecord>{new PaymentRecord{Id=c.Identifiers.Business("PAY-"),Currency=order.Currency,Amount=order.Total,Timestamp=order.CreatedAt.AddHours(1)}};
        }
        private static List<PaymentRecord> RefundRecords(MockContext c, Order order)
        {
            if(order.Status!=OrderStatus.PartiallyRefunded&&order.Status!=OrderStatus.Refunded)return new List<PaymentRecord>();
            decimal amount=order.Status==OrderStatus.Refunded?order.PaidAmount:decimal.Round(order.PaidAmount/2,order.CurrencyDecimalPlaces,MidpointRounding.ToEven);
            if(order.Status==OrderStatus.PartiallyRefunded&&(amount<=0||amount>=order.PaidAmount))throw new InvalidOperationException("金额不足以生成一个有效的部分退款刻度。");
            return new List<PaymentRecord>{new PaymentRecord{Id=c.Identifiers.Business("REF-"),Currency=order.Currency,Amount=amount,Timestamp=order.CreatedAt.AddDays(7)}};
        }
        /// <summary>生成有界部门、员工、角色关系；部门仅引用前序节点，因此没有环。</summary>
        public MockBuilder<Organization> Organization(int departments=5,int employees=20)
            =>Organization(new OrganizationOptions{Departments=departments,Employees=employees});
        /// <summary>从已有订单生成包裹，引用原订单明细，不复制成无关联商品。</summary>
        public MockBuilder<Shipment> Shipment(Order order,bool delivered=true)
        {
            if(order==null)throw new ArgumentNullException(nameof(order));if(order.Status==OrderStatus.Pending||order.Status==OrderStatus.Cancelled||order.ShippedAt==null)throw new ArgumentException("订单必须已发货。",nameof(order));
            if(order.ShippedAt>_context.ReferenceTime)throw new ArgumentException("实际发货时间不得在未来。");
            DomainGuard.Count(_context,order.Items.Count);DomainGuard.Count(_context,delivered?3:2);var items=order.Items.ToArray();
            return _context.Builder<Shipment>().ConstructUsing(c=>
            {
                var from=order.ShippedAt.Value;var end=c.ReferenceTime;var middle=from.AddTicks((end-from).Ticks/2);
                var events=new List<ShipmentEvent>{new ShipmentEvent{Status="collected",Timestamp=from},new ShipmentEvent{Status="in_transit",Timestamp=middle}};
                if(delivered)events.Add(new ShipmentEvent{Status="delivered",Timestamp=end});
                return new Shipment{TrackingNumber=c.Identifiers.Business("SHIP-"),Order=order,Items=Array.AsReadOnly(items),Warehouse="WH-"+c.Numbers.Int(1,10),Bin="A-"+c.Numbers.Int(1,100),Delivered=delivered,DeliveredAt=delivered?(DateTimeOffset?)end:null,Events=events.AsReadOnly()};
            });
        }
        public MockBuilder<InventoryLedger> Inventory(int opening=100,int movements=20)
        {
            if(opening<0)throw new ArgumentOutOfRangeException(nameof(opening));DomainGuard.Count(_context,movements);
            return _context.Builder<InventoryLedger>().ConstructUsing(c=>{long balance=opening;var rows=new List<StockMovement>();for(int i=0;i<movements;i++){var f=c.ForField("movement["+i+"]");int q=f.Numbers.Int(-(int)Math.Min(balance,100),100);balance=checked(balance+q);rows.Add(new StockMovement(c.ReferenceTime.AddMinutes(i-movements),q,balance));}return new InventoryLedger(opening,rows);});
        }
        public MockBuilder<RequestLog> RequestLog(string domain="example.com")
        { TextData.ValidateDomain(domain);return _context.Builder<RequestLog>().RuleFor(x=>x.TraceId,c=>c.Text.Random(32,32,"0123456789abcdef")).RuleFor(x=>x.SpanId,c=>c.Text.Random(16,16,"0123456789abcdef")).RuleFor(x=>x.Exchange,c=>c.Network.Exchange(domain)).RuleFor(x=>x.Message,(_,x)=>x.Exchange.Method+" "+x.Exchange.Url+" "+x.Exchange.StatusCode); }
    }
}
