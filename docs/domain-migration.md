# 全领域 API 迁移说明

当前使用 `domain-v6` / `schema-search-v5` 生成身份，目录集合身份为 `catalog-set-v2`；内置数据身份为 `china-2025-12-31-domain-v5`。当前实现拒绝不匹配身份的历史重放记录。历史输出能否复现取决于完整原始代码、运行时、数据和配置环境是否仍可用；无法使用原环境时，应从原始业务输入在当前版本重新生成并保存重放信息。包版本未提升。格式断言资源控制、方言识别、显式规则定位和目录身份修复见[最新报告](review-followup-fixes-2026-10-26.md)，[上一轮报告](review-fixes-2026-10-26.md)保留；用法见[迁移与示例](constraint-completion-examples.md)。

此版本统一以 `MockContext`、`MockGenerator<T>` 和领域门面作为主要入口。业务模型和模板已升级，旧式逐个随机器的使用方式应迁移到统一上下文；重放契约也已带版本，旧重放记录不会自动解释为新的生成规则。

## 基础生成器迁移

| 旧用法 | 当前用法 |
|---|---|
| 手动创建每个 `*FieldOptions`，再创建对应随机器 | 创建一个 `MockContext`，通过 `Numbers`、`Dates`、`Text`、`Network`、`Collections` 调用 |
| `RandomizerFactory.Create(options)` 后自行编排对象字段 | `context.Builder<T>()` 或 `context.AutoBuilder<T>()` 组合字段规则 |
| 对普通数值/日期生成器依赖隐式空值 | 普通方法返回非空值；仅在需要时用 `Nullable(probability)` 或 `OrNull(probability)` 包装 |
| 自己循环构造结果列表 | 小集合使用 `GenerateMany(count)`；大批量使用 `Enumerate(count, token)` |
| 把共享随机数生成器放进并行任务 | 每条记录按索引及字段路径派生；使用 `EnumerateParallel(count, degree, token)` |

```csharp
using var context = new MockContext(new MockOptions { Seed = 42 });
int age = context.Numbers.Int(18, 65); // 总是有值
var optionalScore = context.Generator(c => c.Numbers.Decimal(0, 100, 1), "score").Nullable(0.05);
var users = context.Templates.User().Build().Enumerate(1_000_000, cancellationToken);
```

空值概率应在产生 nullable 类型的包装调用上显式给出；默认数值、日期和业务模型字段不会悄然变成 null。对旧代码中自己创建的非快照随机器，`AsGenerator()` 不一定支持重放；可重放的自定义逻辑应通过 `context.Generator(...)` 或 `MockOptions.RegisterFactory(...)` 注册。

## 业务模型与金额语义

用户、公司、商品、订单模型已扩充字段，推荐通过 `context.Templates.User/Company/Product/Order` 建立对象，而不是分别填充不相关字段。`Order` 直接引用输入的用户和商品快照，所有商品必须使用同一币种和小数位。

订单总额是只读计算属性：

```text
Total = 明细小计 - Discount + Shipping + Tax
```

支付、退款金额和状态事件由订单规则生成并验证。未付款或取消状态不含支付记录；全额退款金额等于已付款金额；部分退款必须大于零且小于已付款金额。金额按 `CurrencyInfo.DecimalPlaces` 抽样对齐有效步长。不要继续将订单总额作为一个可独立设置的字段，也不要把不同币种的金额相加。需要 CLDR 小数位时加载 CLDR 目录，再用 `context.Finance.Currency(code)` 取得币种配置；核心无目录的 `Currency()` 只提供常用样本。

地址、个人年龄、订单时间、支付与物流节点都依赖上下文 `ReferenceTime`，测试或快照应固定该值。信用代码、条码、VIN 和 IMEI 的格式校验能力与真实分配/车型/TAC 映射是不同保证；领域示例和目录覆盖说明明确标记未验证的关联。

## 重放与版本

`context.Replay` 捕获 seed、参考时间、区域、随机算法、内置数据、生成契约及已挂载扩展版本。目录/Schema 版本随扩展身份一起冻结；目录快照要重新挂载到 `ToOptions()` 返回的配置中，不能从重放记录还原数据文件或自定义规则。

```csharp
ReplayInfo replay;
using (var context = new MockContext(new MockOptions { Seed = 42 }))
{
    replay = context.Replay;
    var value = context.ForRecord(17).Text.Name();
}

var replayOptions = replay.ToOptions();
// 在此重新挂载同版本的目录、Schema、扩展和自定义规则。
using var replayContext = new MockContext(replayOptions);
var sameRecord = replayContext.ForRecord(17).Text.Name();
```

重放保证只适用于相同库版本、运行时、算法、内置数据快照、扩展版本及规则配置。改变外部数据版本会在上下文构建时与期望扩展版本比较并失败；旧格式或不兼容版本不会静默运行。数据包必须通过应用部署流程固定版本。

### 行为兼容提醒

