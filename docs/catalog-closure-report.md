# 目录覆盖与交付收口结果

2026-10-27。依据 [实施计划](catalog-closure-plan.md)。本轮本地可执行实现与验收已收口；N03 因银行前缀来源仍为部分完成，远程两平台未运行。此前未提交实现和历史报告均保留。

|项|状态|证据|
|---|---|---|
|N01 电话|CLOSED|全部 492 条规则可寻址；两资产各 20 记录/规则通过|
|N02 PCI|CLOSED|原始 42,549 条保留；派生 42,503 条，排除 46 条；分区、内容与关系检查通过|
|N03 资源|PARTIAL / BLOCKED_EXTERNAL|20 项实际嵌入资源全部入账；19 项已有核实来源或合格替换；Banks.json 仍未核实|
|N04 本地交付|CLOSED|52 文件版本化归档、可信清单、干净恢复、DataRoot、工作流及历史大文件迁移材料完成|
|N04 远程 Windows / Linux|BLOCKED_EXTERNAL|两平台均未运行；固定数据地址和对应远端代码候选尚未就绪|

本地实施以 N01、N02、N04-local 三项关闭，N03 的 19/20 资源关闭分别计数；不得合并描述为四项全部完成。远程门禁为 0/2。

## 实现与兼容性

- `Phone.NonGeographicNumber` 只接受无前导零、无加号的 1–3 位 ASCII 呼叫码，查询 `001:类型:呼叫码`，不推测国家。旧自定义 Provider 无须修改；可选能力未实现时明确抛出不支持。目录缺失仍为覆盖异常。修复阿根廷本地格式的前缀应用位置：首个格式引用是 `$2`，不能硬编码 `$1`。未改变公共正则求解路径。
- `pci-ids-resolved` 的版本为 `fe7fceb2e1153d495e1deefacf59c5d61c36ef8e-resolved-v1`。保留源记录稳定键与内容，序号重排连续；原始文件 SHA 未变化。派生五类关系全部闭合，原始严格关系仍报告 46 条缺失引用。
- 旧资源替换涵盖固定 Faker 名/姓、CLDR 国家地区、GeoNames 城市、中国区划兼容投影、职业、颜色、示例邮箱和原创模拟字符池。中文姓氏投影 399 条；IBAN/BBAN 保留原 54 国，固定 MIT `iban@0.0.14` 输入 102 条，明确排除旧范围外 48 条。覆盖变化与许可见[台账](legacy-resource-ledger.md)。
- 重放契约为 `domain-v5`，内置数据身份为 `china-2025-12-31-domain-v4`；旧契约/旧数据明确拒绝。随机算法、Schema、目录集合身份保持 `fnv64-mix-v1`、`schema-search-v5`、`catalog-set-v2`。NuGet/程序集/文件版本未提升。
- 恢复校验可信清单版本、字节数及 SHA-256；拒绝路径越界、符号链接、重复/缺失/额外条目及错误校验值。暂存完成后才供测试使用。显式 DataRoot 失效不回退。CLI 失败写报告并传播非零退出；工作流用 `always()` 上传证据。

## 本地验收

Windows，SDK 10.0.400，运行时 .NET 10.0.11。`netstandard2.0` 验证使用该资产，在 .NET 10 测试宿主执行。

|验证|结果|
|---|---|
|完整 Release — net10.0 资产|346/346：核心 169、目录 15、Schema 135、行业 27|
|完整 Release — netstandard2.0 资产|344/344：核心 167、目录 15、Schema 135、行业 27|
|固定官方 Schema 套件|两资产各 2,360/2,360，提交 `89e83a74014617ffbe79a56d83d14d054cd11417`|
|电话矩阵|两资产各 492 × 20 = 9,840；原始模式、E.164、分机、本地格式、重复枚举、JSON 重放与并行度 4 一致|
|电话 / PCI 生命周期|取消、提前结束、幂等释放、释放后调用失败、文件独占打开/移动/删除通过|
|目录全量|干净恢复目录 22 个 SQLite、5,455,855 条；大小、主键、元信息、JSON、连续序号、关系检查通过；原始 PCI 来源缺失单列|
|转换 fixture|PCI/目录 8 项、旧资源 10 项；实际 PCI 分区闭合与原始对照通过|
|恢复 fixture|7 项，覆盖正常恢复、损坏/缺失、条目哈希、路径越界、符号链接、取消清理和打包参数冲突|
|构建脚本|restore/test/build/pack 失败中止；DataRoot 无回退；空白路径和同址输出拒绝；Python 失败传播|
|七包消费|两套 DLL/XML 全有；六项核心数据许可全有；标准资产隔离编译、现代消费者运行及原生 SQLite 访问通过|

