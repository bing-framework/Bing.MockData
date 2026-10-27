# 领域关系补齐示例

本文示例使用 .NET 10/C#。核心场景依赖 `Bing.MockData`；离线目录需要 `Bing.MockData.Catalogs`；教育、医疗、设备和 Schema 示例分别需要 `Bing.MockData.Education`、`Bing.MockData.Healthcare`、`Bing.MockData.Devices` 和 `Bing.MockData.Schema`。先将目录数据包解压到应用可读目录，以下 `dataDir` 指向该目录。

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Bing.MockData;
using Bing.MockData.Catalogs;
using Bing.MockData.Devices;
using Bing.MockData.Education;
using Bing.MockData.Healthcare;
using Bing.MockData.Models;
using Bing.MockData.Schema;

var dataDir = Path.Combine(AppContext.BaseDirectory, "catalog-data");
var options = new MockOptions
{
    Seed = 73,
    ReferenceTime = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero)
}.UseCatalogFiles(new[]
{
    new CatalogFile("institutions", Path.Combine(dataDir, "ror.sqlite3")),
    new CatalogFile("books", Path.Combine(dataDir, "crossref-books.sqlite3")),
    new CatalogFile("diagnoses", Path.Combine(dataDir, "icd10cm.sqlite3")),
    new CatalogFile("medications", Path.Combine(dataDir, "rxnorm-cpc.sqlite3")),
    new CatalogFile("devices", Path.Combine(dataDir, "pci-ids.sqlite3"))
});
using var context = new MockContext(options);
```

目录挂载是离线的，路径型连接由上下文拥有并在释放时关闭。每个目录记录保留来源身份；由模板组合出的人员、选课、就诊、测量和物流事件仍是模拟数据。

## 组织、角色和权限

`OrganizationOptions` 可提供权限代码及角色授权。生成结果包含部门树、员工角色和角色权限关联表；所有关联都指向本组织内已有记录。

```csharp
var organization = context.Templates.Organization(new OrganizationOptions
{
    Departments = 4,
    Employees = 12,
    RolesPerEmployee = 1,
    Permissions = new[]
    {
        new Permission { Code = "orders.read", Name = "查看订单" },
        new Permission { Code = "orders.refund", Name = "处理退款" }
    },
    Roles = new[]
    {
        new Role
        {
            Id = 7,
            Name = "order-operator",
            Permissions = new[] { "orders.read", "orders.refund" }
        }
    }
}).Build().Generate();

var assignment = organization.EmployeeRoles[0];
var employee = organization.Employees.Single(x => x.Id == assignment.EmployeeId);
var role = organization.Roles.Single(x => x.Id == assignment.RoleId);
```

输入的权限和角色定义会在创建模板时复制。部门、员工、角色或权限集合超过上下文预算时会在生成前失败。

## 包裹和库存

物流模板接收已有已发货订单和仓库；每条订单明细只分配一次，包裹有对应运单。库存流水按指定商品和库位生成，逐笔余额不会为负。

```csharp
var users = context.Templates.User().Build().GenerateMany(2);
var products = context.Templates.Product(new CurrencyInfo("CNY", 2)).Build().GenerateMany(3);
var order = context.Templates.Order(users, products, minItems: 3, maxItems: 3,
    status: OrderStatus.Shipped).Build().Generate();
var warehouse = context.Templates.Warehouse(locations: 3, code: "WH-EAST").Build().Generate();
var fulfillment = context.Templates.Fulfillment(order, warehouse, maxLinesPerParcel: 2).Build().Generate();

var inventory = context.Templates.Inventory(products[0], fulfillment.Warehouse.Locations[0],
    opening: 0, movements: 20).Build().Generate();
long endingBalance = inventory.Closing;
```

`Fulfillment.Order` 和包裹行引用传入的订单及其明细，`InventoryLedger.Product` 引用传入商品。仓库库位会在构建物流模板时复制，库存库位也会作为独立快照保存。调用方应在生成期间保持输入实体稳定，并按模型公开的引用检查关联；模板不会把对象所有权转交给上下文。

## 请求跟踪

`RequestTrace` 为一组日志和事件生成共享 TraceId、父子 Span 及按时间排序的开始/完成事件。

```csharp
var trace = context.Templates.RequestTrace(spans: 4, domain: "example.test")
    .Build().Generate();

var traceId = trace.TraceId;
var rootSpan = trace.Logs.Single(x => x.ParentSpanId == null);
var orderedEvents = trace.Events.OrderBy(x => x.Timestamp).ToArray();
```

链路作为单个有界场景生成，不维护跨记录的请求状态。`domain` 仅用于生成示例 URL，不表示真实服务。

## 教育班级和图书详情

机构记录来自已挂载的 ROR 目录；专业、班级、课程、学生和成绩是模拟记录。`Cohort` 中选课不重复，所有学生及课程引用都属于返回的场景。`BookDetails` 将 Crossref 记录中的已有字段映射为结构化作者、出版商、ISBN 和出版日期，源数据缺少的字段仍为空。

```csharp
var institution = context.Education().Institution("US");
var major = context.Education().Major().Build().Generate();
var cohort = context.Education().Cohort(institution, major,
    classes: 2, studentsPerClass: 4, courses: 3, coursesPerStudent: 2).Build().Generate();

