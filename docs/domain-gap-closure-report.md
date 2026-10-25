# 本轮缺口补齐交付报告

日期：2026-10-25。先完成 [解决方案](domain-gap-closure-plan.md)，再按生成正确性/资源、关联场景、验收顺序实施。**本地代码与包消费通过；严格目录关系门禁仍为 FAIL，远程 CI 未运行，整体发布状态为 PARTIAL。** 不以入口存在或验证器标准测试通过替代业务生成验收。

## 补齐结果

|项|结果|对应实现及验证|
|---|---|---|
|G01 Schema 长度/数组|CLOSED|移除隐含 16 字符格式上限；minContains/prefixItems/uniqueItems 合并；超 64 位数值区间采样。`GenerationGapTests`|
|G02 Schema 定位/生成|CLOSED|JSON Pointer 定位子实例；验证失败收集路径；普通锚点不能被同名 dynamicAnchor 错误覆盖。22 项新格式/组合/定位/预算/锚点回归|
|G03 AutoBuilder|CLOSED|长度与格式组合、基础集合元素、枚举和时间类型；缓存属性访问；失败携带成员及约束。`AutoBuilderGapTests`|
|G04 资源释放|CLOSED|构造失败释放同步及异步自有资源；目录释放尝试全部实例后汇总异常，重复释放幂等。`LifecycleRollbackTests`、`CatalogSetLifecycleTests`|
|G05 用户/企业/文本|CLOSED|短名、空名、组合字符及 emoji 头像；企业简称；中英文词槽组合。`RelationshipGapTests`|
|G06 组织权限|CLOSED|权限实体、员工角色和角色权限关联表；配置快照、键检查、无环部门、整体预算|
|G07 物流库存|CLOSED|仓库库位、按订单明细分包、运单及签收；具商品/库位引用的非负库存流水|
|G08 关联日志|CLOSED|共享 TraceId、有效父子 Span、嵌套时间、有序事件及单条重放；G06–G08 均有串并行一致性测试|
|G09 教育|CLOSED|专业、班级、课程配置、同专业 Cohort、结构化书目作者/出版商及缺值保持。`EducationGapTests`|
|G10 医疗|CLOSED|检查项目及记录、对应观察结果、带数据版本的药品引用；不声称适应证关联。`IndustryGapTests`|
|G11 行业便利性|CLOSED|车辆厂商和类别同源字段；设备指标/单位/范围/时间间隔配置、单条及并行重放|
|G12 目录关联|VERIFIED_SOURCE_BOUNDARY|CLDR 导入遗漏已修复；机场关系通过；PCI 原始快照仍有 46 条缺失厂商引用，严格报告保留 FAIL|
|G13 文档/本地门禁|CLOSED（本地范围）|新示例、迁移、双目标资产实际加载检查、七包隔离消费、脚本失败中止；远程 CI 单列未运行|

新增用法见 [关联场景示例](domain-gap-examples.md)，可执行消费程序为 `tests/Bing.MockData.DomainPackageSmoke/Program.cs`。该程序已在隔离的 NuGet 包消费者中编译并运行，覆盖短邮箱 AutoBuilder、格式 Schema、组织、物流、库存、跟踪、教育、医疗、车辆及测量。

生成契约改为 `domain-v2`，内置数据身份为 `china-2025-12-31-domain-v2`；旧重放不能只替换标签后继续使用。NuGet/程序集版本未提升。调用方传入的实体引用需在生成期间保持稳定；配置选项和本轮明确承诺的结构快照已复制。

## 当前候选验收

|检查|现代资产|标准资产|证据|
|---|---:|---:|---|
|核心回归|129/129|127/127|`final-gate.log`，包含实际目标资产断言|
|目录回归|12/12|12/12|`catalogs-modern-assets.log`、`catalogs-standard-assets.log`|
|Schema 回归|43/43|43/43|`schema-modern-assets.log`、`schema-standard-assets.log`|
|行业回归|12/12|12/12|`domains-modern-assets.log`、`domains-standard-assets.log`|
|合计|196/196|194/194|标准库由 .NET 10 测试宿主运行，不代表所有历史运行时兼容性|
|JSON Schema 官方矩阵|2360/2360|2360/2360|`schema-official-suite-net10.0.json`、`schema-official-suite-netstandard2.0.json`|
|七包 DLL/XML 及隔离消费|运行 PASS|编译 PASS|包目录 `evidence.json`；SQLite 原生库查询成功|
|脚本故障注入|PASS|共用脚本|`pack-failure-tests.log`、`core-pack-failure-tests.log`|
|新增目录严格关系检查|FAIL：PCI 来源缺失|共用离线数据|`catalog-relations.json`|
|远程 Windows/Linux CI|NOT_RUN|NOT_RUN|BLOCKED_EXTERNAL，未提交/推送触发远程运行|

