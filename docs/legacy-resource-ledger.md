# 旧资源追溯台账

本台账记录核心程序集内旧兼容资源的固定输入、许可、转换方式和覆盖变化。转换器只读取仓库内的固定文件，不联网、不执行上游 Python 代码。输入与输出 SHA-256、逐资源计数及排除原因见 [生成清单](../tools/LegacyResourceImport/manifest.json)；运行结果保存在 artifacts/catalog-closure/resources/。

## 已替换资源

| 资源 | 固定来源 / 版本 | 许可 | 旧记录数 → 新记录数 | 转换和覆盖 |
|---|---|---|---:|---|
| FemaleNames.txt | Faker en_US，提交 df289bfde6b6c739bbac696ef432d69c9ec33ac1 | MIT | 1,181 → 381 | 从 AST 提取 female OrderedDict 的名称键，保留源顺序并丢弃权重 |
| MaleNames.txt | Faker en_US，同上 | MIT | 322 → 322 | 从 AST 提取 male 名称键；输出与固定源一致 |
| LastNames.txt | Faker en_US，同上 | MIT | 709 → 1,000 | 从 AST 提取 surname 名称键，保留源顺序并丢弃权重 |
| CommonData.ChineseFirstNames（历史内联列表，现由 ChineseLastNames.txt 提供） | Faker zh_CN，同一提交 df289bfde6b6c739bbac696ef432d69c9ec33ac1 | MIT | 184（182 个独立姓氏）→ 399 | AST 读取 400 个 OrderedDict 条目，按声明顺序去掉 1 个重复键；属性名为历史兼容 API，内容是姓氏，不宣称人口排名 |
| CountryNames.txt | Unicode CLDR 49.0.0-import-v2 | Unicode-3.0 | 240 → 257 | 328 条 region 中保留两字母、有英文名的国家/地区；排除 56 个非两字母区域、7 个聚合/伪造/未知代码和 8 个缺名记录 |
| CityNames.txt | GeoNames cities15000，modified 2026-10-24 | CC BY 4.0 | 1,809 → 34,180 | 每条城市记录取 asciiName；本快照有 32,161 个不同名称，重复名称保留为不同地点样本，0 条需要名称回退 |
| ChineseRegionList.txt | areacodes 2025-12-31，固定提交 2da87e7648f0da7e10dea39e8a613107ed3fe273 | CC0 1.0 | 7 → 34 | 从七个宏观区域标签改为快照中的省级名称 |
| ChineseCityNames.txt | 同一中国区划快照 | CC0 1.0 | 339 → 337 | 333 个地级名称及四个直辖市名称；不把行政占位名称补作城市 |
| ProvinceCity.txt | 同一中国区划快照 | CC0 1.0 | 345 → 2,845 | 每条县级记录按真实父级串接省、地、县名称；直辖市和省直辖县级单位保留源中实际路径，不合成缺失节点 |
| ChineseAreaCode.json | 同一中国区划快照 | CC0 1.0 | 35 → 34 | 省级六位代码前两位映射省份简称；这是身份证行政区前缀，不是电话区号 |
| Jobs.en.json | 项目原创 CareerCatalog，project-samples-2026-10 | MIT | 18 → 39 | 从 39 组中英文职业画像投影英文职位；保留跨画像的重复职位，独立值 38 |
| Jobs.zh-cn.json | 同一项目原创职业画像 | MIT | 302 → 39 | 投影中文职位，39 个均不重复 |
| Colors.en.json | W3C CSS Color 4 命名色，核对日期 2026-10-25 | W3C Software and Document License 2023 | 15 → 148 | 取现有命名色表中的全部 CSS 名称 |
| Colors.zh-cn.json | 同一 CSS 命名色表 | W3C Software and Document License 2023 | 15 → 148 | 15 个现有中文标签按颜色名映射，其余 133 个保留 CSS 英文标识作回退 |
| Emails.json | IANA 文档示例域名，RFC 2606 | 不适用 | 22 → 3 | 只保留 example.com、example.net、example.org 主机名；邮箱生成器直接使用这些保留域名，不拼接未保留子域 |
| SimplifiedChinese.txt | 项目独立编写，project-samples-2026-10 | MIT | 2,499 → 708 | 从项目维护的字符样本按原序去重，只保留基本区 CJK 字符；没有复用来源未明的旧字符集 |

## 金融与区划底表

| 资源 | 固定来源 / 版本 | 许可 | 记录数 / 转换 | 状态 |
|---|---|---|---|---|
| IBAN.txt | npm iban 0.0.14；归档 SHA-256 见工具 manifest | MIT | 102 条上游规范保留 54 个旧接口覆盖国家，排除其余 48 条；输出 54 条 | 已替换；长度、模式和 MOD97 转换检查见 artifacts/catalog-closure/resources/iban-conversion.json |
| BBAN.txt | 同一 npm iban 0.0.14 和 CLDR 49 名称投影 | MIT | 输出 54 条，与 IBAN 国家集合对应 | 已替换；许可随核心 NuGet 包发布 |
| Banks.json | npm bankcardinfo@2.0.4；提交 f0393148c98247348b515ece601939de51f12b23 | ISC（原包元信息声明；补充标准条款） | 8 家银行，832 → 845 条；855 展开减 10 个完整重复；共有 778、新增 67、移除 54 | 完成可追溯替换；三位 Code 为 bing-legacy；历史规则不代表实时分配目录 |
| ChinaDivisions2025.json | areacodes 固定提交 2da87e7648f0da7e10dea39e8a613107ed3fe273 | CC0 1.0 | 原始快照 3,212 条，县级及以上；原样保留实际父级和层级 | 已有固定底表；本轮四项中国兼容资源由此派生 |

Faker、Unicode、GeoNames、areacodes、W3C、iban 和 bankcardinfo 许可/署名文件均通过 Bing.MockData 核心包的打包项发布。GeoNames 的城市目录仍受 cities15000 的人口与特征类别筛选限制；中国资源不增加源文件没有的区划关系。银行替换没有证明旧表的全部上游链条；该历史问题与新快照的明确来源分别记录。

银行专用静态转换：`python tools/ImportLegacyBanks.py`；fixture：`python -m unittest discover -s tools -p test_import_legacy_banks.py -v`。固定输入、来源映射及完整差异分别见 tools/data/bankcardinfo-2.0.4/ 和 artifacts/bank-resource-closure/banks-conversion.json；每套资产的 845×20 条矩阵在最终交付报告中登记。

## 复核

资源转换 fixture：

```powershell
python -m unittest discover -s tools/LegacyResourceImport -v
```

从固定仓库输入重新生成兼容资源和证据：

```powershell
python tools/LegacyResourceImport/import_resources.py
```

当前验证通过 10 个 Python fixture。实际转换结果、输入版本、条数、排除原因和输出校验值在 artifacts/catalog-closure/resources/manifest.json 与 verification.json。该资源工具的核查独立于 .NET；双资产资源消费者与完整 Release 已由集成门禁验证，当前证据入口见[交付报告](catalog-closure-report.md)。
