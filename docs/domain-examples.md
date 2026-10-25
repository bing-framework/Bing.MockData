# 全领域能力示例

关系场景、医疗选项、短字符串约束和 Schema 格式调用的补充示例见[领域关系补齐示例](domain-gap-examples.md)。

以下示例按 .NET 10/C# 写法展示当前 API。常见能力来自 `Bing.MockData`；GeoNames、CLDR、电话及 MIME 查询需要 `Bing.MockData.Catalogs`，四个行业门面各需要相应的独立扩展包。离线数据包需先解压到本地目录；库运行时不联网。示例中的每条数据都标记为模拟数据，目录记录只说明来源中存在相应记录。

## 通用上下文

```csharp
using Bing.MockData;
using Bing.MockData.Models;

var options = new MockOptions
{
    Seed = 42,
    ReferenceTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
    Locale = "zh-CN"
};
using var context = new MockContext(options);
```

固定 Seed、参考时间、Locale、同一库与运行时版本以及相同扩展版本时，可按 `ReplayInfo` 重建上下文。地区、电话和其他外部目录需重新挂载；重放元数据保存版本身份，不会复制目录数据。

## 1–12：字段与目录能力

### 1. 用户资料

```csharp
var user = context.Templates.User("example.test").Build().Generate();
// 用户名、个人资料、注册/登录时间、状态和 SVG 头像相互一致。
```

邮箱和 URL 默认使用 `example.com`。需要定制时传入由调用方控制的示例域名。`LastLoginAt` 不早于 `RegisteredAt`。

### 2. 企业、职位和员工

```csharp
var company = context.Templates.Company("example.test").Build().Generate();
var job = context.People.Job();
var department = context.Companies.Department();
```

中文企业的统一社会信用代码仅在地址包含县级代码时生成；其校验位遵循实现中的 GB 32100 计算。企业和职位名称均为模拟文本。

### 3. 中文/英文业务文案

```csharp
var title = context.Text.Business(BusinessTextKind.Title, maxLength: 80);
var review = context.Text.Business(BusinessTextKind.Review);
var paragraph = context.Text.BusinessParagraph(sentences: 4, maxLength: 600);
```

文案由带语义的词句模板组合，不通过随机汉字拼接。按 `Locale` 使用 `zh-CN` 或 `en-US`。

### 4. 商品与条码

```csharp
var product = context.Templates.Product(new CurrencyInfo("CNY", 2)).Build().Generate();
var ean13 = context.Commerce.Ean13();
var upc = context.Commerce.Upc();
```

商品分类包含兼容的名称、材质、规格和单位；EAN-13/UPC 校验位有效。随机条码不表示已分配给真实商品。

### 5. 金融、币种与流水

```csharp
var currency = context.Finance.Currency(); // 核心内置常见币种
var amount = context.Finance.Amount(currency, 10m, 500m);
var ledger = context.Finance.Ledger(20, openingBalance: 1000m, currency);
```

金额按币种小数位数抽取有效步长；流水余额由期初余额和每笔收支推导。要查询 CLDR 全量快照中的币种精度，见下方目录扩展示例，随后可使用 `context.Finance.Currency("JPY")`。

### 6. 电话号码

```csharp
var phone = context.Phone.Number("CN", PhoneNumberType.Mobile, extension: true);
// phone.CallingCode、phone.NationalNumber、phone.Extension 分开保存。
```

号码格式依据已挂载的 libphonenumber 元数据，但不承诺号码真实分配。国际主号码与分机字段分开。

### 7. 地址、邮编与坐标

```csharp
var address = context.Templates.Address("110000").Build().Generate();
var point = context.Geography.Coordinate();
var nearby = context.Geography.Nearby(point, radiusMeters: 250);
```

地址模板按中国行政区划快照选择同一路径；英文模板使用示例地址。WGS84 附近坐标满足球面距离上限，但不保证仍位于同一行政区。真实城市和邮编查询示例见目录扩展部分；缺少可靠地区映射时应按覆盖错误处理。

### 8. HTTP 与客户端

```csharp
var exchange = context.Network.Exchange("example.test");
var client = context.Network.Client();
var port = context.Network.Port(8000, 9000);
```

