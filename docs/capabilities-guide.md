# MockContext 使用指南

## 对象规则与字段隔离

```csharp
using Bing.MockData;

using var mock = new MockContext(new MockOptions { Seed = 42 });
var builder = mock.Builder<Customer>()
    .RuleFor(x => x.Id, c => c.RecordIndex + 1)
    .RuleFor(x => x.Name, c => c.Text.Name())
    .RuleFor(x => x.Email, c => c.Text.Email())
    .RuleFor(x => x.Display, (_, x) => $"{x.Id}: {x.Name}")
    .Ignore(x => x.Note)
    .Strict();
var customers = builder.Build();
var firstTen = customers.GenerateMany(10);
var record99 = customers.At(99);

public sealed class Customer
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Display { get; set; }
    public string Note { get; set; } = "构造默认值";
}
```

字段按声明顺序执行；派生字段只能依赖之前已赋值的成员。新增无关字段不影响已有字段的随机结果。默认只写明确配置的公开可写属性/字段；严格模式要求这些成员全部配置或忽略。没有公开无参构造函数时使用 `ConstructUsing(c => new Customer(...))`。`Build()` 冻结规则；继续编辑 builder 不影响之前生成器。重复 `RuleFor` 会报错；模板覆盖使用显式 `Override(x => x.Name, (c, value) => ...)`，保留原规则顺序。

使用 `.Nested(x => x.Address, addressBuilder)` 或 `.Collection(x => x.Items, 1, 5, c => ...)` 构建嵌套对象及列表。集合规则支持 `List<T>`、`IReadOnlyList<T>`，每个元素获得自己的路径。动态递归可用 `childBuilder.Generate(c)`，共享深度预算；达到上限抛出 `MockLimitException`。自定义构造函数和委托内自行创建的任意对象图由调用方负责；库不会递归扫描未配置成员或中断用户代码。

直接组合 `mock.Generator(c => ...)` 时，使用 `c.ForField("Name")` 等稳定路径分离字段。不要在委托里捕获根 `mock` 或共享 `Random` 来生成随机值；不要用共享可变状态决定结果。自定义规则外部读取的配置、引用集合和字典条目也应保持不变。

## 重放

```csharp
using Newtonsoft.Json;

string saved = JsonConvert.SerializeObject(mock.Replay);
var replay = JsonConvert.DeserializeObject<ReplayInfo>(saved);
using var restored = new MockContext(replay.ToOptions());
// 在 restored 上重新配置相同 builder 后，用 recipe.At(failedRecordIndex) 重放整条。
// 也可以 restored.ForRecord(replay.RecordIndex).ForField(replay.FieldPath) 重放对应子流。
```

Seed 省略时会生成并公开实际种子，参考时间创建时只取一次。重放记录含 Seed、参考时间、地区、预算、记录序号、字段路径、库/运行时/算法/内置数据版本；`ToOptions()` 拒绝版本不匹配。保证范围为相同库版本、运行时、配置、规则及字典数据。自定义委托、外部资源、外部字典内容不序列化，重放时必须重新提供相同版本。该机制不用于跨版本稳定 ID 或密码学随机数。

`Generate()` 消耗该生成器的记录序号；`At(index)` 不消耗序号。`GenerateMany`、`Enumerate` 每次独立从记录 0 开始。同一可枚举对象重复枚举会重新生成，不缓存对象；外部字典以每次开始枚举时的目录快照为准。字段路径使用点分隔，应当稳定且不重名。

`MockGenerationException` 含 `RuleId`、`FieldPath`、`RecordIndex`、`Replay` 与原始异常。取消抛出标准 `OperationCanceledException`；预算超限抛出 `MockLimitException`，提供 `LimitName` 和 `Limit`。

## 类型、概率、唯一值与扩展

