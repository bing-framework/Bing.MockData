# 能力升级本地交付报告

Task：mockdata-capabilities-22。日期：2026-10-25。基于工作区原有修复继续实现，保留 `docs/execution.md`。全量 22 项及阶段映射见 [任务文档](capabilities-plan.md)，用法见 [指南](capabilities-guide.md)。

## 交付

统一 `IRandomizer<out T>` 和 MockContext；非空生成及显式概率包装；带版本的记录/字段随机派生和 JSON 重放；新数值日期类型与精确金额刻度；可冻结对象规则、关联关系和六类可覆盖业务模板；中国县级以上 3212 条离线区划；Unicode、边界/异常值、唯一序列；可替换的外部字典快照；明确资源所有权、同步/异步释放和取消；有界按序并行、JSON/JSONL/CSV 流式导出；双目标包及独立消费者。

实现状态 PASS（22/22）；测试状态 PASS；资源证据 PASS；本地包消费 PASS；外部 CI 状态 BLOCKED_EXTERNAL。所有本地 OPEN_ACTIONABLE 均已关闭，停止自动审查修复。没有执行 Git 提交、推送、版本提升或 NuGet 发布。

## 影响分析与验证选择

ChangedFiles：核心接口/选项/生成器、新上下文/对象/模板/地区/导出 API、回归与新能力测试、双目标项目、构建脚本、锁文件、文档。
ChangedProjects：Bing.MockData、Tests、PackageSmoke、CapabilityProbe。
ChangedPublicContracts：统一接口、非空返回、显式可空包装、移除动态工厂，新增公开 API。
ChangedRuntimePaths：随机派生、批量/并行、金额、对象嵌套、字典 IO、释放与输出。
ChangedProviders：内置生成器、Fare 固定 IBAN、NLipsum 只读词库、内嵌区划。
ChangedTFMs：netstandard2.0 与 net10.0。
ChangedBuildPackaging：双目标 DLL/XML、许可证/数据来源、锁定恢复、隔离包消费。
ChangedBenchmarkHarness：新增不保留记录的流式 JSONL 接收端。
ChangedDocsOnly：最终候选后仅更新报告，不重跑已通过测试。
AffectedDependents：测试、包消费者和调用旧可空/动态 API 的用户。
RiskLevel：HIGH，按精准测试 → 最终完整 Release + 包消费执行。

最终候选只运行一次完整 Release 门禁：net10.0 **108/108**，netstandard2.0 **107/107**，均无跳过；两目标编译零警告零错误。netstandard 测试在 .NET 10 宿主加载真实 netstandard2.0 程序集，另有 netstandard2.0 消费项目编译验证。独立 net10.0 包消费程序运行成功。打包失败替身测试覆盖所有步骤与缺失目标资产。区划离线转换输出与已提交的资源内容逐字一致。

运行环境：Windows 10.0.19045、24 个逻辑处理器、SDK 10.0.400、运行时 10.0.11。资源表见任务文档：百万串行约 2.43 秒，最终并发度 4 约 1.32 秒，真实最大并行 4、最大排队 256；进程峰值约 52.5 / 54.6 MiB。累计分配约 2.89 / 2.95 GB，属于整个运行累计量，不能当作同时存活内存。未设未经确认的吞吐/内存阈值。

## 审查闭环

| Finding | 状态 | 修正与验收 |
|---|---|---|
| 文本预算误计默认字符表 | CLOSED | 区分字符表与输出长度；低预算普通文本回归通过 |
| 极值金额舍入破坏步长 | CLOSED | 按 decimal 可精确表示的规范系数分组抽样；正负极值、0.03 步长、单点区间回归通过 |
| 正则输出长度不足以界定生成工作 | CLOSED | AST 最大步骤/模式预算、有限量词、深度限制；修复两位十六进制转义及代理对重复语义 |
| 空批次/只读集合绕过生命周期或预算 | CLOSED | 快照检查根状态，支持只读集合 Count，断言释放后零条调用报错 |
| 根模板便利入口重复使用记录零 | CLOSED | 独立递增模板记录索引，批量 At 规则继续确定性 |
| 文本与网络入口预算/循环取消遗漏 | CLOSED | 手机长度提前拒绝、旧文本循环令牌检查、网络完整输出预算 |
| 并行枚举提前结束保留未消费窗口 | CLOSED | finally 清空窗口；保留已释放枚举器的弱引用回收测试通过 |
| 外部字典 UTF-8 与极大字节上限 | CLOSED | 严格 UTF-8，防初始读取长度溢出；取消/异常释放与原子替换回归通过 |
| 工厂返回按索引生成器时重置行号 | CLOSED | 沿用传入记录序号和取消令牌；注册快照及单条/批量测试通过 |

轮次：R1 实现与精准验证；R2 上表边界修正、针对性验证及文档收口；R3 最终候选完整门禁与结果复核。R3 无新增 OPEN_ACTIONABLE，不开启下一轮。

## 限制与外部门禁

- ACCEPTED_LIMITATION：重放要求相同库/运行时/规则/配置/内置与外部数据。ReplayInfo 不序列化委托、外部资源和字典内容，调用方重新提供相同定义。
- ACCEPTED_LIMITATION：仅提供源数据的真实层级；台港澳不补造县区，无县级代码时不生成人物大陆身份证。街道门牌、英文地址和账号均为模拟内容。
- ACCEPTED_LIMITATION：正则有限子集；不能可靠约束的模式立即拒绝。Fare 仅用于有限内置 IBAN 模式，不承诺中断底层编译。
- ACCEPTED_LIMITATION：先取消并等待活动工作结束，再释放根上下文。自定义委托内部循环/外部 IO 自行检查令牌，库不强制中断用户代码。任意自定义构造对象图不做反射深遍历预算扫描。
- ACCEPTED_LIMITATION：并行采用 256 记录窗口；提前停止可能已生成当前窗口剩余项。标准枚举器可持有当前单项，生成器不缓存历史；未消费窗口在 finally 清空。
- ACCEPTED_LIMITATION：导出失败可能留下部分结果；JSON 可未闭合，不承诺输出回滚。可用临时文件方式接入。
- VERIFIED_BOUNDARY：已确认默认预算拒绝路径、字典释放、去重耗尽、嵌套深度、百万条成功和有界并行；资源实测不等于所有工作负载保证。
- BLOCKED_EXTERNAL：远程 Windows/Linux CI 未执行；现有矩阵已接入双目标 pack 脚本。没有用重复本地测试代替。
- NOT_APPLICABLE：新增能力没有等价历史 before；本轮不发布、不改版本。
- BLOCKED_APPROVAL / DEFERRED：无。

## 证据索引

- 完整 Release/打包/隔离消费者日志：`artifacts/capabilities/final-gate.log`。
- 候选清单与包 SHA-256：`artifacts/capabilities/final-manifest.json`。
- 包内资产清单：`artifacts/capabilities/package-assets.json`。
- 原始资源探针及程序集身份：`artifacts/capabilities/probe/`，采用文件由任务文档显式列出。
- 本地包：`artifacts/capabilities/final/package-runs/414dfe6e5f4d49b98b3998dea2c657ab/packages/Bing.MockData.0.0.1-preview-20190114-1.nupkg`。

最终本地门禁 8/8；外部 CI 门禁 0/2。Next Action：STOP。Goal Status：STOPPED_BLOCKED（仅外部 CI）。本轮授权的本地实现、测试、文档与包消费交付完成。
