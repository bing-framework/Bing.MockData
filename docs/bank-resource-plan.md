# 银行资源替换与全球 BIN 目录实施计划

日期：2026-10-27。状态：本地实现与验收完成；远程 Windows/Linux 未运行。依据用户批准的 B01–B04 计划及 [解决方案](bank-resource-solution.md)。最终结果见[交付报告](bank-resource-report.md)。

## 固定决策与完成条件

| 项目 | 输入、范围和完成条件 | 状态 |
|---|---|---|
| B01 | bankcardinfo@2.0.4 / f0393148c98247348b515ece601939de51f12b23；固定归档、ISC 声明、署名、静态转换和差异证据 | CLOSED；6 项 fixture、幂等及计数闭合 |
| B02 | 八家银行；替换内嵌规则；旧入口与 Finance.BankCard 共用算法；类型/覆盖/预算/取消/重放验收 | CLOSED；双资产各 16,900 条矩阵及精准测试 |
| B03 | Techbuddie-Solutions/binlist-data@fcc907c81cf02b12e396b0c12a8c16472b47619a；全球离线目录和只读适配器 | CLOSED；458,051 行全字段核验，Catalogs 21/21 PASS |
| B04 | 身份与迁移、双资产、资源、百万条、七包消费、干净恢复 | CLOSED（本地）；远程两平台 BLOCKED_EXTERNAL |

保持 netstandard2.0;net10.0 和七个代码包。核心包不依赖 SQLite；全球目录独立分发。当前未提交实现与历史报告均保留，不执行提交、推送、历史重写、数据上传或发布。

## B01：静态转换

保存 npm 原始归档、index.js、package.json、README、作者及 ISC 声明，并核对研究阶段记录的校验值。原发布物没有独立 LICENSE，许可通知如实区分原声明与补充标准条款。

只解析声明数据，不执行 JavaScript。按原八银行顺序提取；bank Code 保留为 bing-legacy，来源别名映射为 100=PSBC、102=ICBC、103=ABC、104=BOC、105=CCB、301=COMM、308=CMB、305=CMBC。

完整键为银行/前缀/后缀长度/类型，完全重复去重并保留来源顺序；Length 包含校验位。未知语法、非法字段或预期外同前缀同长度跨类型冲突失败并定位。不修改 BankInfo.AddRule 的公开行为。

预期展开 855、重复 10、保留 845；与旧表重合 778、新增 67、移除 54。招商银行 39 组采用来源 CC 类型，15 条旧额外长度规则不带入。完整 PAN 长度 15–19。输出来源位置、重复、覆盖外计数、差集及闭合证据；重复转换一致。

## B02：生成契约

新增 `string FinanceData.BankCard(string bankCode = null, CardType? cardType = null)`；保留 IBankCardRandomizer 和工厂入口。

bankCode 为三位 ASCII 兼容代码，null 表示不限；空白/格式错误抛参数异常，Unknown/未定义类型抛参数异常。无覆盖抛包含查询与数据版本的 InvalidOperationException，并由 MockGenerator 添加字段/记录定位。

先筛选有规则的银行，均匀选银行、均匀选规则、生成正文、计算 Luhn。候选最大长度超预算时在消费随机状态前拒绝。两入口共享内部算法，各自使用受同步保护的随机状态；上下文入口沿用取消、预算、字段隔离、重放、批量与流式契约。不承诺真实账户、可支付或唯一号码。

## B03：全球目录

增加 CatalogImport 的 bank-bins 子命令；输出 bank-bins.sqlite3，Id=bank-bins-techbuddie，Version=fcc907c81cf02b12e396b0c12a8c16472b47619a-import-v1，Kind=bank-bin，Key=bin|原始前缀。

流式 CSV、事务暂存、验证后替换；完全重复计数合并，冲突重复失败。保留前导零、原名称、品牌、类型、分类、联系方式、国家、source/seen；未知字段为 null，查询列未知机构名用空字符串。seen 不宣称为银行核验日期。

预期 458051 行、CN 18389 行；保留 README、CC BY 4.0、上游署名、冲突记录与转换说明。取消或失败清理本次暂存，不破坏原目录。

在 Catalogs 包提供 `BankBins(this MockContext, string mountName = "bank-bins")` 和只读 BankBinRecord。BankBinCatalog 提供 Info、TryGet(bin,out record)、Count(CatalogQuery?=null)、Query(CatalogQuery?=null,CancellationToken=default)、Sample(CatalogQuery?=null)。验证数据集身份，不另开连接、不取得所有权。

