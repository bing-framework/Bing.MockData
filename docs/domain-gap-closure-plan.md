# 全领域能力审查缺口补齐方案

日期：2026-10-25。依据上一轮只读审查，修正此前“21/21 全部完成”的结论。本任务保留旧实现、原始目录及历史测试证据；不提交、推送、修改包版本或发布。

## 解决方案与实施顺序

### 一、生成正确性和资源释放

1. **G01 Schema 长度与数组约束**：区分标准约束的硬上限和默认采样范围；格式/正则生成只受显式长度及资源预算约束；先合并 minItems/minContains/prefixItems，再选择合理默认长度。补格式、组合、contains/uniqueItems、Unicode 和约束冲突回归。
2. **G02 Schema 定位及生成验收**：生成搜索携带 Schema JSON Pointer 和实例 JSON Pointer；子字段失败保留位置。继续以完整验证器验收每个输出；补独立生成矩阵，明确预算失败不等于无解。
3. **G03 AutoBuilder**：统一标量和集合元素的类型生成；按字符串长度与格式组合产生候选，保持最终 DataAnnotations 验证与有界重试；配置期缓存属性访问，约束失败定位到类型/成员/约束。不能自动生成的用户类型仍要求注册。
4. **G04 生命周期**：上下文构造失败时回滚同步及异步自有资源；目录释放尝试所有自有实例并汇总异常，保持幂等及借用语义。通过异常注入验证释放计数和后续资源清理。

### 二、业务关联与使用便利性

5. **G05 用户/企业/文本**：头像按完整 Unicode 字符取首部并允许短用户名；中文简称从模拟企业名称结构取得；文案由词槽与句式组合，保留双语、完整句子、长度和取消预算。
6. **G06 组织权限**：增加权限实体、员工角色及角色权限关联记录和可配置角色定义；默认仍可一行生成。快照配置，检查键与引用，集合/深度预算约束整组关系。
7. **G07 物流库存**：增加仓库、库位、包裹、运单和具商品/位置引用的库存事件；由已有订单明细产生包裹分配，数量不能超发；库存余额由合法入出库推导，时间与签收一致。保留旧便捷入口。
8. **G08 关联日志**：增加有界请求跟踪场景，共享 TraceId、有效父子 Span、事件有序；独立日志和时间序列接口保持现有契约。
9. **G09 教育**：增加专业、班级、可配置课程、结构化书目/作者/出版商入口及整组学生选课关系；真实书目保留来源缺失值，不用随机 ISBN 填补目录事实。
10. **G10 医疗**：增加检查项目、检查记录及就诊药品引用；目录编码与名称同源，事件有序；药品引用只表示模拟场景，不表达适应证关系。
11. **G11 行业便利性**：车辆厂商/类别增加结构化访问；设备测量允许指定指标/单位/范围，不声明目录具备未经来源证实的能力。

### 三、验证与交付

12. **G12 目录关联验收**：在已有只读验证器补 CLDR 区域币种、PCI 层级、机场国家/地区关系检查，区分明确缺失引用与来源明确缺值；不重下载；若发现导入遗漏，以相同 pinned raw 重建并提升导入身份。保留原始排除和覆盖说明。
13. **G13 文档和最终门禁**：逐项记录真实完成状态与最小示例；修正旧报告的当前状态，保留历史结果。执行双目标 Release 测试、官方 Schema 验证矩阵、隔离包消费及构建脚本失败测试。新回归与业务场景独立验收，不用官方验证器用例代替生成验收。

## 影响分析和证据策略

