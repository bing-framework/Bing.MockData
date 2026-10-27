# 内置数据来源

CSS 命名色投影适用 [W3C Software and Document License 2023](https://www.w3.org/copyright/software-license-2023/)，原文、版权、快照状态和修改说明随核心包的 `licenses/w3c-color4.txt` 提供。

## 中国行政区划

截止日期：2025-12-31（2025 年末县级及以上快照），运行时完全离线，不自动更新。

`ChinaDivisions2025.json` 基于 [yescallop/areacodes 固定提交中的 `data/2025.txt`](https://github.com/yescallop/areacodes/blob/2da87e7648f0da7e10dea39e8a613107ed3fe273/data/2025.txt)，共 3212 条；仓库保留源文件和该提交的 [LICENSE](https://github.com/yescallop/areacodes/blob/2da87e7648f0da7e10dea39e8a613107ed3fe273/LICENSE) 于 `tools/data/yescallop-areacodes-2da87e7/`。许可证为 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。

数据以六位字符串代码保存，避免丢失前导零。省级代码以 `0000` 结尾，地级代码以 `00` 结尾；县级记录的父级优先取源数据中存在的地级代码，否则直接关联对应省级代码。该规则保留直辖市和省直辖县级单位的真实父级，不合成缺失的中间层。源数据仅列有省级记录的台湾、香港和澳门维持原样，不补充下级区划。

可离线运行 `pwsh ./tools/ImportChinaDivisions.ps1` 从已固定的文本快照重新生成结构化资源。脚本不访问网络，并在写出前检查行数、重复代码、父级引用、层级和循环关系。


## 名字与业务文本

姓名表取自固定 Faker en_US provider 提交 df289bfde6b6c739bbac696ef432d69c9ec33ac1。导入器用 Python AST 读取 first_names_female、first_names_male 和 last_names 的字符串键，不执行上游代码；权重不用于旧版列表消费者。三个表分别输出 381、322 和 1,000 个名称，固定源、许可和资源哈希见 tools/data/faker-names/manifest.json。中文姓氏来自同一固定提交的 zh_CN provider：从 400 个 OrderedDict 条目按声明顺序保留 399 个唯一姓氏；`CommonData.ChineseFirstNames` 是历史 API 名称，实际返回该姓氏资源，不表示人口排名。源与转换证据见 tools/data/faker-zh-names/manifest.json 和 artifacts/catalog-closure/resources/chinese-surnames.json。Faker MIT 许可随核心 NuGet 包发布。

职业字符串由项目原创 CareerCatalog 的中英文岗位字段投影，每种语言 39 项；相同岗位在不同业务画像中的重复项保留。命名色依照已核对的 [W3C CSS Color 4 命名色表](https://www.w3.org/TR/2026/CRD-css-color-4-20261022/#named-colors)，资源列出 148 个 CSS 名称。中文资源保留 15 个现有项目译名，其余 133 项使用稳定英文 CSS 名称作为回退；HSL 色系分组仍是项目规则，不是 CSS 标准分类。邮箱仅使用 example.com、example.net、example.org 文档示例域名。

SimplifiedChinese.txt 是项目独立编写的模拟字符池，转换后含 708 个去重的基本区 CJK 字符，不含空白、标点或外部词库内容。它供随机字符生成使用，不是词典或完整汉字目录。

## 目录投影与覆盖

CountryNames.txt 现由 CLDR 49.0.0 区域目录投影，保留 257 个有英文名称的两字母国家/地区代码；排除 56 个非两字母区域、7 个聚合/伪造/未知区域代码和 8 个缺少英文名称的记录。此列表覆盖国家与地区，不等同主权国家清单。CLDR Unicode 许可随核心 NuGet 包发布。

CityNames.txt 现由固定 GeoNames cities15000 快照投影 34,180 条城市记录的 asciiName；此全球样本受其人口和特征类别门槛限制，不表示所有聚落。坐标及实际行政关联仍以独立 GeoNames 目录为准，旧字符串列表不携带城市关系。GeoNames 的署名说明随核心 NuGet 包发布。

2025 年末中国区划快照继续保留完整 3,212 条及实际父级。兼容资源投影为：ChineseRegionList.txt 从 7 个宏观区域名改为 34 个省级名称；ChineseCityNames.txt 为 333 个地级名称加四个直辖市名称；ProvinceCity.txt 为 2,845 条县级记录沿实际父级拼接的省级到县级路径；ChineseAreaCode.json 为 34 个省级代码前两位到省份简称的映射。最后一项是中国身份证行政区前缀，不是电话区号。投影不补造源中缺失的层级；更细地址仍为模拟文本。

## 行业目录

完整数据清单见 [目录来源与覆盖](catalog-data-manifest.md)。原始输入保存在 tools/CatalogImport/raw/ 和 tools/IndustryImport/raw/。代码包与离线数据包独立打包，运行时不自动联网。旧兼容资源的逐项来源、版本、许可、转换与覆盖差异见 [旧资源追溯台账](legacy-resource-ledger.md)。

## 金融资源状态

IBAN.txt 和 BBAN.txt 现由固定 npm iban@0.0.14（MIT）生成：从 102 条规范记录中保留旧接口已有的 54 个国家，48 条超出旧覆盖的记录排除；名称取固定 CLDR 49 快照，IBAN/BBAN 长度、模式与 MOD97 均在转换证据中核对。来源、排除清单与资源哈希见 tools/data/iban-js-0.0.14/manifest.json 和 artifacts/catalog-closure/resources/iban-conversion.json。该许可随核心 NuGet 包发布。

Banks.json 已由固定 npm bankcardinfo@2.0.4（提交 f0393148c98247348b515ece601939de51f12b23）显式替换。包元信息声明 ISC；原归档没有独立 LICENSE，随核心包提供作者署名、原声明、补充标准 ISC 条款和转换说明。静态转换从八家银行的 855 条展开规则中去除 10 个完整重复键，保留 845 条；旧三位代码属于 bing-legacy 兼容命名空间。新旧共有 778 条、新增 67 条、移除 54 条，包含招商银行 39 组类型变化和 15 条旧长度规则移除。这是来源替换，不是对旧数据正确性的裁决。

来源、完整差异、输入校验与覆盖边界见 tools/data/bankcardinfo-2.0.4/source.json、artifacts/bank-resource-closure/banks-conversion.json 和[银行迁移示例](bank-resource-examples.md)。历史前缀规则仅用于模拟生成；全球 BIN 查询使用独立 Techbuddie CC BY 4.0 离线目录，二者不做未经证明的机构映射。其他资源见[旧资源追溯台账](legacy-resource-ledger.md)。