上表短路径位于 `artifacts/domain-gap-closure/`。官方套件固定提交为 `89e83a74014617ffbe79a56d83d14d054cd11417`。新生成测试独立于官方验证矩阵；有解 Schema 仍可能在预算内找不到候选，这类失败是 `BudgetExhausted`，不宣称无解。正则生成仍是预算内候选搜索，不能据此宣称所有合法模式必然生成成功。

最终七包和目录归档：`artifacts/domain-gap-pack/19f4db8e4daf48eb91ff540f92d5f605/`。最后一次生产代码门禁后仅调整测试项目的目标选择、资产断言和文档；生产包证据仍有效。发现旧 Catalogs 标准目标测试未选择标准 DLL，行业依赖也混入现代 DLL，因此明确修复引用配置并重跑三组受影响测试的两套资产；旧测试结果不用于证明这些目标选择正确。每个实际加载的第一方依赖 DLL 都有 TargetFrameworkAttribute 断言。

## 数据修复及仍存在的来源边界

CLDR 从相同的 49.0.0 原始输入重建为 `49.0.0-import-v2`，从 1,004 条变为 1,141 条：328 个区域、307 个币种、506 条区域币种关系。增加的是原始关系引用到但此前漏导入的历史端点；没有本地化名称时保留来源代码并明确标记，未编造名称。地区/币种外键缺失均为 0。21 个 SQLite 当前合计 5,413,352 条记录，另有核心中国区划 3,212 条。原始来源与其他数据快照保持不变。

OurAirports 国家/地区引用及二者的一致性均通过；448 条 U-A 伪区域关联保留上游定义，仍引用实际存在的区域节点。PCI 设备厂商、子系统所属设备、分类层级均有效，但 46 条 subsystem.subvendor 引用涉及的 6 个厂商 ID 在同一 pci.ids 原始快照中没有 vendor 定义。46 条均逐项核对原文一致，来源证据不匹配数为 0。报告保留 FAIL 和实际缺失数量，分类 VERIFIED_SOURCE_BOUNDARY；没有删除来源行、虚构厂商或把计数归零。未来若要求所有关系严格闭合，需要新的上游快照或明确的数据筛选策略，这轮不伪称已解决。

首次失败证据 `catalog-relations-pre-import-v2.json` 与修复后证据分开保存。关系验证脚本新增 7 个小型独立 fixture 测试通过；C# CLDR 全来源关系验证包含在目录回归中。历史词库来源尚未完整追溯，继续保留既有披露。

## 规模测量及证据复用

请求日志模型新增 ParentSpanId，重新执行不保留结果的万条诊断与百万条正式测量。测试环境 Windows 10、.NET runtime 10.0.11、SDK 10.0.400。

|百万条请求日志|串行|显式并发度 4|
|---|---:|---:|
|消费数量|1,000,000|1,000,000|
|耗时|10.50 秒|2.83 秒|
|吞吐|95,234 条/秒|353,216 条/秒|
|实际最大并行度|1|4|
|最大排队记录|0|256|
|累计分配|7,947,401,880 字节|7,956,537,000 字节|
|进程峰值工作集|58,359,808 字节|63,574,016 字节|

累计分配不是常驻内存。数值为本机本轮观测，不设新增发布阈值。探针只增加可选工作负载过滤，默认三类行为不变；未改变的目录抽样和时间序列消费路径复用 `artifacts/domain-upgrade/formal-1000000.json` 中对应测量，不用重新跑相同探针替代缺失能力验证。关联场景按单记录有界图验证，不保留跨记录实体历史。

## 审查和停止状态

两轮可行动修正：第一轮收敛生成/关系预算及动态锚点问题；第二轮修正测试资产混用。当前 OPEN_ACTIONABLE=0；G12 为已核实来源边界，远程 CI 为外部未运行项，不因 PARTIAL 自动进入新修复循环。机器可读结果为 `artifacts/domain-gap-closure/summary.json`。

没有 Git 暂存、提交、推送、包版本提升或 NuGet 发布。原有未提交实现与历史交付记录保留。
