# Bing.MockData
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)

Bing.MockData 提供可重放的模拟数据、完整对象规则、关联业务模板及流式导出，支持 `netstandard2.0` 和 `net10.0`。开发与测试使用 .NET SDK 10.0.400。

当前补齐结果见 [交付报告](docs/domain-gap-closure-report.md)；新增关联场景见 [使用示例](docs/domain-gap-examples.md)。目录严格关系校验仍包含 PCI 原始来源的 46 条缺失引用，详见报告。

本轮新增 [链式 Schema 与可配置航班](docs/domain-next-examples.md)，详见 [方案](docs/domain-next-plan.md) 与 [交付结果](docs/domain-next-report.md)。

最新补齐：[组合约束与嵌套自动生成](docs/constraint-completion-examples.md)、[职业/商品/客户端/颜色配置](docs/configurable-samples.md)。本轮使用 `domain-v3` 重放契约，详见[实施方案](docs/constraint-completion-plan.md)与[交付证据](docs/constraint-completion-report.md)。

## 快速开始

```csharp
using Bing.MockData;

using var mock = new MockContext(new MockOptions
{
    Seed = 42,
    ReferenceTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
    Locale = "zh-CN"
});
var people = mock.Templates.Person("110000").Build().GenerateMany(10);
var users = mock.Templates.User().Build().GenerateMany(20);
var products = mock.Templates.Product().Build().GenerateMany(10);
var orders = mock.Templates.Order(users, products).Build();
using var file = File.Create("orders.jsonl");
MockExport.Write(orders.Enumerate(1000000), file, MockExportFormat.JsonLines);
```

`GenerateMany` 立即返回小批量集合；`Enumerate` 逐条生成，不保留已消费结果。`EnumerateParallel(count, 4)` 显式启用有界并行，并按记录顺序输出。相同规则、Seed、参考时间和数据版本下可通过 `At(recordIndex)` 单独重放一条记录。

- [使用指南：对象、重放、并发、字典、资源、DI 与 xUnit](docs/capabilities-guide.md)
- [能力升级迁移对照](docs/capabilities-migration.md)
- [22 项实施计划与验收证据](docs/capabilities-plan.md)
- [行政区划来源、截止日期与 CC0 许可](docs/data-sources.md)
- [此前范围与安全修复记录](docs/execution.md)

`IRandomizer<out T>` 统一为一个 `Generate()` 契约。数值、日期、GUID 默认返回非空值；空值必须通过 `.Nullable(probability)` 或 `.OrNull(probability)` 显式配置。中国地址采用完整县级及以上区划快照；街道门牌和英文地址是模拟文本。

## 构建与验证

```powershell
dotnet restore Bing.MockData.sln --locked-mode
dotnet test tests/Bing.MockData.Tests/Bing.MockData.Tests.csproj --no-restore
dotnet test tests/Bing.MockData.Tests/Bing.MockData.Tests.csproj --no-restore -p:MockDataTargetFramework=netstandard2.0
pwsh -NoProfile -File build/test-pack-script.ps1
pwsh -NoProfile -File build/pack.ps1
```

最后一条命令执行锁定还原及依赖审计、两套程序集的完整 Release 测试、双目标显式构建、打包资产检查、隔离缓存中的包消费编译与运行，默认不发布。`netstandard2.0` 的共享测试在 .NET 10 测试宿主上加载该目标程序集；不等于在每个 .NET Standard 实现上都运行过。现代异步枚举、DateOnly、TimeOnly 和异步释放只在 `net10.0` 提供。

资源探针：`dotnet run --project tools/CapabilityProbe -c Release -- 1000000 4`。探针使用丢弃输出的接收端，记录条数、耗时、累计分配量、采样托管内存、进程峰值和实际并行度，不设未经确认的性能门槛。

