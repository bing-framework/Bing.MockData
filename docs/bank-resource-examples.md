# 银行卡与全球 BIN 目录

核心入口按固定 bankcardinfo 2.0.4 历史规则生成模拟号码。全球 BIN 目录来自单独挂载的社区快照，两者分别声明覆盖，不用没有卡号长度的 BIN 行推导完整卡号。

## 银行卡

```csharp
using Bing.MockData;
using Bing.MockData.Datas.Models.Banks;

using var context = new MockContext(new MockOptions
{
    Seed = 39,
    ReferenceTime = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero)
});
string card = context.Finance.BankCard("308", CardType.CreditCard);
var cards = context.Generator(c => c.ForField("payment.card").Finance.BankCard());
foreach (string value in cards.Enumerate(1_000_000))
{
    // 逐条写入或消费；不要为流式任务累计全部结果。
}
string record42 = cards.At(42);
```

代码属于 `bing-legacy` 兼容命名空间：100 邮政储蓄、102 工行、103 农行、104 中行、105 建行、301 交行、308 招行、305 民生。不是 CNAPS 或联行号。参数为 null 表示不限；Unknown 类型、空白代码或非法格式会报参数错误，有效格式但无规则会报覆盖错误。

候选规则可能输出 15–19 位数字；号码满足所选规则及 Luhn。若文本预算小于候选最大长度，调用在抽样前失败，不暗中缩小覆盖。号码不表示账户已分配、可支付或全局唯一。现有 RandomizerFactory / IBankCardRandomizer 入口继续可用，但不承诺与上下文入口使用同 Seed 时产生同一序列。

## 全球 BIN 数据

```csharp
using Bing.MockData.Catalogs;

using var context = new MockContext(new MockOptions { Seed = 39 }
    .UseCatalogFile("bank-bins", Path.Combine(dataRoot, "bank-bins.sqlite3")));
var bins = context.BankBins();
Console.WriteLine(bins.Info.Version);

if (bins.TryGet("622000", out var row))
    Console.WriteLine($"{row.Bin}: {row.Issuer ?? "未知机构"}");

foreach (var record in bins.Query(new CatalogQuery
{
    CountryCode = "CN",
    KeyPrefix = "bin|622",
    PageSize = 256
}, cancellationToken))
{
    // 保留 Source、Seen 和 DataVersion；Seen 不代表银行核验日期。
}

var samples = context.Generator(c => c.BankBins().Sample());
foreach (var sample in samples.EnumerateParallel(1_000_000, 4,
    cancellationToken: cancellationToken))
{
    // 输出按记录序号排列，随机状态由记录上下文隔离。
}
```

精确查询只接受 1–8 位 ASCII 前缀，不接受完整卡号自动截断；固定 CSV 实际有 7 条五位、454,496 条六位和 3,548 条八位前缀，与上游 README 的六位/少量五位描述不完全一致。八位来源行原样保留，但未据此核验其实际分配或宣称完整八位 BIN 覆盖；精确查询可能未命中。CountryCode 和名称筛选使用来源文本；KeyPrefix 包含 `bin|`。机构缺名、国家缺失、类型未知按 null 返回，名称前缀筛选不代表实体归并。类型与 Category 保留来源语义，不自动映射旧银行卡枚举。

目录适配器借用上下文挂载项，不需要单独 Dispose。路径挂载由上下文拥有；外部实例的 `UseCatalog` 默认借用，显式 `ownsResource: true` 才转移所有权。应先取消并等待活动任务结束，再释放根上下文。提前结束 Query 会清理枚举资源，重新枚举重新读取同一快照。

## 迁移与重放

当前生成契约 `domain-v6`，内置数据身份 `china-2025-12-31-domain-v5`。规则由旧 832 条替换为固定来源的 845 条：精确交集 778，新增 67，移除 54；招行有 39 组类型变化及 15 条旧额外长度规则移除。变化是显式来源替换，不声称旧表的直接上游已被证明。

旧契约或旧数据身份被拒绝，不能仅修改重放元信息中的版本字段。要复现旧输出需使用原代码、数据和运行时；新任务使用当前配置重新生成并保存 Replay。挂载的 BIN 目录版本由现有目录集合身份校验，恢复时需重新挂载同一数据集版本。

全球目录 Id 为 `bank-bins-techbuddie`，版本为 `fcc907c81cf02b12e396b0c12a8c16472b47619a-import-v1`。CC BY 4.0、署名和转换说明随离线数据包分发。新数据包版本为 `catalogs-2026-10-27-banks-v1`，使用现有 restore-data 脚本及对应可信清单恢复；显式配置的 MOCKDATA_DATA_ROOT 无效时直接失败。