- `Numbers.Between<T>(min, max)`：有符号/无符号整数、浮点、decimal、日期和时长的闭区间；还有 `Numbers.Enum<T>()`。
- `Numbers.Decimal(min, max, decimalPlaces: 2, step: 0.05m)`：在零起点的有效刻度中抽样，向区间内部对齐；没有有效刻度直接报错，不通过四舍五入制造越界值。
- `Dates`：DateTime、DateTimeOffset、TimeSpan，`net10.0` 另有 DateOnly、TimeOnly。`Past`/`Future` 使用冻结参考时间。
- `Numbers.Boolean(0.8)`；`generator.Nullable(0.2)` / `OrNull(0.2)`；`Collections.Weighted(values, weights)`；零权重不会被选中。
- `Collections.Pick`、`Sample`、`Shuffle`。后两者按候选下标不放回，不修改输入；相等的重复候选项仍可能产生相等值。
- `Sequence(start, step)` 与 `Identifier(prefix, start, step, format)` 按记录序号计算，不保存历史，适合百万唯一编号；批次重启会重复编号，跨批需指定不同起点。
- `using var unique = new UniqueScope<string>(generator)`，调用 `Next()`，最多保留 `MaxUniqueValues` 条；碰撞/条件筛选最多 `MaxAttempts` 次，耗尽明确失败。有限候选先用 `Sample`。
- `Boundaries.Text(TextBoundary.Overlong, 20)` 显式生成业务长度超限样本，仍服从全局文本上限；另有空串、空白、端点和格式错误。异常值不会混入正常模式。

```csharp
var options = new MockOptions { Seed = 52 }
    .Register("score", c => c.Numbers.Int(0, 100));
using var scope = new MockContext(options);
var scores = scope.Registered<int>("score").GenerateMany(10);
```

`RegisterFactory` 接收每条记录的上下文并创建 `IRandomizer<T>`；工厂每条记录调用一次，不能共享可变随机源。配置在创建上下文后冻结，库没有全局用户注册表。既有配置可用 `c.Generate<string>(new MobileFieldOptions())` 适配到当前字段的随机源、文本预算及取消信号。

## 地区和业务模板

`Templates.Person()`、`Address()`、`User()`、`Company()`、`Product()`、`Order(users, products)` 返回可覆盖的 builder。订单直接引用调用方提供的用户和商品对象，单价复制到明细，金额由数量和单价求和。候选数组结构在配置时复制，实体引用不深复制；生成过程中请勿修改源实体。

中国区划使用 2025 年末 3212 条县级及以上离线快照。`ChinaDivisions.All/Find/Children/Path` 提供结构查询；`Templates.Address("420000")` 可按省、市、县过滤。直辖市和省直辖县可没有 City，台港澳没有快照未提供的县区。地址代码和名称来自同一真实路径，街道和门牌明确模拟。无县级代码的人物不生成大陆身份证；其生日、年龄、性别、身份证日期/性别位/校验位及区划保持关联。身份证不表示真实个人，也不承诺历史签发时该区划已存在。

地区支持 `zh-CN`、`en-US`，其他值立即报错；英文地址为 `Example State / Sample City` 样本。邮箱和 URL 默认 example.com，可传入自定义 DNS 域名。模板覆盖会按执行顺序重新计算后续派生字段；若覆盖相关派生字段本身，一致性由调用方负责。

`Text.Random(min, max, alphabet)` 按 UTF-16 单元计长，字符表按完整 Unicode 标量抽样，拒绝孤立代理项。仅含补充字符的字符表不能生成奇数长度。

正则只支持可约束的有限子集：字面量、字符类、常见转义、分组、选择、`?`、`{m}`、`{m,n}` 和外层锚点；拒绝 `*`、`+`、无上限重复、断言、回溯引用等。最大可能输出超过文本预算就提前拒绝；模式大小和 AST 最大执行步数受 `max(1024, MaxTextLength * 16)` 约束。补充字符重复需分组，如 `(?:😀){2}`。IBAN 的固定内置模式仍使用 Fare，不承诺强行中断其底层编译。

## 外部字典与资源所有权

```csharp
using var context = new MockContext();
context.LoadTextDictionary("names", "names.txt"); // UTF-8，每行一条，库打开并关闭文件
context.LoadJsonDictionary<CatalogItem>("catalog", "catalog.json"); // 完整 JSON 数组
context.SetDictionary("names", new[] { "张三", "李四" }); // 原子替换
var names = context.Generator(c => c.Collections.Pick(c.Dictionary<string>("names")));
context.RemoveDictionary("catalog");
```

读入同时限制字节数、条数、JSON 深度，并检查取消信号；失败保留旧快照。调用方传入 Stream 默认 `leaveOpen: true`；显式 false 时，成功、取消和异常均关闭。JSON 不启用类型名反序列化。字节限制约束输入文件大小，不等于托管堆硬限制。`SetDictionary` 控制条数并复制集合结构，条目引用不深复制，建议使用不可变数据。