电话固定 Seed=39，ReferenceTime=2026-10-27T00:00:00Z，记录 0–19；流式逐条核对，不持有全部输出，仅保存每条规则通过数。

## 候选与证据

汇总：[summary.json](../artifacts/catalog-closure/summary.json)。数据版本 `catalog-closure-20261027-v1`，压缩包 360,017,030 bytes；可信清单 [catalog-data.lock.json](../build/catalog-data.lock.json)。归档 SHA-256 为 `ce8674f037e9367235415b1a03ed40f8a29672519457434c2cff0d8763aa9205`。

- 完整测试：`artifacts/catalog-closure/final/c41767772c4d426883bd75a9134d316b/reports/`，含 TRX、电话和 Schema 矩阵。该运行的包缺两个新增许可，包结果已由下项替代；测试结果仍有效。
- 最终包：`artifacts/catalog-closure/final-package/d5821ff2b20a49739949f1daa4b34b76/`，含 candidate/evidence、七包和两个隔离消费者。最终源范围 SHA-256：`36FA30629E96A17BD1CF6104D042699324752D41185BED9AF1060940C0380C28`。
- 最终修正仅改变 core csproj 的两个 `None Include` 和包内许可检查。七包全部 28 个 DLL/XML 资产与完整测试时逐字节相同，对比记录于 summary；因此复用完整测试，重新执行打包与隔离消费。
- PCI：`artifacts/catalog-closure/pci/derivation-validation.json`；目录：`catalog-validation.json`；资源：`resources/`；恢复：`restore.json`、`bundle/`；只读 Git 清单：`git-large-files.json`。
- 百万条证据为 `REUSED_UNCHANGED_SCOPE`：原 `artifacts/constraint-completion` 及 `artifacts/domain-upgrade/formal-1000000.json`。本轮未改变日志/时间序列/目录负载的生成、排队或查询热点，不生成新的性能结论。

## 审查与停止条件

自动审查修复两轮：R1 关闭空数据根路径与归档/清单同址覆盖两个问题；R2 关闭新增许可未进入核心包的问题。均有精准验证与复核，`OPEN_ACTIONABLE=0`。

- Completed：N01、N02、N04-local、N03 的 19 项资源与台账、本地测试及七包消费。
- Open Actionable：无。
- Blocked Approval：无当前本地步骤；历史重写、上传、提交推送和发布未执行。
- Blocked External：Banks.json 的来源/再分发证据；同一候选与数据版本的远程 Windows/Linux CI。
- Not Applicable：新入口与旧版本的等价性能比较。
- Accepted Limitations：号码仅保证元数据格式；城市/国家投影以固定来源覆盖为界；中文颜色中 133 项明确回退 CSS 名称；账户不代表实际分配。
- Verified Boundaries：原始 PCI 的 46 条 subvendor 引用在源输入中同样缺失，派生目录显式排除。
- Deferred：Schema 求解增强、行业便捷查询和更多模拟样本，依用户计划另行处理。

Implementation=PASS（当前可执行范围）；Tests=PASS；ResourceEvidence=PASS；ExternalGate=BLOCKED_EXTERNAL；Release=BLOCKED；NoProgressCheck=CHANGED；GoalStatus=STOPPED_BLOCKED；NextAction=STOP。用法见[迁移示例](catalog-closure-examples.md)，远程接入见[数据迁移材料](catalog-data-migration.md)。
