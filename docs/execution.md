# 修复执行记录

Task: mockdata-repair-22 · Round: 2（范围算法复核与构建收口）

BaselineCommit: `50bac60d902db3f2d4e88c76b647df6e7ab1266a`
Candidate: 以上基线上的当前未提交工作区；库版本未变，未提交、推送或发布。

## 已批准计划

1. 优先修复凭据输出、依赖漏洞、缓存泄漏与随机核心。
2. 统一配置快照、闭区间、种子、并发及选项契约，再修复地址、文本、日期、IBAN、资源和参数校验。
3. 为原有 24 个示例测试加断言，新增边界、生命周期、并发和格式回归。
4. 升级测试工具链，固定 SDK/依赖，隔离输出，增加失败中止的打包流程、真实包消费与发布替身测试。
5. 完成前后性能对照、迁移说明、逐项复核与最终本地门禁。远程 CI 和实际密钥轮换单独记录。

具体契约见 [迁移说明](migration.md)。库保持 netstandard2.0，测试迁至 net10.0；破坏性 API 调整已获批准。

## 当前进度

- Implementation: 22/22（100%）；OPEN_ACTIONABLE = 0。
- Release Gates: 4/5（完整测试、依赖审计、包消费、脚本替身通过；远程 CI 待运行）。
- IMPLEMENTATION_STATUS: PASS
- TEST_STATUS: PASS（Debug 43/43，Release 43/43）
- PERFORMANCE_EVIDENCE_STATUS: PASS（固定工作量前后对照）
- RESOURCE_EVIDENCE_STATUS: PASS（弱引用生命周期测试、文本分配量对照）
- EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL
- RELEASE_STATUS: BLOCKED
- GOAL_STATUS: STOPPED_BLOCKED（仅外部 CI 证据未取得）

## 原报告逐项状态

| # | 问题 | 当前实现 / 验证 | 状态 |
|---|---|---|---|
| 1 | 发布脚本打印密钥 | 环境变量取值，默认不发布；替身确认正常和失败路径不输出密钥 | CLOSED |
| 2 | Newtonsoft.Json 漏洞 | 升至 13.0.4；JSON 明确深度；直接/传递依赖审计无已知漏洞 | CLOSED |
| 3 | 工厂哈希碰撞 | 删除实例缓存与哈希键；冲突配置各自正确输出 | CLOSED |
| 4 | 工厂缓存无法释放 | 工厂每次新建；生成器和配置弱引用可回收 | CLOSED |
| 5 | 文本保留全部历史 | 删除历史集合；活着的生成器不保留已输出字符串 | CLOSED |
| 6 | 静态 Random 并发竞争 | 实例随机源、完整操作加锁；32 类配置并发集合等于顺序结果 | CLOSED |
| 7 | 专用数值配置工厂失败 | 显式映射；泛型和专用配置覆盖；未知类型明确拒绝 | CLOSED |
| 8 | 数字/tick 溢出及精度损失 | 整数位宽抽样，浮点有限插值；极值和闭区间测试 | CLOSED |
| 9 | IPv4 固定段异常 | 完整数值闭区间；固定地址、子网及跨边界覆盖 | CLOSED |
| 10 | IPv6/MAC 格式解析错误 | IPv6 标准解析、MAC 十六进制解析；格式/范围校验 | CLOSED |
| 11 | 仅空格字符集崩溃 | 统一字符集合；仅空格/空文本/空字符集测试 | CLOSED |
| 12 | 文本长度范围不完整 | 使用覆盖完整范围的长度抽样；70000 字符上端点测试 | CLOSED |
| 13 | IBAN 无校验位 | 54 国家独立格式夹具 + MOD 97；纠正 7 国规则问题 | CLOSED |
| 14 | 语言参数被忽略 | 透传语言、主程序集嵌入、明确资源缺失异常；修正错置语言资源 | CLOSED |
| 15 | DateTime 丢失 Kind | 保留一致 Kind，拒绝混用；日期/整秒在原区间内对齐 | CLOSED |
| 16 | 选项无效与可变配置 | 落实开关，删除空壳 API，标量/列表/数组快照与空值测试 | CLOSED |
| 17 | 参数校验不足 | 构造时拒绝非法范围、格式、性别、前缀、类型和国家 | CLOSED |
| 18 | 测试没有断言 | 原 24 项加断言，新增 19 项回归；Debug 43/43 | CLOSED |
| 19 | 测试目标过时 | net10.0 + xunit.v3；本机可完整运行 | CLOSED |
| 20 | 发布缺少测试和失败中止 | test→pack→consume→可选 publish；全部阶段失败中止、历史产物隔离测试通过 | CLOSED |
| 21 | 文本与资源重复开销 | 预分配 char[]、固定资源懒加载；文本耗时/分配量显著下降 | CLOSED |
| 22 | 随机/构建不可重放 | 全生成器 Seed、SDK/依赖锁定、输出隔离；CI 已配置，本地完整包门禁通过 | CLOSED |