正在枚举的任务继续使用开始时的快照，后来任务观察替换/移除；字典仅由上下文持有。普通生成器无须 Dispose。根上下文释放显式登记的拥有资源；借用资源由调用方负责。

```csharp
var options = new MockOptions().UseResource(myResource, ownsResource: true);
using var context = new MockContext(options);
// 先取消并等待所有活动任务结束，再释放 context。
```

释放幂等，即使某个资源释放失败仍尝试其余资源并汇总异常。资源按登记逆序释放；子上下文不拥有根资源。包含仅支持异步释放资源时，net10.0 必须 `await using` / `DisposeAsync()`。不要将同一拥有资源交给多个上下文。失败、取消或提前结束枚举会结束本次枚举；上下文资源仍保留到根上下文释放，以便继续生成。

## 流式消费与导出

```csharp
var metrics = new GenerationMetrics();
var rows = customers.EnumerateParallel(1000000, 4, cancellationToken, metrics);
using var stream = File.Create("customers.csv");
MockExport.Write(rows, stream, MockExportFormat.Csv,
    new[] { new ExportColumn<Customer>("id", x => x.Id), new ExportColumn<Customer>("name", x => x.Name) },
    leaveOpen: true, cancellationToken: cancellationToken);
```

默认串行；显式并行使用最多 256 个记录的固定窗口、固定并行度上限，输出有序。消费者不请求下一窗口就不会开始后续生产；提前结束可能已经生成当前窗口的其余项，方法返回后没有后台工作线程仍在生成。每条记录只使用自己的上下文，串并行结果一致。`GenerationMetrics` 为每次枚举单独创建，记录真实最大并行度、排队记录上限和生成数。

JSON、JSON Lines、CSV 都逐条写出，只暂存当前记录的序列化文本。CSV 要显式列定义，支持逗号、引号及 CR/LF 转义；JSON/JSONL 可省略列或投影选列。默认保留输出 Stream/TextWriter。取消/异常时文件可能包含部分数据（JSON 可能未闭合）；库不回滚输出。消费者应自行选择临时文件后原子替换。net10.0 的 `EnumerateAsync` 和 `MockExport.WriteAsync` 支持异步枚举输入；CPU 生成不自动转到后台线程。

## 默认预算

| 配置 | 默认值 |
|---|---:|
| MaxMaterializedRecords | 10,000 |
| MaxStreamingRecords | 1,000,000 |
| MaxTextLength | 100,000 UTF-16 单元 |
| MaxDepth | 8 |
| MaxCollectionSize | 1,000 |
| MaxAttempts | 100 |
| MaxUniqueValues | 100,000 |
| MaxDictionaryBytes | 16 MiB |
| MaxDictionaryEntries | 100,000 |

在 `MockOptions.Limits` 中调整，构造后被冻结。顶层数量与对象集合分别计数。可预知超限在执行前拒绝，动态超限终止当前操作。库检查记录、元素和重试边界；调用方自定义委托内部的循环、分配及外部 IO 须自行检查传入的 CancellationToken。

## DI 和 xUnit 接入

核心包不依赖 Microsoft.Extensions.DependencyInjection 或 xUnit。以下代码在已有对应框架引用的应用/测试项目中使用。

```csharp
// DI：请求/测试作用域内使用，容器负责释放创建的根上下文。
services.AddScoped(_ => new MockContext(new MockOptions { Seed = 42 }));
// 消费者构造函数接收 MockContext；不要将其注入更长生命周期的单例。
```

```csharp
[Fact]
public void Order_total_matches_lines()
{
    using var mock = new MockContext(new MockOptions { Seed = 42,
        ReferenceTime = DateTimeOffset.Parse("2026-01-01T00:00:00Z") });
    var users = mock.Templates.User().Build().GenerateMany(3);
    var products = mock.Templates.Product().Build().GenerateMany(5);
    var order = mock.Templates.Order(users, products).Build().At(10);
    Assert.Equal(order.Items.Sum(x => x.Subtotal), order.Total);
}
```

每个测试创建独立上下文，固定种子和参考时间。共享 fixture 只共享不可变配置，避免并行测试共享有状态 Generate 序号或唯一值作用域。