`HttpExchange` 将方法、状态、URL、客户端和请求/响应时间成组生成；可用 `DurationMilliseconds` 读取耗时。所有客户端、域名和响应均为模拟样本。

### 9. 文件元数据与 MIME

```csharp
var file = context.Files.Metadata("json", MockPathStyle.Posix, maxBytes: 1_000_000);
// file.Extension 与 file.MimeType 来自同一目录映射。
```

只生成文件名、路径、MIME、大小和时间元数据，不创建文件。Windows/POSIX 路径可选，`CreatedAt` 不晚于 `ModifiedAt`。MIME 数据库需要挂载，见下方示例。

### 10. 颜色与离线 SVG

```csharp
var color = context.Colors.Named();
string placeholder = context.Images.Placeholder(320, 180, "演示 & 预览", color);
string avatar = context.Images.Avatar("AB", 128);
```

颜色对象提供 HEX、RGB、RGBA、HSL 值；SVG 文本会进行 XML 转义，不访问网络或写入图片文件。

### 11. 结构化标识

```csharp
string ulid = context.Identifiers.Ulid(context.ReferenceTime);
string shortId = context.Identifiers.Short(12);
string orderNo = context.Identifiers.Business("ORD-");
```

ULID 时间由参数或上下文参考时间确定。`Short` 是随机标识，不承诺全局唯一；`Business` 按记录序号生成常量内存编号。

### 12. 异常/边界输入

```csharp
string badDate = context.Boundaries.Invalid(InvalidInputKind.Date);
string whitespace = context.Boundaries.Invalid(InvalidInputKind.Whitespace);
string overlongBoundary = context.Boundaries.Text(TextBoundary.Overlong, businessMaximumLength: 64);
```

异常值必须通过 `Boundaries` 显式请求。`InvalidInputKind.Unicode` 会返回孤立代理项，适合验证拒绝路径；它不代表正常 Unicode 文本输出。

## 离线目录挂载

基础目录数据包和许可文件见 [catalog-data-manifest.md](catalog-data-manifest.md)。下面把城市、两个邮编分片、CLDR、libphonenumber 和 mime-db 挂载到同一上下文；数据文件先从 `catalog-data.zip` 解压到 `dataDir`。每个显式传入的目录都转移给这个上下文，不能在其他上下文或另一个 options 配置中复用。

```csharp
using Bing.MockData.Catalogs;

var cityCatalog = SqliteMockCatalog.Open(Path.Combine(dataDir, "geonames-cities.sqlite3"));
var postal0 = SqliteMockCatalog.Open(Path.Combine(dataDir, "geonames-postal-0.sqlite3"));
var postal1 = SqliteMockCatalog.Open(Path.Combine(dataDir, "geonames-postal-1.sqlite3"));
var currencyCatalog = SqliteMockCatalog.Open(Path.Combine(dataDir, "cldr.sqlite3"));
var phoneCatalog = SqliteMockCatalog.Open(Path.Combine(dataDir, "libphonenumber.sqlite3"));
var mimeCatalog = SqliteMockCatalog.Open(Path.Combine(dataDir, "mime-db.sqlite3"));

var options = new MockOptions { Seed = 42 }
    .UseGeographicCatalog(cityCatalog, cities: true, ownsCatalog: true)
    .UseGeographicCatalogs(new IBingMockCatalog[] { postal0, postal1 }, ownsCatalogs: true)
    .UseCurrencyCatalog(currencyCatalog, ownsCatalog: true)
    .UsePhoneMetadataCatalog(phoneCatalog, ownsCatalog: true)
    .UseMimeTypeCatalog(mimeCatalog, ownsCatalog: true);
using var context = new MockContext(options);

var city = context.Geography.City("CN");
var postal = context.Geography.Postal("US");
var yen = context.Finance.Currency("JPY");
var phone = context.Phone.Number("CN", PhoneNumberType.Mobile);
var file = context.Files.Metadata("json");
```

每次查询都按目录过滤和调用方随机 selector 确定性抽样。`CatalogLocation` 保留来源记录的国家、地点、邮编、坐标及精度；没有匹配项时抛出 `CatalogCoverageException`。电话元数据描述可匹配的格式，不表示号码真实分配；MIME 目录描述扩展名关系，不验证文件内容。