精确查找接受 1–8 位 ASCII 前缀，不截断完整卡号。查询 Kind 只能为 null/bank-bin；条件在操作开始快照。合并上下文、查询与枚举参数取消令牌，提前终止释放链接。抽样使用当前上下文随机位；无筛选按序号访问，有筛选复用目录查询；无覆盖抛 CatalogCoverageException。

沿用八文件、512 MiB/文件、8 MiB 页面缓存、有界分页；不全量缓存、不模糊合并机构、不推导 PAN 长度或声称覆盖八位 BIN。

实施中核验补充：固定 CSV 实际含 7 条五位、454,496 条六位、3,548 条八位前缀，上游 README 的长度描述与数据不完全一致。按全量保真要求全部保留并支持精确查询；“有八位来源行”不等于已核验八位 BIN 实际分配或覆盖完整。此差异记入转换报告，不因 README 的概述丢弃来源行。

## B04：身份、交付与验证

生成契约 domain-v6；内置数据 china-2025-12-31-domain-v5；离线包 catalogs-2026-10-27-banks-v1。随机算法、Schema 和目录集合身份不变；旧重放明确拒绝。不修改 NuGet/程序集/文件版本。

更新现行迁移指南、来源台账、许可打包与消费样例；保留历史记录。接入现有 pack-data/restore-data、MOCKDATA_DATA_ROOT 和可信数据清单。

验证顺序：转换 fixture → 精准核心/目录测试 → 受影响项目 → 最终一次完整 Release 双资产、固定官方 Schema 矩阵和七包消费。核心测试补实际加载目标资产断言。

规则矩阵每资产 845×20=16900：Seed=39，参考时间 2026-10-27T00:00:00Z，记录 0–19；检查来源前缀/长度/独立 Luhn。另验类型/银行筛选、Unknown、覆盖错误、预算随机状态、取消、释放、字段隔离、重复枚举、单条重放、串并行一致。

目录验全量主键/计数/字段、未知值、前导零、短前缀、CSV 引号换行、重复冲突、取消/失败清理、借用/拥有/重复释放、提前终止、关闭后文件可移动删除、干净恢复与脚本失败中止。

新增银行卡与无筛选 BIN 工作负载：万条诊断 120 秒窗口，然后百万条串行和并发度4各一次，每格600秒窗口。不保留输出；记录吞吐、分配、峰值内存、实际并行度及队列。超时记录边界，不自动加时，不设新性能发布阈值。

证据位于 artifacts/bank-resource-closure/；N03 银行项在 B01、B02 和相关 B04 通过后按可追溯替换关闭。全球目录独立验收；远程 Windows/Linux 只记录同一代码与数据版本的实际结果。

## 影响与执行规则

ChangedProjects: Core、Catalogs、CatalogImport、核心/目录测试、资源探针与包消费。
ChangedPublicContracts: Finance.BankCard、BankBins/BankBinCatalog/BankBinRecord。
ChangedRuntimePaths: 银行规则选择和 Luhn 生成、BIN 数据投影与查询取消。
ChangedProviders: 新 BankBinCatalog 适配器；目录存储格式不变。
ChangedTFMs: 无。
ChangedBuildPackaging: 许可检查、BIN 数据清单与七包消费者。
ChangedBenchmarkHarness: 新银行/BIN 场景。
ChangedDocsOnly: false。RiskLevel: HIGH（数据与重放契约变化）。

主代理负责公共契约、适配器和最终集成；独立数据转换、fixture、文档例子委派 luna_worker。最多三轮自动审查修复，仅 OPEN_ACTIONABLE > 0 继续。外部 CI、未知上游更早历史不触发重复本地测试。

当前分类：Completed=B01–B04全部本地实现、7组本地验收、两路径万条诊断及百万条串行/并发4；OpenActionable=无；BlockedApproval=无；BlockedExternal=同候选远程 Windows/Linux（0/2）；NotApplicable=新 API 历史同构性能对比；AcceptedLimitations=历史格式/全球目录覆盖；VerifiedBoundaries=来源八位行与README差异、非法同Id目录不构成有效数据集；Deferred=官方实时卡表/完整PAN识别/全球银行实体归并。自动审查1轮；NextAction=STOP，不以外部门禁未运行触发重复本地修复。
