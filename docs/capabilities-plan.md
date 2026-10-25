# Mock 数据能力升级

Task: mockdata-capabilities-22

本任务采用用户批准的整体 API 重构，目标为 netstandard2.0 与 net10.0；保持之前已完成的修复。未授权提交、推送、版本提升或发布。

## 阶段与清单映射

1. 统一接口、MockContext、重放、基础类型、概率、集合、自定义生成器：#1、2、6、7、8、9、13。
2. 批量、序列、唯一性、字典、资源所有权和取消：#5、10、16、17、18。
3. 2025 年县级以上完整区划、地区与 Unicode：#12。
4. 对象规则、关系、模板、边界数据和诊断：#3、4、11、14、15。
5. 有界并行、流式导出、接入文档、双目标与验证：#19、20、21、22。

## 已批准契约

- IRandomizer<out T> 仅保留 Generate；非空生成与可空包装分离。
- 上下文冻结配置，公开实际 Seed、参考时间、地区、数据和派生算法版本；按记录序号/字段路径派生随机源。
- GenerateMany 立即生成；Enumerate 重复枚举从相同序列开头重放，不缓存结果。
- 默认上限：集合返回 10000，流式 1000000，文本 100000 UTF-16 单元，深度 8，集合字段 1000，尝试 100，去重 100000，单外部字典 16 MiB/100000 条；全部可调。
- 普通生成器不实现 IDisposable；上下文只释放自己拥有的扩展资源。调用方先停止活动任务再释放上下文。
- 内置中国区划固定 yescallop/areacodes 提交 2da87e7648f0da7e10dea39e8a613107ed3fe273 的 2025 年末 3212 条快照，保留 CC0 和来源；不伪造台港澳下级或行政中间层。
- 对象规则声明顺序执行，支持严格模式、构造函数、嵌套、集合与路径诊断。
- 并行结果按序，队列有界；JSON/JSONL/CSV 流式输出支持取消与 leaveOpen。

## 验证与执行

每次测试先做影响分析；精准测试优先，阶段收口/最终候选才扩大到全量。两套程序集分别验证共享契约，包包含两套资产，独立消费验证。万条诊断后做百万条串行与并发度 4 正式探针，记录实际并行/排队/耗时/内存，不擅设吞吐或内存发布阈值。

依 GOAL_RULES：只有 OPEN_ACTIONABLE 才触发修复；最多三轮自动审查修复；相同无进展不重复运行。外部 CI 单列，不用本地测试替代。

## 22 项实现映射

| 项 | 能力 | 实现入口 | 当前状态 |
|---|---|---|---|
| 1 | 统一生成契约 | IRandomizer、RandomizerExtensions、旧接口迁移 | CLOSED；见最终门禁 |
| 2 | 主入口与配置快照 | MockContext、MockOptions | CLOSED；见最终门禁 |
| 3 | 完整对象规则 | MockBuilder | CLOSED；见最终门禁 |
| 4 | 关联与一对多 | Collection、Order(users, products) | CLOSED；见最终门禁 |
| 5 | 小批与惰性批量 | GenerateMany、Enumerate | CLOSED；见最终门禁 |
| 6 | 种子与单条重放 | ReplayInfo、ForRecord、ForField、At | CLOSED；见最终门禁 |
| 7 | 新类型与金额 | NumberData、DateData | CLOSED；见最终门禁 |
| 8 | 概率及权重 | Boolean、Nullable、OrNull、Weighted | CLOSED；见最终门禁 |
| 9 | 集合选择 | Pick、Sample、Shuffle | CLOSED；见最终门禁 |
| 10 | 序列与唯一性 | Sequence、Identifier、UniqueScope | CLOSED；见最终门禁 |
| 11 | 业务模板 | MockTemplates、Models | CLOSED；见最终门禁 |
| 12 | 地区与 Unicode | ChinaDivisions、TextData | CLOSED；见最终门禁 |
| 13 | 自定义扩展注册 | Register、RegisterFactory | CLOSED；见最终门禁 |
| 14 | 显式异常/边界数据 | BoundaryData | CLOSED；见最终门禁 |
| 15 | 预检查及错误定位 | Validate、MockGenerationException | CLOSED；见最终门禁 |
| 16 | 外部字典 | MockContext.Dictionaries | CLOSED；见最终门禁 |
| 17 | 所有权及释放 | Dispose、DisposeAsync、leaveOpen | CLOSED；见最终门禁 |
| 18 | 取消与预算 | MockLimits、有限正则 AST、循环检查 | CLOSED；见最终门禁 |
| 19 | 有界并行 | EnumerateParallel、GenerationMetrics | CLOSED；见最终门禁 |
| 20 | 流式导出 | MockExport | CLOSED；见最终门禁 |
| 21 | 文档与接入 | README、guide、migration、XML、DI/xUnit | CLOSED；见最终门禁 |
| 22 | 双目标、锁与消费 | 测试目标选择、pack、PackageSmoke | CLOSED；见最终门禁 |

