# 约束生成与可配置航班实施方案

2026-10-25；承接用户“继续实现功能”。保留此前实现、未提交修改和历史验收；不提交、推送、提升包版本或发布。

## 本轮范围和设计

1. N01 对象约束生成：以有界固定点展开 dependentRequired/dependentSchemas，支持新依赖引入的属性、必填项及进一步依赖；按合并后的数量上限选择可选项，满足 minProperties 时优先利用已声明属性。additionalProperties 与 unevaluatedProperties 保留标准语义，由完整根验证器最终判断。避免用未声明属性填充一个禁止额外属性的对象。
2. N02 有限值及预算：合并 const/enum 的有限候选域，有限域按约束交集抽样；搜索取消、步数、集合、文本及深度预算继续受控；冲突或预算失败不能被错误报告成已证明无解。Schema 生成契约加入编译身份，防止旧规则静默重放。
3. N03 可配置航班及座位：新增 ConfiguredFlight(FlightOptions) 与 Seat(SeatOptions)，独立选择出发/到达/航空公司国家或显式目录记录；起飞窗口、飞行时长、座位范围可配置并冻结。旧 Flight(string) 保持原有行为。数据来源不代表真实运营航线。
4. N04 验收和示例：先精准回归，再完整受影响项目两套程序集测试；公共入口收口执行完整 Release 和七包隔离消费。更新示例及报告，保留 PCI 来源缺失与远程 CI 状态，不重跑无关目录全量扫描及百万条日志。

## 影响分析

ChangedFiles/Projects：SchemaSearch、CompiledSchema、TransportData、新配置/测试、包消费程序、文档。
ChangedPublicContracts：新增航班/座位配置入口；Schema 编译身份增加生成算法版本，不改变 NuGet/程序集版本。
ChangedRuntimePaths：Schema 对象候选求解及有限域选择；新增航班入口。
ChangedProviders：Schema / Transport。ChangedTFMs：netstandard2.0、net10.0。
ChangedBuildPackaging：无包结构或依赖版本调整；更新隔离消费例。ChangedBenchmarkHarness：无。
AffectedDependents：Schema/Transport 消费者。RiskLevel：HIGH（组合约束和确定性）。

## 验收案例

- 多级依赖、环形依赖闭包、依赖引入的数量/字段约束、在 min/maxProperties 下的大量可选字段、禁止额外属性的已知候选集合。
- const/enum 交集、JSON 数值等价、空交集、有限候选复合值；必须通过最终根 Schema 验证。
- 字段错误定位、取消、低步数/集合/深度上限、串并行一致和单条重放。
- 航班配置快照、国家/显式机场、有效座位范围、不同起降机场、时间范围、非法配置和取消。

N01–N04 初始 OPEN_ACTIONABLE。主代理负责求解器/身份/集成，luna_worker 负责 Transport 独立文件及精准测试。自动审查修复最多 3 轮，只有 OPEN_ACTIONABLE 才进入下一轮。上轮未变范围证据复用；新功能无同构性能基线，不人为设定性能发布阈值。

## 实施及精准验证

- N01/N02：已实现依赖闭包、可选属性数量规划、有限域交集和字面量预算；新增空属性名及字段路径编码。19 项新回归覆盖该范围，其中默认预算下 1,000 个已声明字段一次尝试成功。
- N03：新增 FlightOptions、SeatOptions、ConfiguredFlight 和 Seat；7 项精准测试通过。集成审查发现并修正“显式 Arrival + 随机 Departure”碰撞，使用两行 SQLite fixture 固定重现并验收。
- N04：已扩展隔离消费者，挂载 8 个目录验证新航班入口及链式 Schema。完整 Release/七包门禁已通过，实际结果见 [交付报告](domain-next-report.md)。
- Review Round：1；实现阶段的编译/用例纠正不作为无进展自动循环。未发现需要批准的破坏性公共变更；Schema 行为改变通过 schema-search-v2 编译身份隔离。
- Reused Evidence：未修改目录数据、核心日志/时间序列、生命周期与打包脚本；沿用上轮对应来源检查、百万条和脚本故障注入证据。新最终门禁不覆盖旧日志文件。

## 最终台账

|项|状态|验收|
|---|---|---|
|N01|CLOSED|链式/循环依赖、数量、属性名、单条与并行重放|
|N02|CLOSED|有限交集、数值/复合值等价、字面量预算及身份拒绝|
|N03|CLOSED|7 项新回归，两目标行业项目各19/19，隔离消费|
|N04|CLOSED|完整现代222/222、标准220/220，官方矩阵各2360/2360，七包消费|

Implementation: PASS；Test: PASS；Local Package Gate: PASS；Open Actionable: 0；Review Round: 1/3。
External Gate: BLOCKED_EXTERNAL（远程 CI 未运行）；Release: PARTIAL（既有 PCI 来源边界和远程门禁）。
Next Action: 本轮功能交付；无新可行动问题，不进入追加 Fix。无新增性能阈值或目录覆盖承诺。
