using System;
using System.Collections.Generic;

namespace Bing.MockData.Models
{
    /// <summary>模拟权限定义；代码在同一组织内唯一。</summary>
    public sealed class Permission
    {
        /// <summary>权限稳定代码。</summary>
        public string Code { get; set; }
        /// <summary>权限展示名称。</summary>
        public string Name { get; set; }
    }
    /// <summary>员工和角色的关联记录。</summary>
    public sealed class EmployeeRole
    {
        /// <summary>组织中的员工标识。</summary>
        public long EmployeeId { get; set; }
        /// <summary>组织中的角色标识。</summary>
        public long RoleId { get; set; }
    }
    /// <summary>角色和权限的关联记录。</summary>
    public sealed class RolePermission
    {
        /// <summary>组织中的角色标识。</summary>
        public long RoleId { get; set; }
        /// <summary>组织中的权限代码。</summary>
        public string PermissionCode { get; set; }
    }
    /// <summary>有界组织场景配置；模板创建时复制配置和定义。</summary>
    public sealed class OrganizationOptions
    {
        /// <summary>部门数量，至少一个。</summary>
        public int Departments { get; set; } = 5;
        /// <summary>员工数量。</summary>
        public int Employees { get; set; } = 20;
        /// <summary>每位员工分配的不同角色数量。</summary>
        public int RolesPerEmployee { get; set; } = 1;
        /// <summary>权限定义；为空时使用 read/write 默认权限。</summary>
        public IReadOnlyList<Permission> Permissions { get; set; }
        /// <summary>角色定义及权限代码；为空时使用 reader/editor。</summary>
        public IReadOnlyList<Role> Roles { get; set; }
    }
    public sealed partial class Organization
    {
        /// <summary>本组织内的完整权限定义。</summary>
        public IReadOnlyList<Permission> Permissions { get; set; }
        /// <summary>与员工 RoleIds 一致的关联表。</summary>
        public IReadOnlyList<EmployeeRole> EmployeeRoles { get; set; }
        /// <summary>与角色 Permissions 一致的关联表。</summary>
        public IReadOnlyList<RolePermission> RolePermissions { get; set; }
    }
    /// <summary>模拟仓库，包含属于该仓库的库位。</summary>
    public sealed class Warehouse
    {
        /// <summary>仓库代码。</summary>
        public string Code { get; set; }
        /// <summary>仓库名称。</summary>
        public string Name { get; set; }
        /// <summary>仓库的库位。</summary>
        public IReadOnlyList<StorageLocation> Locations { get; set; }
    }
    /// <summary>模拟库位，通过仓库代码建立归属。</summary>
    public sealed class StorageLocation
    {
        /// <summary>库位代码。</summary>
        public string Code { get; set; }
        /// <summary>所属仓库代码。</summary>
        public string WarehouseCode { get; set; }
    }
    /// <summary>包裹对订单明细的数量分配。</summary>
    public sealed class ParcelLine
    {
        /// <summary>原订单中的零基明细序号，区分重复商品。</summary>
        public int OrderItemIndex { get; set; }
        /// <summary>原订单明细引用。</summary>
        public OrderItem Item { get; set; }
        /// <summary>分配到该包裹的数量。</summary>
        public int Quantity { get; set; }
    }
    /// <summary>包裹及其订单明细分配。</summary>
    public sealed class Parcel
    {
        /// <summary>场景内唯一包裹编号。</summary>
        public string Number { get; set; }
        /// <summary>发出库位。</summary>
        public StorageLocation Origin { get; set; }
        /// <summary>包裹内容，不超出原订单数量。</summary>
        public IReadOnlyList<ParcelLine> Lines { get; set; }
    }
    /// <summary>引用一个包裹的模拟运单。</summary>
    public sealed class Waybill
    {
        /// <summary>场景内唯一运单号。</summary>
        public string Number { get; set; }
        /// <summary>关联包裹。</summary>
        public Parcel Parcel { get; set; }
        /// <summary>按时间排序的物流事件。</summary>
        public IReadOnlyList<ShipmentEvent> Events { get; set; }
        /// <summary>实际签收时间；未签收时为空。</summary>
        public DateTimeOffset? DeliveredAt { get; set; }
        /// <summary>是否已有签收时间。</summary>
        public bool Delivered => DeliveredAt.HasValue;
    }
    /// <summary>一个订单的有界仓储发运场景。</summary>
    public sealed class Fulfillment
    {
        /// <summary>关联原订单。</summary>
        public Order Order { get; set; }
        /// <summary>发出仓库。</summary>
        public Warehouse Warehouse { get; set; }
        /// <summary>订单拆分后的包裹。</summary>
        public IReadOnlyList<Parcel> Parcels { get; set; }
        /// <summary>与包裹一一对应的运单。</summary>
        public IReadOnlyList<Waybill> Waybills { get; set; }
    }
    /// <summary>库存事件类型；数量的符号与类型一致。</summary>
    public enum StockMovementKind
    {
        /// <summary>入库。</summary>
        Receipt,
        /// <summary>出库。</summary>
        Issue
    }
    /// <summary>同一请求跟踪中的业务事件。</summary>
    public sealed class TraceEvent
    {
        /// <summary>共享跟踪标识。</summary>
        public string TraceId { get; set; }
        /// <summary>事件所属 Span。</summary>
        public string SpanId { get; set; }
        /// <summary>事件时间。</summary>
        public DateTimeOffset Timestamp { get; set; }
        /// <summary>事件名称。</summary>
        public string Name { get; set; }
    }
    /// <summary>有界的单请求调用链，所有引用均限制在本场景。</summary>
    public sealed class RequestTrace
    {
        /// <summary>所有日志和事件共享的跟踪标识。</summary>
        public string TraceId { get; set; }
        /// <summary>根 Span 在首位，后续日志引用已存在的父 Span。</summary>
        public IReadOnlyList<RequestLog> Logs { get; set; }
        /// <summary>全链路按时间排序的事件。</summary>
        public IReadOnlyList<TraceEvent> Events { get; set; }
    }
}