## 最终状态与验证

Implementation：22/22 CLOSED。Local Gates：8/8 PASS。External Gates：0/2，远程 Windows/Linux CI 未执行。公开 API、资源路径和双目标均已在最终候选上完成 Release 验证。详细审查见 [交付报告](capabilities-report.md)。

| 本地门禁 | 最终证据 |
|---|---|
| net10.0 完整 Release | 108/108，无跳过 |
| netstandard2.0 共享契约完整 Release | 107/107，无跳过；测试断言实际加载 .NETStandard 2.0 程序集 |
| 双目标显式构建与包内资产 | 两套 DLL/XML 均存在，构建零警告零错误 |
| netstandard2.0 独立包消费 | 隔离目录及独立缓存，锁定还原并编译成功 |
| net10.0 独立包消费 | 实际运行通过，含区划、模板、并行、DateOnly 与旧入口 |
| 打包失败中止 | 恢复、两目标测试、库构建、缺失包资产、打包、消费者恢复/构建/运行失败均中止 |
| 资源 | 万条诊断、百万串行与并发 4 正式验证；提前结束窗口弱引用回收测试通过 |
| 地区 | 3212 行原始数据离线重建与资源逐字一致，全量代码/父级/无环验证通过 |

Completed：22 项实现、迁移、文档、XML、DI/xUnit 示例与全部本地门禁。
Open Actionable：无。
Blocked Approval：无。
Blocked External：远程 Windows/Linux CI，未将本地 Windows 测试冒充远程矩阵结果。
Not Applicable：新能力没有历史等价 API；Git 提交、推送、版本提升、NuGet 发布均不在本轮动作内。
Accepted Limitations：见报告，包含同版本重放、外部资源与用户委托协作取消、部分输出和模拟地区样本。
Verified Boundaries：默认数量/长度/集合/深度/重试/唯一值/字典限制、有限正则预算、百万条流式输出与 256 项窗口。
Deferred：无。
No-Progress：无需继续循环，OPEN_ACTIONABLE=0。
Next Action：STOP。
Goal Status：STOPPED_BLOCKED（仅远程 CI 门禁；授权范围内的本地交付已完成）。

最终日志 `artifacts/capabilities/final-gate.log`；包资产 `package-assets.json`；候选身份 `final-manifest.json`。本轮完整 Release 门禁仅执行一次；之后只有文档更新及受窗口释放修改影响的并行资源复验，没有重复全量测试。

## 当前资源证据

固定 Seed=31817、参考时间 2026-01-01 UTC，.NET 10.0.11 / Windows；三字段记录（序号、12 字符文本、整数），JSON Lines 丢弃接收端，默认资源限制。预期均为 EXPECTED_SUCCESS，万条诊断后每个百万场景仅正式运行一次。不保留输出，不设置吞吐/内存硬阈值。

| 条数/并发 | 实际最大并行 | 最大排队 | 毫秒 | 累计分配字节 | 采样托管峰值字节 | 进程峰值字节 |
|---|---:|---:|---:|---:|---:|---:|
| 10,000 / 1 | 1 | 0 | 118.2641 | 30,756,128 | 12,325,472 | 51,068,928 |
| 10,000 / 4（最终） | 4 | 256 | 70.5164 | 30,951,736 | 12,682,136 | 52,338,688 |
| 1,000,000 / 1 | 1 | 0 | 2430.3772 | 2,887,540,200 | 19,100,896 | 55,062,528 |
| 1,000,000 / 4（最终） | 4 | 256 | 1323.5834 | 2,950,910,032 | 20,036,888 | 57,200,640 |

采用的原始文件：`artifacts/capabilities/probe/diagnostic-10000-serial.json`、`formal-1000000-serial.json`、`diagnostic-final-10000-parallel4.json`、`formal-final-1000000-parallel4.json`。程序集身份在同目录 `assemblies.json` 与 `final-assemblies.json`。原并行证据因窗口释放路径修改被替代，未覆盖原始文件；串行生产路径未改变，标记 REUSED_UNCHANGED_SCOPE。累计分配量不是常驻内存。峰值仅对本机工作负载有效，不宣称其他对象规则具有相同吞吐或内存。后续注释或非探针路径修改不重跑该矩阵，复用未改变路径证据。
