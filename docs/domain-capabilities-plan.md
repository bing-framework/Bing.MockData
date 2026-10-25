# 全领域数据能力升级执行记录

> **历史记录，已被后续审查修正。** 下文的“21/21”“全部完成”及旧门禁数字仅保留首轮记录，不能作为当前完整验收结论。后续发现了能力、释放与目标资产验证缺口；本次补齐及实际状态以 [补齐方案](domain-gap-closure-plan.md) 和 [补齐交付报告](domain-gap-closure-report.md) 为准。PCI 原始快照仍有 46 条悬空 subvendor 引用，远程 CI 未运行。

## 冻结范围

本任务执行用户批准的全部 21 项计划，双目标 netstandard2.0 / net10.0。允许模板破坏性升级；不提交、推送、提升包版本或发布。原有 capabilities-* 和 execution.md 记录保留。

基线提交：50bac60d902db3f2d4e88c76b647df6e7ab1266a。开始时间：2026-10-25。已有工作区实现不是 HEAD；tracked 补丁与全部源码 SHA256 清单保存在 artifacts/domain-upgrade/baseline-*，不得用当前候选覆盖。新增 API 的历史性能比较为 NOT_APPLICABLE。

## 21 项映射和状态

|编号|能力|实施状态|验收状态|
|---|---|---|---|
|1|用户资料、年龄范围、时间一致性|CLOSED|DomainTests：用户时间和人物一致性|
|2|企业、职业、统一社会信用代码|CLOSED|DomainTests：企业区划与校验位|
|3|中文及英文业务文本|CLOSED|双语词语、业务句式和段落；完整核心回归|
|4|商品、分类规格、条码|CLOSED|DomainTests：分类规格与 GS1 校验|
|5|币种、金额、账户流水|CLOSED|JPY/KWD 精度、账本和订单金额|
|6|电话元数据及格式|CLOSED|10 国手机/固话、来源格式和 E.164|
|7|邮编、坐标、附近点|CLOSED|GeoNames 全量、两片邮编、附近距离|
|8|HTTP 与成组客户端信息|CLOSED|日志/HTTP 时间及并行重放|
|9|文件元数据|CLOSED|MIME 目录、路径及文件时间|
|10|颜色转换、离线 SVG|CLOSED|SVG XML 转义和颜色转换|
|11|ULID、短标识、业务编号|CLOSED|ULID 时间、短标识及序列编号|
|12|分类异常输入|CLOSED|显式异常入口；正常生成回归|
|13|订单、支付、退款|CLOSED|七种订单状态、支付和退款约束|
|14|组织与权限|CLOSED|组织无环、员工和角色有效引用|
|15|物流与库存|CLOSED|包裹引用、事件排序、非负库存|
|16|分布、时间序列、日志|CLOSED|索引重放与三类百万条诊断|
|17|DataAnnotations、完整 JSON Schema 2020-12|CLOSED|双目标 2360/2360 官方矩阵及生成/约束测试|
|18|Transport|CLOSED|车辆/航空目录与模板集成|
|19|Education|CLOSED|ROR/图书目录、学生选课引用|
|20|Healthcare|CLOSED|ICD/RxNorm 全量关系与就诊集成|
|21|Devices|CLOSED|PCI 型号厂商、IMEI 与测量模板|

## 阶段与契约

1. 资源整理、目录接口、版本集合、生命周期和各包双目标基础。
2. 核心门面 1–12，保留字段随机隔离和预算。
3. 业务场景 13–16，金额派生、时间有序、引用有效、有界关系图。
4. 行业扩展 18–21，真实目录与模拟对象分离，缺少覆盖明确失败。
5. 自动约束和完整 Schema、文档迁移、最终 Release 与包消费门禁。

目录默认：8 个文件，单文件 512 MiB，SQLite 页面缓存 8 MiB/连接，Pooling=False。上下文构建时冻结挂载；借用实例不释放，工厂创建的实例由上下文释放。运行时不联网。

