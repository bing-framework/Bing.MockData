using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.MockData.Models
{
    public enum UserStatus { Active, Inactive, Suspended }
    public sealed partial class UserProfile
    { public string Username {get;set;} public string Nickname {get;set;} public string Biography {get;set;} public UserStatus Status {get;set;} public DateTimeOffset RegisteredAt {get;set;} public DateTimeOffset LastLoginAt {get;set;} public string AvatarSvg {get;set;} }
    public sealed partial class CompanyProfile
    { public string ShortName {get;set;} public string Industry {get;set;} public string CreditCode {get;set;} }
    public sealed partial class Product
    { public ProductCategory Category {get;set;} public string Brand {get;set;} public string Sku {get;set;} public int Stock {get;set;} public string Barcode {get;set;} public string Description {get;set;} public string Currency {get;set;}="CNY"; public int CurrencyDecimalPlaces {get;set;}=2; }
    public enum OrderStatus { Pending, Paid, Shipped, Completed, Cancelled, PartiallyRefunded, Refunded }
    public sealed class PaymentRecord
    { public string Id {get;set;} public string Currency {get;set;} public decimal Amount {get;set;} public DateTimeOffset Timestamp {get;set;} }
    public sealed partial class Order
    {
        public string Number {get;set;} public OrderStatus Status {get;set;} public string Currency {get;set;}="CNY"; public int CurrencyDecimalPlaces {get;set;}=2;
        public decimal Discount {get;set;} public decimal Shipping {get;set;} public decimal Tax {get;set;}
        public List<PaymentRecord> Payments {get;set;}=new List<PaymentRecord>(); public List<PaymentRecord> Refunds {get;set;}=new List<PaymentRecord>();
        public decimal PaidAmount=>Payments.Sum(x=>x.Amount); public decimal RefundedAmount=>Refunds.Sum(x=>x.Amount);
        public DateTimeOffset CreatedAt {get;set;} public DateTimeOffset? ShippedAt {get;set;} public DateTimeOffset? CompletedAt {get;set;} public DateTimeOffset? CancelledAt {get;set;}
    }
    public sealed class Department
    { public long Id {get;set;} public long? ParentId {get;set;} public string Name {get;set;} }
    public sealed class Employee
    { public long Id {get;set;} public long DepartmentId {get;set;} public string Name {get;set;} public string Job {get;set;} public IReadOnlyList<long> RoleIds {get;set;} }
    public sealed class Role
    { public long Id {get;set;} public string Name {get;set;} public IReadOnlyList<string> Permissions {get;set;} }
    public sealed partial class Organization
    { public IReadOnlyList<Department> Departments {get;set;} public IReadOnlyList<Employee> Employees {get;set;} public IReadOnlyList<Role> Roles {get;set;} }
    public sealed class StockMovement
    { public DateTimeOffset Timestamp {get;} public int Quantity {get;} public long Balance {get;}
        /// <summary>事件类型，零增量视为入库。</summary>
        public StockMovementKind Kind => Quantity<0?StockMovementKind.Issue:StockMovementKind.Receipt;
        /// <summary>关联商品；旧的纯数量账本为空。</summary>
        public Product Product {get;}
        /// <summary>关联库位；旧的纯数量账本为空。</summary>
        public StorageLocation Location {get;}
        internal StockMovement(DateTimeOffset t,int q,long b,Product product=null,StorageLocation location=null){Timestamp=t;Quantity=q;Balance=b;Product=product;Location=location;} }
    public sealed class InventoryLedger
    { public int Opening {get;} public IReadOnlyList<StockMovement> Movements {get;} public long Closing=>Movements.Count==0?Opening:Movements[Movements.Count-1].Balance; /// <summary>本账本的商品引用。</summary>
        public Product Product {get;}
        /// <summary>本账本的库位引用。</summary>
        public StorageLocation Location {get;}
        internal InventoryLedger(int opening,List<StockMovement> movements,Product product=null,StorageLocation location=null){Opening=opening;Movements=movements.AsReadOnly();Product=product;Location=location;} }
    public sealed class ShipmentEvent
    { public string Status {get;set;} public DateTimeOffset Timestamp {get;set;} }
    public sealed class Shipment
    { public string TrackingNumber {get;set;} public Order Order {get;set;} public IReadOnlyList<OrderItem> Items {get;set;} public string Warehouse {get;set;} public string Bin {get;set;} public bool Delivered {get;set;} public DateTimeOffset? DeliveredAt {get;set;} public IReadOnlyList<ShipmentEvent> Events {get;set;} }
    public sealed class RequestLog
    { public string TraceId {get;set;} public string SpanId {get;set;}
        /// <summary>关联跟踪的父 Span；独立日志或根 Span 为空。</summary>
        public string ParentSpanId {get;set;}
        public HttpExchange Exchange {get;set;} public string Message {get;set;} }
}
