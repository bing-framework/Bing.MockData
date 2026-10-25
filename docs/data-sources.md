# 内置数据来源

## 中国行政区划

截止日期：2025-12-31（2025 年末县级及以上快照），运行时完全离线，不自动更新。

`ChinaDivisions2025.json` 基于 [yescallop/areacodes 固定提交中的 `data/2025.txt`](https://github.com/yescallop/areacodes/blob/2da87e7648f0da7e10dea39e8a613107ed3fe273/data/2025.txt)，共 3212 条；仓库保留源文件和该提交的 [LICENSE](https://github.com/yescallop/areacodes/blob/2da87e7648f0da7e10dea39e8a613107ed3fe273/LICENSE) 于 `tools/data/yescallop-areacodes-2da87e7/`。许可证为 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。

数据以六位字符串代码保存，避免丢失前导零。省级代码以 `0000` 结尾，地级代码以 `00` 结尾；县级记录的父级优先取源数据中存在的地级代码，否则直接关联对应省级代码。该规则保留直辖市和省直辖县级单位的真实父级，不合成缺失的中间层。源数据仅列有省级记录的台湾、香港和澳门维持原样，不补充下级区划。

可离线运行 `pwsh ./tools/ImportChinaDivisions.ps1` 从已固定的文本快照重新生成结构化资源。脚本不访问网络，并在写出前检查行数、重复代码、父级引用、层级和循环关系。


## 名字与业务文本（本轮）

原男性名表与姓氏表误重合，现替换为 Faker `en_US` 男性名表 322 个名称，固定提交 `df289bfde6b6c739bbac696ef432d69c9ec33ac1`，来源及 MIT 许可保存在 `tools/data/faker-names/`。原中文职业表从 309 项去空白和去重为 302 项；未给历史职业表新增未经证实的来源声明。

本轮中英文业务词语、句式、企业样本、品牌样本、商品兼容规格及客户端样本为项目编写的模拟样本，不作为真实组织或全球完整目录。其他旧内置词库来自任务开始前的仓库，其上游历史来源尚未完整追溯；此限制不改变或覆盖旧交付记录。

## 行业目录

完整数据清单见 [目录来源与覆盖](catalog-data-manifest.md)。原始输入保存在 `tools/CatalogImport/raw/` 和 `tools/IndustryImport/raw/`。代码包与离线数据包独立打包，运行时不自动联网。

## 本轮样本与历史追溯

新增职业、行业、部门、商品兼容样本及客户端资料为项目原创模拟样本。命名色的名称与 RGB 数值对照 [W3C CSS Color 4 的命名色表](https://www.w3.org/TR/2026/CRD-css-color-4-20261022/#named-colors)，核对日期 2026-10-25；本库的色系分组是按 HSL 编写的模拟选择规则，不是标准定义的分类。没有引入运行时下载或新的数据依赖包。

本地历史使用 `git log --follow --reverse` 核对。以下仅表示可以追溯到的仓库入口，不能据此推定第三方上游与许可已确认：

| 资源 | 最早可见提交 / 日期 | 上游状态 |
|---|---|---|
| BBAN、IBAN、CityNames、CountryNames、FemaleNames、LastNames | acbe0e7 / 2018-12-13 | 上游尚未确认 |
| MaleNames | acbe0e7 / 2018-12-13 | 当前已替换为上述固定 Faker 来源 |
| SimplifiedChinese | 0834f85 / 2018-12-14 | 上游尚未确认 |
| ChineseAreaCode、ChineseCityNames、ChineseRegionList、ProvinceCity | c9153fd / 2018-12-21 | 历史来源尚未确认；现代结构化中国地址使用已记录来源的 2025 区划 |
| Banks | 5ab9075 / 2018-12-21 | 上游尚未确认 |
| Colors.en/zh-cn、Emails、Jobs.en/zh-cn | c5a1cba / 2018-12-21 | 当前路径入口可追溯，上游尚未确认 |

这些历史资源继续明确标为来源追溯未完成，不因新样本完成而改为已核实；本轮没有猜测其来源或伪造许可。
