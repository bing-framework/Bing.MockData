# 银行资源替换与全球 BIN 目录交付报告

日期：2026-10-27。B01–B04 本地实现 4/4、本地验收 7/7 已完成；远程 Windows/Linux 0/2，NOT_RUN。范围对应[实施计划](bank-resource-plan.md)，未执行提交、推送、数据上传或发布。

## 已完成实现

- B01：静态读取固定 bankcardinfo@2.0.4；保存原归档、元信息、作者/ISC 声明及旧表基线。目标八银行 855 条展开、10 条完全重复、845 条输出；旧新交集 778、新增 67、移除 54。39 组招行类型差异及 15 条旧长度规则均有逐条证据。
- B02：新增 `Finance.BankCard`，与旧随机生成器共用规则选择及 Luhn 算法；覆盖参数、文本预算预检、取消、释放和字段隔离。内部身份改为 domain-v6 / china-2025-12-31-domain-v5；NuGet/程序集/文件版本未改。
- B03：只读 `BankBins()` 适配器已实现精确查找、计数、流式查询与抽样；458,051 条全字段导入验收 PASS，CN 18,389，重复 0，文件 172,437,504 B（164.4 MiB）。六个原始输入有固定 SHA-256 锁。
- 实际来源边界：CSV 长度分布为五位 7、六位 454,496、八位 3,548，与上游 README 概述不完全一致；全部保留原值，不据此声明已核验八位 BIN 分配或完整覆盖。
- B04：迁移文档、核心包 ISC 通知、七包消费者及 CI 已接入；新可信数据包已从干净目录恢复，最终候选双资产和包消费全部 PASS。

## 当前验证

| 验证 | 结果 | 证据 |
|---|---|---|
| 静态转换 fixture | 6/6 PASS；重复转换一致 | artifacts/bank-resource-closure/banks-fixtures.log、banks-conversion.json |
| 核心精准回归 | 两套资产各 6/6 PASS，包含实际加载目标断言 | tests/bank-core-matrix-modern.trx、bank-core-matrix-standard.trx |
| 银行规则矩阵 | 每套资产 845×20=16,900 条，前缀/长度/Luhn/重放 PASS | banks-matrix-net10.0.json、banks-matrix-netstandard2.0.json |
| BIN 适配器 fixture | 5/5 PASS；查询快照、取消、提前结束、借用/拥有、释放后文件移动 | tests/bank-bin-fixtures.trx |
| 目录受影响项目 | 21/21 PASS，包含全量 458,051 行投影、稳定键、位数和缺失字段计数 | tests/catalogs-affected.trx |
| 全球目录导入 | 全量 12 字段、索引、主键、计数、文件大小 PASS；源哈希及发布边界取消 fixture PASS | bank-bins-import.json |
| 打包脚本失败中止 | restore/test/build/pack 失败均立即停止 | pack-failure-tests.log |
| 数据包失败路径 | 7 个 bundle fixture PASS；缺失归档、无效显式 DataRoot 均停止 | bundle-fixtures.log、data-script-tests.log |

## 最终候选

唯一最终 Release/打包运行：`artifacts/bank-resource-closure/final/9f1a7c61386c46cd99868e8aeeccd431/`。现代资产测试 358/358，标准资产测试 356/356，无失败或跳过。核心实际资产断言通过；每套资产银行矩阵 845×20，官方 JSON Schema 固定提交 89e83a74014617ffbe79a56d83d14d054cd11417 的 2,360 项矩阵通过。标准资产使用 .NET 10 测试宿主，实际加载 netstandard2.0 库由断言验证。

七个代码包各自包含两套 DLL/XML，核心包 bankcardinfo ISC 和 NOTICE 检查通过；隔离 netstandard2.0 消费编译、net10.0 消费运行及原生 SQLite 访问通过。全部测试和消费者使用干净恢复的数据目录。包版本保持 0.0.1-preview-20190114-1。

数据包 `catalogs-2026-10-27-banks-v1`：58 文件 / 23 SQLite，382,219,455 B，SHA-256 `d1923d11d5ced8ace342566c93299fb5d7d7f7183cd5cef3b9c8f3b4133e0115`。可信清单为 build/catalog-data.lock.json；归档与恢复报告在 artifacts/bank-resource-closure/。旧清单与历史报告保留；N03 银行项以“完成可追溯替换”关闭，全球目录另有独立全量验收。

精确证据索引：[summary.json](../artifacts/bank-resource-closure/summary.json)、最终运行中的 candidate.json / evidence.json / reports/、工具及探针身份 tool-candidate.json。性能实测依赖的程序集 SHA 与最终包对应 DLL 一致。报告收口后仅改文档，复用已通过的最终代码门禁。

上述简写路径均相对于 artifacts/bank-resource-closure/。银行矩阵 Seed=39、参考时间 2026-10-27T00:00:00Z，规则记录序号 0–19。

## 新路径资源实测

本机 Windows 10 / .NET 10.0.11；不保留结果，正式每格 600 秒观察窗口，万条诊断 120 秒。分配量是累计托管分配；峰值是进程峰值工作集，包含预热及同进程先前格，不等于 retained heap。

| 工作负载 | 条数 | 并发请求 / 实际 | 最大排队 | 耗时 | 累计分配 | 进程峰值 |
|---|---:|---:|---:|---:|---:|---:|
| BankCard | 1,000,000 | 1 / 1 | 0 | 1.117 秒 | 723,312,664 B | 59,629,568 B |
| BankCard | 1,000,000 | 4 / 4 | 256 | 0.349 秒 | 731,414,352 B | 64,688,128 B |
| BankBin Sample | 1,000,000 | 1 / 1 | 0 | 22.522 秒 | 10,143,792,240 B | 73,445,376 B |
| BankBin Sample | 1,000,000 | 4 / 4 | 256 | 28.939 秒 | 10,157,082,592 B | 79,273,984 B |

四格计数均准确且 PASS；万条诊断见 bank-card-10000.json、bank-bin-10000.json，正式结果及程序集 SHA-256 见对应 *-1000000.json。不设置吞吐发布阈值；新入口的历史同构性能基线为 NOT_APPLICABLE。BIN 累计分配约 10.14 GB，但没有保留百万条结果，峰值约 70–76 MiB。实际并行度指生成工作者，目录查询仍按契约共用受同步保护的连接；该场景并发 4 比串行慢，不将其表述为数据库四路查询提速。

## 状态分类

Completed：B01–B04 全部本地实现及验收。OpenActionable：无。BlockedApproval：无。BlockedExternal：远程 Windows/Linux 尚无当前代码候选和已上传固定数据包，NOT_RUN。NotApplicable：新 API 历史同构基线。AcceptedLimitations：历史生成规则、全球社区目录来源及覆盖边界。VerifiedBoundaries：八银行 15–19 位来源规则、来源八位前缀行与 README 描述差异、未知字段保持未知；适配器假设合法单 Kind 数据集，伪造同Id文件的其他Kind行会在投影拒绝。Deferred：官方实时卡表、完整 PAN 识别、全球银行实体归并。

自动审查实际 1 轮，5 项有明确验收条件的集成问题均关闭，另一个非法数据集输入问题归类为 VERIFIED_BOUNDARY，详见[审查记录](bank-resource-review.md)。未变化的其他行业百万条与资源证据复用。No-Progress Check=CHANGED，Next Action=STOP，Goal Status=STOPPED_BLOCKED（仅外部 CI）；不因外部 CI 未运行重复本地测试。