var bookRecord = context.Education().Book();
var bookDetails = context.Education().BookDetails(bookRecord);
var sourceDoi = bookDetails.Doi;
```

`EducationCohort.Institution` 和班级中的机构字段保留传入的目录记录引用。专业配置在创建模板时复制；之后修改原 `Major` 不会改变已创建的生成器。

## 就诊药品选项

`EncounterOptions` 默认生成两个检查和一个药品引用。药品数量大于零时必须挂载 `medications` 目录；如需保留仅使用诊断目录的旧行为，显式设为 `Medications = 0`。

```csharp
var patient = context.Healthcare().Patient().Build().Generate();
var encounter = context.Healthcare().Encounter(patient, new EncounterOptions
{
    Examinations = 2,
    Medications = 1
}).Build().Generate();

var diagnosisVersion = encounter.DiagnosisDataVersion;
var medicationVersions = encounter.Medications.Select(x => x.DataVersion).Distinct().ToArray();
```

兼容没有药品目录的调用方式：

```csharp
var diagnosisOnly = context.Healthcare().Encounter(patient, new EncounterOptions
{
    Examinations = 2,
    Medications = 0
}).Build().Generate();
```

`Encounter.Patient` 是传入的同一患者对象。诊断和药品各自引用目录记录；模板不会推导诊断与药品之间未经定义的临床关系。选项在创建模板时复制。

## 设备测量选项

`MeasurementOptions` 控制指标、单位、范围、起始时间和相邻记录间隔。模板在创建时冻结设备序列号和选项；`At(index)` 的时间按序号直接计算。

```csharp
var device = context.Devices().Device().Build().Generate();
var measurements = context.Devices().Measurement(device, new MeasurementOptions
{
    Metric = "humidity",
    Unit = "%",
    Minimum = 0,
    Maximum = 100,
    StartAt = context.ReferenceTime.AddHours(-1),
    Interval = TimeSpan.FromMinutes(5)
}).Build();

var reading = measurements.At(3);
```

该指标是调用方指定的模拟测量，不表示设备目录证明该型号支持湿度传感器。测量值也不会被写回 `Device`。

## 短字符串 AutoBuilder

自动构建器按 DataAnnotations 生成并验证短字符串约束。可提前判断的长度冲突在创建生成器时失败；其他约束组合仍在有界尝试后验证，失败携带成员和约束信息。

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class ShortCodeDto
{
    [Required, StringLength(3, MinimumLength = 1)]
    public string Code { get; set; }

    [EmailAddress, MaxLength(10)]
    public string Email { get; set; }
}
```

```csharp
var codes = context.AutoBuilder<ShortCodeDto>().Build().GenerateMany(20);
```

应用中将 DTO 类型放在独立源文件中；`Code` 的结果长度始终满足 1 到 3 个字符。

## JSON Schema format 断言

`format` 默认作为注解处理。显式设置 `SchemaOptions.AssertFormat = true` 后，格式成为生成和验证必须满足的约束。

```csharp
var schemaOptions = new MockOptions { Seed = 74 }.UseSchema(
    "uuid-value",
    """{"type":"string","format":"uuid"}""",
    limits: new SchemaOptions { AssertFormat = true });
using var schemaContext = new MockContext(schemaOptions);
var uuidJson = schemaContext.Schema("uuid-value").Generate().Json;
```

样本经过完整 Schema 验证。外部 `$ref` 或 `$dynamicRef` 文档必须预加载；格式约束也受搜索预算限制。

## CLDR 版本和重放

CLDR 目录当前身份为 `49.0.0-import-v2`，包含关系中引用到的历史币种；找不到本地化名称的地区保留来源代码并标记缺名。目录版本会参与重放身份。

```csharp
using var cldr = SqliteMockCatalog.Open(Path.Combine(dataDir, "cldr.sqlite3"));
using var currencyContext = new MockContext(new MockOptions().UseCurrencyCatalog(cldr));
var yen = currencyContext.Finance.Currency("JPY");
var cldrVersion = currencyContext.Replay.ExtensionVersions["currency-metadata"];
```

当前重放契约为 `domain-v6`，内置数据身份为 `china-2025-12-31-domain-v5`；Schema 搜索身份为 `schema-search-v5`，目录集合身份为 `catalog-set-v2`。旧 `domain-v1`、`domain-v2` 等历史记录不受当前契约支持，`ReplayInfo.ToOptions()` 会因身份不兼容而抛出 `InvalidOperationException`；不要只改版本标记。历史结果能否复现取决于原始完整运行环境是否仍可用。迁移到当前版本时，应使用原始业务输入重新生成并保存新的重放信息。
