# 能力升级迁移对照

本轮允许破坏性 API 调整；本地版本号未提升。此前修复的闭区间、配置复制、锁定依赖及实例并发保护继续保留。原迁移说明中的固定空值概率和 Fare 任意正则入口被本次契约替代。

| 旧调用/行为 | 当前调用/行为 |
|---|---|
| 各接口重复声明生成/格式化/批量方法 | `IRandomizer<out T>` 仅有 `T Generate()`，格式化和批量在根命名空间扩展 |
| `int? / DateTime? / Guid? / TimeSpan?` 普通输出 | 非空 `int / DateTime / Guid / TimeSpan`；移除 `.Value`/`.HasValue` |
| `UseNullValues`、固定 10% 空值 | 删除；值类型 `.Nullable(probability)`，引用类型 `.OrNull(probability)` |
| 动态工厂入口 | 删除；使用强类型 `RandomizerFactory.GetRandomizer` 或 `MockContext` |
| `GenerateAsString()`、`Generate(bool)` 接口成员 | 添加 `using Bing.MockData;` 使用扩展方法，格式化默认 invariant |
| 依赖共享随机源组装对象 | 用 `MockBuilder<T>.RuleFor` 或 `c.ForField(path)` 获得字段隔离 |
| Seed 即全部重放信息 | 保存 `ReplayInfo`，包括固定参考时间、预算和版本；重放相同业务规则与外部数据 |
| 文本正则允许无界重复 | 使用有界 `{m,n}`；不支持的模式立即抛异常 |
| 全部结果一次装入列表 | 小批用 `GenerateMany`，大批用 `Enumerate` 或显式有界 `EnumerateParallel` |
| 不提供唯一性 | `Sequence`/`Identifier` 计算唯一序列，有限集合不放回抽样，通用 UniqueScope 有容量/重试上限 |
| 中国地址来自旧样本 | 新结构化模板使用固定 3212 条完整快照；旧单项地址入口保留兼容输出 |
| 逐个调用身份证辅助方法拼人物 | `Templates.Person` 一次生成一致字段；独立旧方法各次调用不构成同一人物 |
| 只发布 netstandard2.0 | 同时发布 netstandard2.0 / net10.0 DLL 与 XML；现代接口仅后者有 |

即使 Seed 相同，新旧版本输出序列也不承诺相同。已有自定义 `IRandomizer<T>` 仍可实现单值 Generate；批量/重放扩展要求配方，所以应通过 `MockOptions.RegisterFactory` 或 `MockContext.Generator` 接入，不能推断任意外部实例如何重启。工厂逐记录创建结果，资源默认由调用方管理；通过 `UseResource(..., true)` 明确转移扩展资源的释放责任。

升级完成后参照 [使用指南](capabilities-guide.md) 迁移对象规则、取消、重放和导出流程。