## 13–17：场景、统计与约束

### 13. 订单、支付与退款

```csharp
var users = context.Templates.User().Build().GenerateMany(20);
var products = context.Templates.Product(new CurrencyInfo("CNY", 2)).Build().GenerateMany(10);
var order = context.Templates.Order(users, products, status: OrderStatus.PartiallyRefunded).Build().Generate();
```

订单引用既有用户和商品；行金额、币种精度、支付和退款均由规则验证。`Total = 明细金额 - Discount + Shipping + Tax`，是只读派生值；退款累计不会超过已支付金额。状态控制事件字段是否存在。其他场景可选 `Pending`、`Paid`、`Shipped`、`Completed`、`Cancelled` 或 `Refunded`。

### 14. 组织与权限

```csharp
var org = context.Templates.Organization(departments: 8, employees: 60).Build().Generate();
var departmentIds = org.Departments.Select(x => x.Id).ToHashSet();
```

部门仅关联已生成的前序父节点，员工引用有效部门和角色。组织大小受集合/深度预算限制，不保存跨批次的无界实体关系。

### 15. 物流与库存

```csharp
var shippedOrder = context.Templates.Order(users, products, status: OrderStatus.Shipped).Build().Generate();
var shipment = context.Templates.Shipment(shippedOrder, delivered: true).Build().Generate();
var inventory = context.Templates.Inventory(opening: 100, movements: 50).Build().Generate();
```

包裹引用订单及其明细，物流事件按时间排序；库存流水从期初余额推导，正常生成不会低于零。已发货订单必须含实际发货时间。

### 16. 分布、时间序列与请求日志

```csharp
double sample = context.Distributions.Normal(mean: 10, standardDeviation: 2);
var series = context.TimeSeries.Create(new TimeSeriesOptions
{
    Baseline = 20, Trend = 0.01, Amplitude = 3, Period = 24,
    Noise = 0.5, MissingProbability = 0.01, OutlierProbability = 0.002
});
var point = series.At(500);
var log = context.Templates.RequestLog("example.test").Build().Generate();
```

时间序列按记录索引计算，便于随机访问和并行重放。请求日志内的 TraceId/SpanId 与 HTTP 交换一起生成；不为任意独立记录伪造前序状态。

### 17. DataAnnotations 与 JSON Schema

```csharp
public sealed class CustomerDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(80, MinimumLength = 2)]
    public string Name { get; set; }

    [System.ComponentModel.DataAnnotations.Range(18, 99)]
    public int Age { get; set; }
}

var customer = context.AutoBuilder<CustomerDto>().Build().Generate();
```

`AutoBuilder<T>()` 根据 DataAnnotations 配置基础生成，并在输出前验证；需构造参数的类型应调用 `ConstructUsing`。显式规则覆盖自动规则。

Schema 需要 `Bing.MockData.Schema`：

```csharp
using Bing.MockData.Schema;

var schemaOptions = new MockOptions { Seed = 7 }
    .UseSchema("customer", """{"type":"object","required":["age"],"properties":{"age":{"type":"integer","minimum":18,"maximum":99}}}""");
using var schemaContext = new MockContext(schemaOptions);
var sample = schemaContext.Schema("customer").Generate();
bool valid = schemaContext.Extension<CompiledSchema>("schema:customer").Validate(sample.Json);
```

引用文档必须预加载；不自动访问 HTTP 或文件。每个样本会经完整 Schema 校验。`BudgetExhausted` 只表示搜索预算内未找到样本，不能据此断言 Schema 无解。

## 18–21：行业目录包

目录文件通过 `CatalogFile` 按路径挂载。每个上下文最多挂载 8 个目录文件，路径打开的 SQLite 连接由上下文拥有并在释放时关闭。各示例各自创建上下文，避免借用连接的生命周期混淆。

### 18. 交通：车辆、机场与航空公司

需要 `Bing.MockData.Transport`。`vehicles.sqlite3` 来自 VehiclesDB，`ourairports.sqlite3` 来自 OurAirports，`wikidata-airlines.sqlite3` 来自 Wikidata。

