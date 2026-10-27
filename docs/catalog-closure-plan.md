# 目录覆盖与交付收口实施计划

2026-10-27。执行用户批准的[解决方案](catalog-closure-solution.md)和本轮实施计划；保留此前未提交实现、修复报告与证据。基准提交 ae04f8355a313d0fd4b674dd09f44aa57da02741，实际起点包含既有工作区修改。

|项|实施与验收|状态|
|---|---|---|
|N01|NonGeographicNumber、可选 Provider、492 条规则各 20 记录双资产矩阵及重放/格式/资源验证|CLOSED；最终双资产验收通过|
|N02|保留原始 PCI，派生 pci-ids-resolved；逐条排除清单、集合闭合及严格关系检查|CLOSED；最终双资产验收通过|
|N03|20 项嵌入资源台账；19 项已有来源或替换；Banks 保留来源阻塞|PARTIAL；19/20 核实，消费者验收通过|
|N04-local|版本化数据包、可信清单、原子恢复、DataRoot、CI 证据与大文件迁移清单|CLOSED；恢复、七包及隔离消费通过|
|N04-remote|同一候选和数据版本的 Windows/Linux 实际运行|BLOCKED_EXTERNAL|

## 公共契约

新增 `Phone.NonGeographicNumber(string callingCode, PhoneNumberType type = Mobile, bool extension = false)` 及可选 `INonGeographicPhoneMetadataProvider.GetNonGeographicPattern`。呼叫码是无前导零的 1–3 位 ASCII 数字，不含加号；只查询 001 区域。旧 Provider 无需改动，未支持时明确报错。PCI 原始/派生快照独立身份，通过原有挂载 API 选择。

两个目标、默认预算、冻结配置、所有权及版本重放保持；数据/生成行为变化更新对应重放身份。取消仍为标准取消异常。Git 暂存、提交、推送、历史重写、数据上传、包版本提升及发布不在本地执行范围。

## 执行与影响分析

顺序 N01 → N02 → N03 → N04-local → 最终门禁。主代理负责公共契约、兼容性和集成；独立转换/提取任务委派 luna_worker 并限定文件范围。

- ChangedProjects：核心、Catalogs、目录转换、测试、构建脚本；行业扩展为依赖验收。
- ChangedPublicContracts：非地理电话可选接口、数据目录参数；现有入口保留。
- ChangedRuntimePaths / Providers：电话生成/格式、嵌入资源加载、离线数据恢复；必要时精准修改正则路径。
- ChangedTFMs：netstandard2.0、net10.0；测试宿主 .NET 10，断言实际资产。
- ChangedBuildPackaging：数据归档/恢复、DataRoot、报告归集和 CI；ChangedBenchmarkHarness：无预设修改。
- ChangedDocsOnly：否；AffectedDependents：全部模板、行业与包消费者；RiskLevel：HIGH。

每阶段先 L0/L1，之后对应 L2/L3；最后一次新鲜完整 Release、官方 Schema 验证矩阵及七包消费。电话 Seed=39，ReferenceTime=2026-10-27T00:00:00Z，492 个规则各记录 0–19；两资产各 9840 个基础样本。若通用正则改变，追加 AutoBuilder/Schema 回归。未影响的百万条日志/时间序列/目录测量复用。

PCI 原始严格关系报告保持 46 条来源缺失；派生检查必须无悬空引用、内容与排除集合完整闭合。资源只有来源核实或等价替换后关闭，无法核实保留阻塞。恢复验证拒绝越界、缺失及校验错误；显式 DataRoot 失效不回退旧数据。远程缺候选/数据地址不以本地结果替代。

按 GOAL_RULES 自动审查修复最多三轮，仅 OPEN_ACTIONABLE 触发修复。正式证据放 artifacts/catalog-closure/；详细结果见独立交付报告，不覆盖旧任务结论。
