# 历史金融资源追溯

核对日期：2026-10-27。

## 当前状态：银行资源已完成显式替换

以下溯源过程保留为历史记录。当前 Banks.json 使用 bankcardinfo@2.0.4 的固定归档，提交 f0393148c98247348b515ece601939de51f12b23，包元信息声明 ISC，作者 navyxie。原发布物没有独立 LICENSE，核心包保留原声明、署名、补充标准许可文本和转换说明。三位银行代码显式定义为 bing-legacy，未将其冒充官方代码。

八银行的 855 条展开规则去掉 10 个完整重复键，输出 845 条；旧表与新表共有 778 条、新增 67 条、移除 54 条，其中招商银行 39 组类型变化采用新来源，15 条旧长度规则不带入。两套资产各完成 845×20 条固定矩阵。当前 N03 银行项按“完成可追溯替换”收口；这不代表恢复了旧表完整上游链条。计划、差异、全球 BIN 独立目录与最终交付验收见[银行实施计划](bank-resource-plan.md)和[银行示例](bank-resource-examples.md)。

## IBAN / BBAN：已替换

旧表首次进入本仓库的提交为 `acbe0e7`，其第三方上游无法据此确认。本轮使用 [npm 官方发布 iban@0.0.14](https://www.npmjs.com/package/iban/v/0.0.14) 的固定归档替换，不将旧表反推为该来源。原 GitHub `arhs/iban.js` 地址当前返回 404；实际取证对象是发布归档 `https://registry.npmjs.org/iban/-/iban-0.0.14.tgz`。

原始归档、源码、包元信息、README、MIT LICENSE 和 SHA-256 保存在 `tools/data/iban-js-0.0.14/`；MIT 署名为 ARHS Developments SA, 2013–2017。`tools/ImportLegacyIban.py` 只读取声明数据，不执行 JavaScript；102 条国家规则中保留原接口全部 54 国，另外 48 条明确排除为超出旧覆盖。国家英文名称使用固定 CLDR 49.0.0。转换为大写 ASCII 电子形式，IBAN 继续计算 MOD 97，BBAN 不冒充满足各国额外银行校验或实际账户分配。

转换证据 `artifacts/catalog-closure/resources/iban-conversion.json` 校验每个保留国家的源示例长度、字符模式和 MOD 97。源码原有许可随核心 NuGet 包分发。不增加 JavaScript 运行时或核心 SQLite 依赖。

## Banks.json：BLOCKED_EXTERNAL

该资源首次可追溯至本仓库 `5ab9075`；目前无法确认其上游数据权利声明。银行代码、名称、卡前缀、后缀长度和类型构成原接口关联，不能用通用卡组织前缀或任意 Luhn 号码替换。

本轮检查了仓库历史、公开 GitHub 精确资源名称，以及已固定 [Faker bank provider](https://github.com/joke2k/faker/blob/df289bfde6b6c739bbac696ef432d69c9ec33ac1/faker/providers/bank/__init__.py)。后者提供模拟账户格式与部分银行字段，不能为现有中国银行代码和全部卡前缀建立等价来源。未复制商业银行目录或未经核实的数据集。

保留原资源及明确阻塞，不删除、不伪造银行映射，也不宣称当前映射是实时分配目录。关闭条件为取得该快照可再分发的来源证据，或引入可核实且保留所需关联语义的替代目录。此项使 N03 仍为部分完成；它不触发无意义的代码修复或重复测试。

### 后续固定候选核验（2026-10-27）

本轮按银行、前缀、总长度和卡类型比较原有 8 家银行的 832 条规则，记录在 `artifacts/catalog-closure-followup/bank-source-review.json`。

- [hexindai/bcbc 固定提交](https://github.com/hexindai/bcbc/tree/de631827ffe8db2792d140f3476b02498fc1244f)：CSV 包含前缀、银行简称、类型和总长度，仓库为 MIT；新增记录脚本调用 Alipay 校验接口。所查材料未证明历史 BIN 数据的完整来源及适用再分发条件。八家银行中有 446 条候选规则，与原表重合 318 条。
- [caijf/bankcard 固定提交](https://github.com/caijf/bankcard/tree/79af3e5d1bd381537c2f0e95c03584c911963d1e)：相似字段、MIT 仓库许可证，八家银行中有 423 条候选规则，与原表重合 318 条；所查材料未明确 BIN 数据上游。
- [voocx/bank-card 固定提交](https://github.com/voocx/bank-card/tree/6713de8f868c5e8d5c32fa6b2c82c5e67f44f6c5)：所查版本提供银行名称表，没有可替代现有前缀、长度和类型的规则集。

这些覆盖差异可以在说明转换、计数及影响后保留，并不要求新目录复刻全部旧数据。当前没有采用替代的原因是数据来源及适用条件、旧数字银行代码的命名空间仍未核实，不代表这些仓库数据不可用或违法。不同官方/支付接口采用不同银行代码体系，不能只因名称相同就声称旧 Code 与所查标准完全一致。保持 BLOCKED_EXTERNAL，等待足够来源证据或合格替代。

### 再次溯源结果（2026-10-27）

新增调查见 [银行资源补充溯源](bank-provenance-search.md)。固定 `bankcardinfo@2.0.4` 的规则与现有 832 条精确重合 778 条，五家银行的顺序也一致；包元信息明确声明 ISC，属于优先替换候选，但尚不能证明它是本项目直接上游。另找到明确 CC BY 4.0 的全球 BIN 合并目录及银联官方卡表渠道，并分别记录字段缺失和分发条件的取证情况。

旧数字银行码可以明确保留为本库兼容标识；未核实官方命名空间不等于必须阻止新目录采用。当前尚未执行替换，N03 状态不变，但后续可通过固定来源的显式替换收口，不要求恢复旧表全部历史链条。