## Nuget
|Nuget|版本号|说明|
|---|---|---|
|Bing.MockData|[![NuGet Badge](https://buildstats.info/nuget/Bing.MockData?includePreReleases=true)](https://www.nuget.org/packages/Bing.MockData)|

## 支持的随机数据
- 身份证号码：`ChineseIdCardRandomizer`
- 城市：`CityRandomizer`
- 国家：`CountryRandomizer`
- 日期时间：`DateTimeRandomizer`
- Email 地址：`EmailAddressRandomizer`
- 英文名（FirstName、LastName、FullName）：`FirstNameRandomizer`、`LastNameRandomizer`、`FullNameRandomizer`
- Guid：`GuidRandomizer`
- IBAN：`IBANRandomizer`
- IP 地址（IP4、IP6）：`IPv4AddressRandomizer`、`IPv6AddressRandomizer`
- MAC 地址：`MACAddressRandomizer`
- 手机号码：`MobileRandomizer`
- 随机数（int、long、float、double....）：`NumberRandomizer<T>`
- 自定义字符串列表：`StringListRandomizer`、`TextRegexRandomizer`、`TextRandomizer`
- 单词：`TextWordsRandomizer`
- 时间跨度：`TimeSpanRandomizer`
- 文章段落：`TextLipsumRandomizer`
- 地址：`ChineseAddressRandomizer`
- 姓名：`ChineseNameRandomizer`
- 银行卡号：`BankCardRandomizer`

## 单项生成器

以下是可独立使用的兼容入口。扩展格式化、可空和批量方法需要 `using Bing.MockData;`；配置与工厂分别位于 `Bing.MockData.Core.Options`、`Bing.MockData.Factories`。跨字段一致的人物信息应使用 `Templates.Person()`。

```c#
// 生成随机身份证
var randomizer = RandomizerFactory.GetRandomizer(new ChineseIdCardFieldOptions());
var idcard = randomizer.Generate();
var time = randomizer.GenerateValidPeriod();
var address = randomizer.GenerateIssueOrg();

// 生成随机城市(国外)
var randomizer = RandomizerFactory.GetRandomizer(new CityFieldOptions());
var result = randomizer.Generate();

// 生成随机国家(英文名)
var randomizer = RandomizerFactory.GetRandomizer(new CountryFieldOptions());
var result = randomizer.Generate();

// 生成随机时间
var randomizer = RandomizerFactory.GetRandomizer(new DateTimeFieldOptions());
var result = randomizer.Generate();

// 生成随机Email地址
var randomizer = RandomizerFactory.GetRandomizer(new EmailAddressFieldOptions());
var result = randomizer.Generate();

// 生成随机英文名(FirstName)
var randomizer = RandomizerFactory.GetRandomizer(new FirstNameFieldOptions());
var result = randomizer.Generate();

// 生成随机英文名(LastName)
var randomizer = RandomizerFactory.GetRandomizer(new LastNameFieldOptions());
var result = randomizer.Generate();

// 生成随机英文名(FullName)
var randomizer = RandomizerFactory.GetRandomizer(new FullNameFieldOptions());
var result = randomizer.Generate();

// 生成随机GUID
var randomizer = RandomizerFactory.GetRandomizer(new GuidFieldOptions());
var result = randomizer.Generate();

// 生成随机IBAN
var randomizer = RandomizerFactory.GetRandomizer(new IBANFieldOptions());
var result = randomizer.Generate();

// 生成随机IP地址(IP4)
var randomizer = RandomizerFactory.GetRandomizer(new IPv4AddressFieldOptions());
var result = randomizer.Generate();

// 生成随机IP地址(IP6)
var randomizer = RandomizerFactory.GetRandomizer(new IPv6AddressFieldOptions());
var result = randomizer.Generate();

// 生成随机MAC地址
var randomizer = RandomizerFactory.GetRandomizer(new MACAddressFieldOptions());
var result = randomizer.Generate();

// 生成随机手机号码
var randomizer = RandomizerFactory.GetRandomizer(new MobileFieldOptions());
var result = randomizer.Generate();

// 生成随机数值(int,long,float,double...)
var randomizer = RandomizerFactory.GetRandomizer<int>(new NumberFieldOptions<int>() {Min = 0, Max = 1000});
var result = randomizer.Generate();

// 生成随机自定义字符串
var randomizer = RandomizerFactory.GetRandomizer(new StringListFieldOptions(){Values = new List<string>() {"张三", "李四", "王五"}});
var result = randomizer.Generate();

// 生成随机段落(英文)
var randomizer = RandomizerFactory.GetRandomizer(new TextLipsumFieldOptions());
var result = randomizer.Generate();

// 生成随机文本
var randomizer = RandomizerFactory.GetRandomizer(new TextFieldOptions() {Min = 3, Max = 20, UseLetter = true, UseNumber = true});
var result = randomizer.Generate();

// 按照正则表达式随机生成文本
var randomizer = RandomizerFactory.GetRandomizer(new TextRegexFieldOptions() {Pattern = @"^[0-9]{4}[A-Z]{2}"});
var result = randomizer.Generate();

// 生成随机单词
var randomizer = RandomizerFactory.GetRandomizer(new TextWordsFieldOptions() {Min = 3, Max = 20});
var result = randomizer.Generate();

// 生成随机时间跨度
var randomizer = RandomizerFactory.GetRandomizer(new TimeSpanFieldOptions() {From = TimeSpan.Zero, To = TimeSpan.FromDays(20)});
var result = randomizer.GenerateAsString();

// 生成随机地址
var randomizer = RandomizerFactory.GetRandomizer(new ChineseAddressFieldOptions());
var result = randomizer.Generate();
var region = randomizer.GenerateRegion();

// 生成随机姓名
var randomizer = RandomizerFactory.GetRandomizer(new ChineseNameFieldOptions());
var result = randomizer.Generate();

// 生成随机银行卡号
var randomizer = RandomizerFactory.GetRandomizer(new BankCardFieldOptions());
var result = randomizer.Generate();
```

## 依赖类库
- NLipsum
- Fare
- Json.Net

## 作者

简玄冰

## 贡献与反馈

> 如果你在阅读或使用Bing中任意一个代码片断时发现Bug，或有更佳实现方式，请通知我们。

> 为了保持代码简单，目前很多功能只建立了基本结构，细节特性未进行迁移，在后续需要时进行添加，如果你发现某个类无法满足你的需求，请通知我们。

> 你可以通过github的Issue或Pull Request向我们提交问题和代码，如果你更喜欢使用QQ进行交流，请加入我们的交流QQ群。

> 对于你提交的代码，如果我们决定采纳，可能会进行相应重构，以统一代码风格。

> 对于热心的同学，将会把你的名字放到**贡献者**名单中。

## 免责声明
- 虽然我们对代码已经进行高度审查，并用于自己的项目中，但依然可能存在某些未知的BUG，如果你的生产系统蒙受损失，Bing 团队不会对此负责。
- 出于成本的考虑，我们不会对已发布的API保持兼容，每当更新代码时，请注意该问题。

## 开源地址
[https://github.com/bing-framework/Bing.MockData](https://github.com/bing-framework/Bing.MockData)

## License

**MIT**

> 这意味着你可以在任意场景下使用 Bing 应用框架而不会有人找你要钱。

> Bing 会尽量引入开源免费的第三方技术框架，如有意外，还请自行了解。


## 全领域数据扩展

本地实现新增 Catalogs、Schema、Transport、Education、Healthcare、Devices 六个扩展包，均与核心包一样输出 `netstandard2.0` / `net10.0`。目录数据按版本单独交付，运行时离线读取。

- [21 项能力与最小示例](docs/domain-examples.md)
- [模型、金额、重放和目录迁移](docs/domain-migration.md)
- [数据来源、许可证和覆盖范围](docs/catalog-data-manifest.md)
- [本轮计划和验收状态](docs/domain-capabilities-plan.md)

本地完整门禁：`pwsh ./build/pack-domains.ps1`。脚本执行双目标验证、七个代码包检查、独立数据归档和隔离包消费者；不会提交或发布。
