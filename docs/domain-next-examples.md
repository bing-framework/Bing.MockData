# 链式约束与可配置航班示例

本轮入口来自 `Bing.MockData.Schema` 和 `Bing.MockData.Transport`。代码包保持 `netstandard2.0` / `net10.0`，下例采用 C# 的现代语法；可直接编译的双目标隔离消费者位于 `tests/Bing.MockData.DomainPackageSmoke/Program.cs`。

## 按依赖生成完整 JSON

当 `account` 存在时要求 `profile`，而 `profile` 又要求 `verified`。生成器现在继续展开新引入的依赖，并保持 `unevaluatedProperties` 的最终根验证。

```csharp
using Bing.MockData;
using Bing.MockData.Schema;

const string json = """
{
  "type": "object",
  "properties": { "account": { "const": "demo" } },
  "required": ["account"],
  "dependentSchemas": {
    "account": {
      "properties": { "profile": { "const": "basic" } },
      "required": ["profile"],
      "dependentSchemas": {
        "profile": {
          "properties": { "verified": { "type": "boolean" } },
          "required": ["verified"]
        }
      }
    }
  },
  "unevaluatedProperties": false
}
""";
using var schema = CompiledSchema.Compile(json);
using var context = new MockContext(new MockOptions { Seed = 42 });
var samples = schema.Generator(context);
string value = samples.At(5).Json;
// {"account":"demo","profile":"basic","verified":...}
bool valid = schema.Validate(value);
```

`GenerateMany`、`Enumerate`、`EnumerateParallel` 和 `At(index)` 使用同一生成契约。按数量挑选可选属性时会考虑 `minProperties` / `maxProperties`；需要补足字段时优先选择 Schema 已声明的属性，从而支持禁止额外属性的对象。

## 有限候选交集

```csharp
using var choices = CompiledSchema.Compile("""
{"allOf":[{"enum":[1,2,3]},{"enum":[2.0]}]}
""");
var selected = choices.Generate(context); // JSON 数值 2 或等价表示
```

候选中的 `1`、`1.0`、`1e0` 按数学数值相等处理，复合对象忽略键顺序。`const` / `enum` 里的嵌套数据同样受对象深度、集合和文本预算约束。空交集和预算内搜索失败仍报告 `BudgetExhausted`，不会凭尝试失败宣称整个 Schema 无解。

## 分别配置出发地、到达地和座位

```csharp
using System;
using System.IO;
using Bing.MockData;
using Bing.MockData.Catalogs;
using Bing.MockData.Transport;

string dataDir = Path.Combine(AppContext.BaseDirectory, "catalog-data");
using var context = new MockContext(new MockOptions
{
    Seed = 42,
    ReferenceTime = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero)
}.UseCatalogFile("airports", Path.Combine(dataDir, "ourairports.sqlite3"))
 .UseCatalogFile("airlines", Path.Combine(dataDir, "wikidata-airlines.sqlite3")));

var options = new FlightOptions
{
    DepartureCountry = "CN",
    ArrivalCountry = "US",
    AirlineCountry = "GB",
    EarliestDeparture = context.ReferenceTime.AddDays(1),
    LatestDeparture = context.ReferenceTime.AddDays(2),
    MinDuration = TimeSpan.FromHours(10),
    MaxDuration = TimeSpan.FromHours(12),
    Seats = new SeatOptions
    {
        MinRow = 10, MaxRow = 20,
        MinColumn = 'A', MaxColumn = 'D'
    }
};
var flights = context.Transport().ConfiguredFlight(options).Build();
var flight = flights.At(7);
string seat = context.Transport().Seat(new SeatOptions
{
    MinRow = 4, MaxRow = 4, MinColumn = 'B', MaxColumn = 'B'
}); // 4B
```

使用已有目录记录时，设置 `FlightOptions.Departure`、`Arrival` 或 `Airline`。这些记录必须来自当前挂载数据集，并与同时配置的国家筛选一致。只固定到达机场时，随机出发机场也会排除同一机场。配置、时间和嵌套座位范围在创建模板时复制，后续修改原 options 不影响已创建的生成器。

生成结果仍为模拟航线，国家筛选及飞行时长不代表航空公司实际运营该航线。座位范围是调用方的模拟配置，不表示目录具备真实飞机客舱布局。旧 `Flight(country)` 的行为和调用方式保持不变。

## 重放与生命周期

当前核心生成契约为 `domain-v6`，内置数据身份为 `china-2025-12-31-domain-v5`；Schema 搜索版本 `schema-search-v5` 参与 `CompiledSchema.Identity`，目录集合身份为 `catalog-set-v2`。当前实现会拒绝旧契约或旧 Schema 身份的历史重放记录，不会静默套用新算法。历史结果能否复现取决于原代码、运行时、数据和配置环境是否齐备；在当前版本重新生成记录时，应从原始业务输入出发并保存新的完整重放信息，不能只改身份字段。

直接创建的 `CompiledSchema` 由调用方 `using` 释放；`UseSchema` 挂载的实例由上下文释放。目录路径挂载由上下文拥有；取消、枚举及外部实例所有权契约保持既有约定。