## 变更影响分析

- ChangedFiles: Core、Internals、Factories、Datas、资源、测试、构建、README、docs。
- ChangedProjects: Bing.MockData、Bing.MockData.Tests、PerformanceProbe、PackageSmoke。
- ChangedPublicContracts: Seed、配置快照、闭区间、选项删除、IBAN Pattern；详见迁移说明。
- ChangedRuntimePaths: 全部生成器、随机源、资源初始化和 JSON 加载。
- ChangedProviders: 不适用。
- ChangedTFMs: 库 netstandard2.0 不变；测试 netcoreapp2.1 → net10.0。
- ChangedBuildPackaging: 依赖升级、锁文件、输出隔离、发布门禁与 Windows/Linux CI。
- ChangedBenchmarkHarness: 新增固定文本长度/次数的轻量前后对照；同一程序引用前后程序集。
- ChangedDocsOnly: 否。
- AffectedDependents: 删除的空壳配置、实例缓存语义、范围端点和 IBAN 元数据需要迁移。
- RiskLevel: HIGH。

先执行 L0/L1；共享核心与公共 API 收口时执行 L2/L4；打包边界执行 L3。仅受影响的文本热路径执行小型性能探针，不做无关大容量矩阵。

## 验证证据

环境：Windows 10.0.19045，SDK 10.0.400，测试/探针运行时 10.0.11。

| 层级 | 验证 | 结果 |
|---|---|---|
| L0 | restore、生产与测试编译、diff whitespace | 通过 |
| L1 | 首轮 Regression 16 项 | 15 通过；1 项定位到 culture 资源被拆入卫星程序集，已修复 |
| L1 | 资源失败项及后续新增/扩展契约，共 6 项 | 6/6 通过 |
| L2/L4 | Debug 完整 solution | 43/43 通过，0 跳过 |
| L2/L4 | Release 完整 solution（打包门禁） | 43/43 通过，0 跳过 |
| L3 | Release 包与独立缓存/临时项目消费 | 通过；第三方依赖锁定，本轮包来源精确映射与 hash 验证 |
| L3 | 发布脚本替身 | restore/test/pack/consumer restore/run/push 失败即停；缺失密钥中止；不会发布旧包 |
| L0 | CI YAML 解析、Windows/Linux matrix | 通过；未运行远程工作流 |
| Audit | `dotnet list ... package --vulnerable --include-transitive --no-restore` | 无已知漏洞 |
| L5 | 固定文本工作量对照 | 已完成一次 before 和一次 after |

测试并行验证请求最大并行度 4；同实例内部生成最大实际并行度 1，通过锁串行化，比较结果集合而非线程分配顺序；队列数量未测量，不宣称 4 路内部生成。

### 文本性能对照