Schema 默认：64 个实例/上下文，16 MiB 文档，256 个引用文档，100000 搜索步/记录，单正则 1 秒。Corvus.Text.Json 5.7.9 实例验证器负责完整语义，生成器另外验收；不能把预算耗尽报告为已证明无解。

数据来源：已有中国区划 3212 条；CLDR、libphonenumber、GeoNames、mime-db、VehiclesDB、OurAirports、Wikidata、ROR、Crossref（最多 100000 条合格图书）、ICD-10-CM FY2027、RxNorm CPC、PCI IDs。原始输入、固定版本、许可、下载日期、转换规则、排除计数及 SHA256 均须进入清单。来源/再分发未核实的目录为 BLOCKED_EXTERNAL，不打包。禁止用小样本替代广泛目录。

## 影响分析及验证规则

ChangedProjects：核心、Catalogs、Schema、Transport、Education、Healthcare、Devices、导入与测试工具。ChangedPublicContracts：门面、模板模型、重放和扩展资源。ChangedRuntimePaths：字段生成、场景、目录 IO、Schema。ChangedTFMs：两套。ChangedBuildPackaging：新增独立包和消费验证。RiskLevel：HIGH。

先 L0 构建与 L1 精准行为测试，阶段收口扩大 L2/L3/L4；百万条串行及并发 4 仅在新目录/日志/时间序列正式检查点运行。最终一次新鲜完整 Release 和包消费；远程 Windows/Linux CI 状态独立记录。自动审查修复最多三轮，仅 OPEN_ACTIONABLE 进入修复。

## 阶段结果与验收证据

五个阶段的本地实现均完成。七个代码包（核心 + 六扩展）保留现有版本号，各输出 `netstandard2.0` / `net10.0`。21 个 SQLite 文件共 5,413,215 行，另有核心内置中国区划 3,212 行；实际覆盖和每个来源的许可/排除规则见 [目录清单](catalog-data-manifest.md)。

完整验收与已知边界见 [最终报告](domain-delivery-report.md)。示例见 [21 项示例](domain-examples.md)，升级调用见 [迁移说明](domain-migration.md)。旧修复记录与初始 baseline 均保留。

## 最终执行状态

- Task：domain-capabilities；收口审查最多三轮，当前 OPEN_ACTIONABLE=0。
- Implementation：21/21，PASS。
- Local Release Gates：8/8，PASS。外部 Windows/Linux CI：0/2，BLOCKED_EXTERNAL（未触发远程运行）。
- Changed Scope：核心公共契约、领域模型、目录、Schema、四行业包、数据导入、测试、脚本、文档。
- Validation：L0 双目标构建；L1/L2 精准与完整测试；L3 隔离包消费者；L4 现代目标 148 项、标准程序集 147 项；L5 三种工作负载各百万条串行/并发 4。
- Reused Evidence：最终仅修正 Schema 错误重放信息后，重新验证 Schema 两目标；其余未改生产路径复用完整 Release。构建脚本源配置修正只重跑包门禁及失败中止测试。
- Completed：21 项本地实现、21 文件离线目录、完整标准矩阵、双目标七包、迁移与证据。
- Open Actionable：无。
- Blocked Approval：无。
- Blocked External：远程 Windows/Linux CI 尚未运行；本地结果不替代。
- Not Applicable：新增能力没有同构历史性能基线；未执行发布操作。
- Accepted Limitations：目录覆盖遵从来源；不声明真实航线、TAC/VIN 映射或诊疗适应关系；有限搜索失败报告预算耗尽；仅 SVG 文本图片。
- Verified Boundaries：GeoNames 全量邮编和 RxNorm 关系超过单文件默认预算，已确定性拆片，所有交付文件满足 512 MiB；队列上限 256。
- Deferred：无本地计划项延期。
- No-Progress Check：CHANGED；最终审查无新增可执行问题。
- Next Action：STOP。
- Goal Status：STOPPED_BLOCKED（本地交付完成，外部 CI 待实际运行）。