- ChangedFiles/Projects：核心 AutoMockBuilder、MockContext、业务模型与模板；Schema；Catalogs 释放；Education/Healthcare/Transport/Devices；对应测试、目录验证脚本与文档。
- ChangedPublicContracts：新增场景/模型/配置，现有便捷调用尽量兼容；生成内容改变需更新内部生成契约身份（不提升包版本），拒绝旧规则重放。
- ChangedRuntimePaths：约束候选、构造回滚、释放、业务图生成；影响所有领域消费者。
- ChangedTFMs：netstandard2.0 与 net10.0；异步资源回滚仅现代目标。
- ChangedBuildPackaging：不改变包划分和依赖版本；最终重新打包消费。补充发现测试目标混用，修复三个测试项目的直接引用和输出隔离，新增逐依赖资产断言。
- ChangedBenchmarkHarness：DomainProbe 新增可选工作负载过滤；请求日志模型变化，重跑万条/百万条；目录和时间序列未变热路径复用历史证据。
- RiskLevel：HIGH。每组先精准测试，阶段收口扩大；最终一次完整门禁。目录新关系校验做一次正式全量运行，不重复历史哈希和下载。
- 主代理负责 Schema/AutoBuilder/场景设计及集成；边界明确的释放修复、目录校验和测试/示例任务委派 luna_worker，文件所有权独立。

## 状态与验收台账

初始 G01–G13 均为 OPEN_ACTIONABLE。下表为本轮最终状态；验证明细见 [补齐交付报告](domain-gap-closure-report.md)。

|项|状态|验收证据|
|---|---|---|
|G01|CLOSED|GenerationGapTests：格式、组合、数组和预算|
|G02|CLOSED|生成路径、普通/动态锚点回归；两目标官方矩阵各2360项|
|G03|CLOSED|AutoBuilderGapTests + 核心两目标完整回归|
|G04|CLOSED|LifecycleRollbackTests、CatalogSetLifecycleTests|
|G05|CLOSED|RelationshipGapTests：短/空/Unicode 头像、双语文案|
|G06|CLOSED|RelationshipGapTests：权限引用、无环、快照与重放|
|G07|CLOSED|RelationshipGapTests：分包数量、运单、库存与取消|
|G08|CLOSED|RelationshipGapTests：父子Span、事件、并行重放|
|G09|CLOSED|EducationGapTests：整组引用与结构化原始书目|
|G10|CLOSED|IndustryGapTests：检查、观察、药品及时间|
|G11|CLOSED|IndustryGapTests：厂商类别、测量配置与重放|
|G12|VERIFIED_SOURCE_BOUNDARY|catalog-relations.json：CLDR/机场通过；PCI来源46条缺失，严格FAIL|
|G13|CLOSED|两目标196/194项测试、七包消费、故障注入；远程CI未运行|

外部门禁：远程 Windows/Linux CI 为 BLOCKED_EXTERNAL，未运行不得写 PASS。旧词库历史来源未完整追溯，保留披露，不伪造来源。有限目录覆盖按已确认产品边界保留。

## 最终轮次

Task：domain-gap-closure；Round：2/3；Implementation：12 CLOSED、1 VERIFIED_SOURCE_BOUNDARY；Local Release Gates：2/3 PASS（双目标测试、包消费），严格目录关系 FAIL（PCI 来源边界）。

Completed：G01–G11、G13 及 G12 导入修复/全量关系核对；Open Actionable：0。Blocked Approval：无。Blocked External：远程 Windows/Linux CI。Verified Boundaries：PCI 原始来源 46 条悬空引用，完整计数保留。Accepted Limitations：沿用已披露目录覆盖；未把来源缺失冒充全量关系 PASS。Deferred：无新增代码推迟。

Validation：现代196/196、标准194/194、官方矩阵各2360/2360；七包隔离消费、故障注入、百万条日志。证据位于 artifacts/domain-gap-closure/，包位于 artifacts/domain-gap-pack/19f4db8e4daf48eb91ff540f92d5f605/。

Reused Evidence：生产代码未再变化，保留完整门禁的核心测试及包消费；只重跑测试引用修正后的 Catalogs/Schema/Domains 两目标。旧目录/时间序列未变热路径保留历史百万条数据。No-Progress：CHANGED 后收口。Next Action：本地实现交付；不因 PARTIAL 或外部 CI 自动新一轮 Fix。Goal Status：LOCAL_COMPLETE_WITH_SOURCE_BOUNDARY；Release Status：PARTIAL。