同一 Windows 主机、.NET 10.0.11、Debug/netstandard2.0 库、同一 harness；每组 3 次热身、3 次测量。固定长度/次数，比较字符数量、耗时和当前线程分配量，不比较随机内容。旧版没有 Seed，因此未构造不存在的固定种子 before；字符集的修复已改变内容语义，但每次字符数量相同。

| 文本长度 × 次数 | before 中位 ms | after 中位 ms | before 分配字节 | after 分配字节 |
|---|---:|---:|---:|---:|
| 8 × 1000 | 0.7525 | 0.1321 | 712608 | 144040 |
| 1024 × 100 | 11.1370 | 0.6504 | 113254200 | 420840 |
| 8192 × 10 | 28.6313 | 0.5098 | 677806176 | 328840 |

这是局部探针的观察结果，不是生产容量承诺；本任务没有设立正式性能/容量阈值。原始 evidence 只保存在工作区被忽略的 artifacts，不加入包。

### Evidence 清单

- `artifacts/review/performance-before.txt`：基线 50bac60 的 Debug 库。
- `artifacts/review/performance-after.txt`：当前文本实现的 Debug 库。
- `artifacts/review/test-results/targeted.trx`：首轮失败诊断，已被后续修复项覆盖，不能视作最终通过。
- `artifacts/review/test-results/targeted-resource-and-contracts.trx`：精准确认 6/6。
- `artifacts/review/test-results/debug-full.trx`：当前 Debug 全量 43/43。
- `artifacts/review/dependency-audit.json`：当前库和测试直接/传递依赖审计。
- `artifacts/review/release-package-gate.txt`：首轮包消费锁闭包缺失诊断；不是最终结果。
- `artifacts/review/release-package-final.txt`：锁定还原、Release 43/43、打包、隔离包消费全部通过。
- `artifacts/review/pack-script-substitute-final.txt`：最终脚本替身确认，包含仓库外目录继承审计配置。
- `artifacts/review/consumer-final.txt`：消费夹具改为实际 JSON 反序列化之后，精准消费验证 exit 0。
- 本轮已验证包：`artifacts/package-runs/3f659a9c0bcc464b86d06a9505d1d126/packages/Bing.MockData.0.0.1-preview-20190114-1.nupkg`。

Round 1 修复并集成 22 项；初次验证发现 culture 资源嵌入和消费锁文件闭包问题，均修复并精准验证。Round 2 独立只读复核了整数、浮点、地址与 tick 算法，未发现新的可执行缺陷；主线补齐仓库外消费目录的审计配置复制，以脚本替身确认，并强化 JSON 包消费验证。生产源、主单测、依赖图和性能热路径未再次改变，因此复用上述全量测试与性能证据，不重复无关检查。

打包仍显示原有许可证 URL 弃用和缺少包 README 的提示，不影响此轮安全/正确性门禁；未修改版本或执行真实 NuGet 发布。

## TODO 分类

- Completed: #1–22 的本地实现与证据，迁移说明与构建流程。
- Open Actionable: 无。
- Blocked Approval: 无。未申请真实发布。
- Blocked External: 远程 Windows/Linux CI；若历史日志确有有效密钥，凭据轮换由持有人在 NuGet 完成。
- Not Applicable: 旧版不存在 Seed 重放，不伪造该功能的 before 对照。
- Accepted Limitations: 同版本/运行时/配置/顺序的重放；线程分配顺序不确定；模拟账号不保证真实存在或额外国内校验；见迁移说明。
- Verified Boundaries: 无容量边界实验。
- Deferred: 无本地修复项。
- Reused Evidence: 基线只冻结一次；文本 source scope 不变，复用性能探针；最终仅脚本/消费夹具/文档变动，复用未改变的生产与主测试证据。
- No-Progress Check: CHANGED。
- Next Action: STOP。推送后的远程 CI 属于外部门禁，不触发新一轮自动修复。
- Goal Status: STOPPED_BLOCKED。