```csharp
using Bing.MockData.Transport;

var options = new MockOptions { Seed = 8 }.UseCatalogFiles(new[]
{
    new CatalogFile("vehicles", Path.Combine(dataDir, "vehicles.sqlite3")),
    new CatalogFile("airports", Path.Combine(dataDir, "ourairports.sqlite3")),
    new CatalogFile("airlines", Path.Combine(dataDir, "wikidata-airlines.sqlite3"))
});
using var context = new MockContext(options);
var vehicle = context.Transport().Vehicle("CN").Build().Generate();
var airports = context.Catalog("airports").Info;
```

`Coverage("vehicles")` 等可查看快照元数据。航班路径是模拟组合，并非真实航线；VIN 格式/校验位有效不代表车型编码与 VIN 有验证关联。

### 19. 教育：机构、图书、学生与选课

需要 `Bing.MockData.Education`。机构来自 ROR（研究组织目录，不是完整中小学目录），图书来自最多 100,000 条合格 Crossref 元数据，不包含摘要或正文。

```csharp
using Bing.MockData.Education;

var options = new MockOptions { Seed = 9 }.UseCatalogFiles(new[]
{
    new CatalogFile("institutions", Path.Combine(dataDir, "ror.sqlite3")),
    new CatalogFile("books", Path.Combine(dataDir, "crossref-books.sqlite3"))
});
using var context = new MockContext(options);
var institution = context.Education().Institution("US");
var book = context.Education().Book();
var student = context.Education().Student("US").Build().Generate();
```

学生、课程和成绩是模拟记录；图书与机构记录保留目录原始键和 JSON payload。`Coverage("books")` 和 `Coverage("institutions")` 可检查其版本及覆盖说明。

### 20. 医疗：诊断、药品与就诊

需要 `Bing.MockData.Healthcare`。诊断目录是 CDC/NCHS ICD-10-CM FY2027；药品目录为 RxNorm Current Prescribable Content 子集。

```csharp
using Bing.MockData.Healthcare;

var options = new MockOptions { Seed = 10 }.UseCatalogFiles(new[]
{
    new CatalogFile("diagnoses", Path.Combine(dataDir, "icd10cm.sqlite3")),
    new CatalogFile("medications", Path.Combine(dataDir, "rxnorm-cpc.sqlite3"))
});
using var context = new MockContext(options);
var patient = context.Healthcare().Patient().Build().Generate();
var encounter = context.Healthcare().Encounter(patient).Build().Generate();
var medication = context.Healthcare().Medication();
```

诊断和药品各自引用其来源目录；本库不推断临床适应关系。就诊、观察和时间均为模拟信息，不应用于临床决策。

### 21. 设备：PCI 设备、厂商和测量

需要 `Bing.MockData.Devices`。`pci-ids.sqlite3` 提供 PCI 厂商/设备目录，并非所有消费电子产品型号。

```csharp
using Bing.MockData.Devices;

var options = new MockOptions { Seed = 11 }
    .UseCatalogFile("devices", Path.Combine(dataDir, "pci-ids.sqlite3"));
using var context = new MockContext(options);
var device = context.Devices().Device().Build().Generate();
var reading = context.Devices().Measurement(device).Build().Generate();
```

设备型号与厂商沿 PCI 目录关系生成。IMEI 校验位正确不代表 TAC 被验证映射到此设备型号；`HasVerifiedTacMapping` 明确为 `false`。

## 流式消费与释放

目录查询逐页读取，`Query` 返回稳定键顺序的惰性序列；确定性抽样可用 `Count(query)` 后配合调用方提供的随机 selector 调用 `Sample(selector, query)`。生成器可以逐条流式枚举，不保留全部结果：

```csharp
var usersGenerator = context.Templates.User().Build();
foreach (var user in usersGenerator.Enumerate(1_000_000, cancellationToken))
{
    Consume(user);
}
```

`GenerateMany` 受物化记录预算限制。`MockExport.Write` 支持 JSON、JSON Lines 和 CSV，允许列投影及 `leaveOpen`；写入取消或失败时目标流可能包含部分结果。调用方应先取消并等待活动任务结束，再释放其拥有的上下文。路径挂载资源由 `using`/`Dispose` 释放；借用的目录默认仍由调用方释放。