当前生成契约为 `domain-v6`，内置数据身份为 `china-2025-12-31-domain-v5`；Schema 搜索和目录集合身份分别为 `schema-search-v5` 与 `catalog-set-v2`。历史 `domain-v1`、`domain-v2` 等不匹配记录不受当前实现支持，`ReplayInfo.ToOptions()` 会因身份不兼容而抛出 `InvalidOperationException`。历史结果能否复现取决于原始完整环境是否仍可用；不能只改写版本字段。迁移到当前版本时，应根据原始业务输入重新生成并保存新的重放信息。CLDR 目录身份为 `49.0.0-import-v2`，历史币种和地区关系已纳入版本化数据。

就诊模板的药品引用默认数为 1，因此默认要求挂载 `medications` 目录；仅需诊断和检查时，设置 `new EncounterOptions { Medications = 0 }` 可保留无药品目录的旧流程。传入的患者、订单、商品、目录记录或设备由调用方持有，生成后仍按字段引用规则关联；生成期间请保持这些实体稳定。

更多完整调用示例见[领域关系补齐示例](domain-gap-examples.md)。

## 离线目录挂载与资源所有权

`UseCatalogFile`/`UseCatalogFiles` 适用于路径型目录：每个上下文创建自己的只读 SQLite 连接并拥有其释放。`UseCatalog(name, instance)` 默认借用传入对象；传 `ownsResource: true` 表示一次性转移所有权。电话、MIME、地理和币种适配器同样默认借用目录实例；`ownsCatalog(s): true` 时上下文负责释放目录。

```csharp
string dataDir = Path.Combine(AppContext.BaseDirectory, "catalog-data");
var options = new MockOptions()
    .UseCatalogFile("devices", Path.Combine(dataDir, "pci-ids.sqlite3"))
    .UseCatalogFile("airports", Path.Combine(dataDir, "ourairports.sqlite3"));
using var context = new MockContext(options);
var deviceCatalog = context.Catalog("devices");
DataSetInfo source = deviceCatalog.Info;
```

多次 `UseCatalogFile(s)` 会组合进同一个 `CatalogSet`。单上下文最多挂载 8 个文件，路径数据库单个不超过 512 MiB。`UseGeographicCatalogs` 可挂载多个邮编分片，按过滤后的记录数加权抽样。Catalogs 代码包不包含大型数据库；使用 `build/pack-data.ps1` 独立生成版本化归档，经 `build/restore-data.ps1` 与可信清单验证后随应用部署/更新，详见[恢复与迁移](catalog-data-migration.md)。

借用目录生命周期示例：

```csharp
using var catalog = SqliteMockCatalog.Open(Path.Combine(dataDir, "mime-db.sqlite3"));
var options = new MockOptions().UseMimeTypeCatalog(catalog); // 默认借用
using (var context = new MockContext(options))
    _ = context.Files.Metadata("json");
// 上下文释放 provider；目录仍由外层 using 释放。
```

如显式设置 `ownsCatalog: true`，应只将实例转交给一个上下文；不要把同一个已转移所有权的实例复用于多个上下文。已打开的生成/查询任务结束后再释放所属上下文。外部目录快照、许可和覆盖边界见 [catalog-data-manifest.md](catalog-data-manifest.md)。

## Schema 生成语义

Schema 适配包实现 Draft 2020-12 编译、验证和约束样本生成。外部 `$ref`/`$dynamicRef` 文档必须通过 `UseSchema(..., documents)` 预加载，不会在运行时访问网络或任意文件。`format` 断言可由配置控制，`content*` 不会自动变成验证约束。

每个候选 JSON 都会使用编译后的 Schema 进行验证。约束矛盾只有能够确定时才报告 `ProvenContradiction`；达到搜索步数、深度、正则超时或尝试预算时报告 `BudgetExhausted`，不能据此推断 Schema 无解。Schema 文档字节数、文档数、编译缓存数和每条记录搜索步骤都受预算控制。`JsonSample.Json` 保持数值精度；序列化器会把它写成 JSON 值，而不是被再次转义的字符串。

```csharp
var options = new MockOptions { Seed = 42 }
    .UseSchema("invoice", schemaJson, preloadedDocuments, new SchemaOptions
    {
        MaxSearchSteps = 100_000,
        RegexTimeout = TimeSpan.FromSeconds(1)
    });
using var context = new MockContext(options);
var sample = context.Schema("invoice").Generate();
```

上下文拥有已编译 Schema evaluator，并在 `Dispose`/现代目标的 `DisposeAsync` 时释放。领域级语义样本和标准测试状态记录在任务验收报告中；验证器通过官方测试并不意味着任意复杂约束都能在预算内生成样本。

## 链式 Schema 与可配置航班

新入口和示例见 [本轮功能示例](domain-next-examples.md)。`ConfiguredFlight(FlightOptions)` 将出发国家、到达国家及航空公司国家拆分配置，也可指定当前目录中的记录；`Seats` 会复制子选项。旧 `Flight(string)` 保持原有行为。

当前核心重放契约为 `domain-v6`，内置数据身份为 `china-2025-12-31-domain-v5`；Schema 编译身份包含 `schema-search-v5`，目录集合身份为 `catalog-set-v2`。当前实现不恢复使用旧契约或旧 Schema 身份的历史记录；没有挂载 Schema 也不会使旧生成契约自动兼容。历史输出只有在原代码、运行时、数据与配置环境齐备时才可能复现；否则应使用原始业务输入在当前版本重新生成并保存完整重放信息。
