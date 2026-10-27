# 离线目录数据清单

本文件描述 `Bing.MockData.Catalogs` 数据包中的真实离线快照。运行时不会联网，也不会根据文件名推断数据覆盖范围。每个 SQLite 文件在 `CatalogInfo` 保存数据集 ID、版本、来源、许可、快照日期、记录数、来源 SHA-256 和覆盖说明；逐源输入文件及输出数据库 SHA-256 保存在 `src/Bing.MockData.Catalogs/Data/catalogs.manifest.json`。许可正文和来源说明随离线数据包保留在 `notices/`。

| 数据集 / 文件 | 固定版本或快照 | 许可 | 记录数与 Kind | 覆盖边界 |
|---|---|---|---:|---|
| `mime-db.sqlite3` | mime-db 1.54.0，2025-03-18 | MIT | 1,291 `mime-type` | MIME 类型及扩展名别名；非文件样本或安全性判断 |
| `cldr.sqlite3` | Unicode CLDR 49.0.0，导入版 49.0.0-import-v2，2026-10-21 | Unicode-3.0 | 1,141：328 `region`、307 `currency`、506 `region-currency` | 地区、当前及历史币种与关系；缺失本地化名称保留代码并显式标记；历史关系不是当前唯一币种 |
| `pci-ids.sqlite3` | PCI IDs 提交 `fe7fceb2e1153d495e1deefacf59c5d61c36ef8e`，2026-10-24 | BSD-3-Clause（上游多许可中选用 BSD） | 42,549：2,522 `vendor`、21,693 `device`、18,110 `subsystem`、22 `class`、118 `subclass`、84 `interface` | PCI 设备目录，不是消费电子产品全集；46 条子系统 subvendor 引用的 6 个 ID 在原始快照中无 vendor 定义 |
| `ourairports.sqlite3` | OurAirports 提交 `711cf6754ef05a1ccedc2482cc1e838032249d3c`，2026-10-25 | Public Domain | 180,283：86,281 `airport`、249 `country`、3,987 `region`、48,370 `runway`、30,388 `frequency`、11,008 `navaid` | 机场及相关航空设施；本文件不含航空公司或真实航线 |
| `libphonenumber.sqlite3` | libphonenumber 提交 `6d349ac62b76a86eccba0ac1d2fc83ea2b00a841`，2026-10-23 | Apache-2.0 | 492 `phone-pattern` | 保留源正则、移除 XML 排版空白的匹配正则和号码格式规则；不代表号码已分配 |
| `geonames-cities.sqlite3` | GeoNames `cities15000.zip`，2026-10-24 | CC BY 4.0 | 34,180 `city` | 全球人口门槛城市快照，不含所有聚落 |
| `geonames-postal-0.sqlite3` | GeoNames `allCountries.zip`，2026-10-24，源行偶数分片 | CC BY 4.0 | 913,482 `postal` | 全球邮编源记录分片；保留来源粒度及精度 |
| `geonames-postal-1.sqlite3` | 同上，源行奇数分片 | CC BY 4.0 | 913,483 `postal` | 与分片 0 合并使用才覆盖完整导入记录 |
| `bank-bins.sqlite3` | Techbuddie-Solutions/binlist-data 提交 `fcc907c81cf02b12e396b0c12a8c16472b47619a`，导入标识 import-v1，2026-09-03 | CC BY 4.0 | 458,051 `bank-bin`，CN 18,389 | 原始 BIN、品牌、类型、分类、机构及联系信息、国家、source/seen；未知保持 null；5/6/8 位分别 7/454,496/3,548 条；不含完整卡号长度，不承诺八位 BIN 分配已核验或覆盖完整 |

上游入口：[mime-db](https://github.com/jshttp/mime-db)、[CLDR JSON](https://github.com/unicode-org/cldr-json/tree/49.0.0)、[PCI IDs](https://github.com/pciutils/pciids/tree/fe7fceb2e1153d495e1deefacf59c5d61c36ef8e)、[OurAirports 数据](https://github.com/davidmegginson/ourairports-data/tree/711cf6754ef05a1ccedc2482cc1e838032249d3c)、[Google libphonenumber](https://github.com/google/libphonenumber/tree/6d349ac62b76a86eccba0ac1d2fc83ea2b00a841)、[GeoNames 城市快照](https://download.geonames.org/export/dump/cities15000.zip)、[GeoNames 邮编快照](https://download.geonames.org/export/zip/allCountries.zip)。GeoNames 来源说明和 CC BY 4.0 文本随数据保存在 `notices/geonames-*-readme.txt`。

## 完整性与校验

| 文件 | 来源 SHA-256 | SQLite SHA-256 | 字节数 |
|---|---|---|---:|
| `mime-db.sqlite3` | `5c3f4b798b81976b1496ec0d81b600d5c366e47bba0727c13ba7143ea1989a77` | `ef4fcdd78043d059ad8c1ad8744122f92ed8bb801676f2f2a54f3c3157befe2c` | 483,328 |
| `cldr.sqlite3` | `d9a14c5bd1d463ac0a713e37b87411dd3708bc57a2966310bbfbfed000922b1e` | `7c256d778bf5ca29eb784425d73d021a879ed902db986f6678fe4dceba9175b4` | 356,352 |
| `pci-ids.sqlite3` | `c536d82778aaafb7344204239a806fb89d13138467aae4c3d22125fe02d482a0` | `67abc2b82c9d0b95ca4cc33d5123614d0001384157889d5f0acd7cb6121818eb` | 12,759,040 |
| `ourairports.sqlite3` | `ae78ac51c8de9b2897d90bd1e75579f192ec2f0fe41c2223098e26d5b9fafcf9` | `80db10ddcc70f2c8fca11e596d68507445d9aba51ddce6b65827329b44cf9097` | 95,903,744 |
| `libphonenumber.sqlite3` | `1274b3356dc726e8531a00c8d188e5b8f5c6aa2d7d4c56f459e74ba81ae8f18d` | `e68cf394a9289f82bd33613e5b502f3588769075b906654f99ac03dc96fce3a0` | 692,224 |
| `geonames-cities.sqlite3` | `4c921f81f2cf4cc65cd00e2d7c03a6f6f91da4871044e3640dcea886451f45c7` | `373c58841bbe87c742341402d77aee6e8809243b95e9b532fef7b5a40439e7` | 24,887,296 |
| `geonames-postal-0.sqlite3` | `7aff9e9cf85d16a1ee978e55aeab38c59ed79cb58cf299998fd76dc844042c68` | `2f67b0234162a3fffe53e56b2fcfcfce0bf438e69569e88a4415d50991c7c4f3` | 446,251,008 |
| `geonames-postal-1.sqlite3` | `7aff9e9cf85d16a1ee978e55aeab38c59ed79cb58cf299998fd76dc844042c68` | `a41de1ca23080b616102b6bc1a44fe301aba41a9ec7c87f15593645a8e2b5c4e` | 446,197,760 |
| `bank-bins.sqlite3` | `4db6fd3e0fd0a9baa2973776bfa6559ce584952e287fa5d1a16414445b891126` | `646a7ec092b0b019d17ed5723c64b0c2ece648231d595cf0fa901c6d8ebe5d86` | 172,437,504 |

导入工具保留每个下载的原始文件，并在写目录时验证稳定键唯一性、输出记录数和数据库结构。邮编原始 ZIP 约 19.7 MB，但 SQLite 单库超过 512 MiB 默认目录上限；因此按来源行序号奇偶分为两个确定性文件，记录数之和为 1,826,965。两片查询按过滤后的行数加权选择，不会只抽到单个分片。各 SHA-256、版本、源文件校验值、输入字节数、Kind 计数及排除数量以 JSON manifest 为权威证据。

## 目录装载与所有权

`Catalogs` 是代码包，离线数据库和许可通知单独打包。运行 `pwsh ./build/pack-domains.ps1` 后，在生成目录中解压 `catalog-data.zip`，然后将目录传给 `UseCatalogFile` 或相应目录适配器。路径挂载会为每个 `MockContext` 单独打开 SQLite 只读连接，并由该上下文释放；调用方传入的 `IBingMockCatalog` 默认是借用资源，只有显式 `ownsResource: true` 或适配器的 `ownsCatalog(s): true` 才转移所有权。邮编分片挂载名固定为 `geonames-postal-0`、`geonames-postal-1`，均应传给 `UseGeographicCatalogs`。

默认单上下文最多挂载 8 个目录文件，每文件不超过 512 MiB；邮编分片各自满足默认大小上限。数据文件不自动更新，升级快照时应保留源、版本和许可信息，重新运行导入及校验后使用新的上下文。

## 导入与重建

`tools/CatalogImport` 是显式网络导入工具，正常库运行不会调用它。使用 `dotnet run --project tools/CatalogImport -- all-basic --output <目录>` 下载并转换上述基础快照。GeoNames 命令可单独运行 `geonames-city` 或 `geonames-postal`。原始输入按数据源及版本保存在 `tools/CatalogImport/raw/`；生成物可写到独立 staging 目录核对后再更新 `src/Bing.MockData.Catalogs/Data/`。重新生成 manifest 后，必须审阅上游许可和覆盖说明；不要将测试样本或部分快照描述为完整目录。

行业数据单独列于 `src/Bing.MockData.Catalogs/Data/industry.manifest.json`，由 `tools/IndustryImport` 管理；本清单仅负责上述基础五类目录和 GeoNames。行业目录及其覆盖边界见 [全领域示例](domain-examples.md)。

全球 BIN 使用独立命令 `dotnet run --project tools/CatalogImport -- bank-bins`，固定输入来自 [Techbuddie 来源提交](https://github.com/Techbuddie-Solutions/binlist-data/tree/fcc907c81cf02b12e396b0c12a8c16472b47619a)，保留 README、CC BY 4.0 正文、合并脚本与上游冲突记录。署名包括 Techbuddie Solutions、Ian Nuttall 和 Venelin Kochev。读取时不发起网络请求；库不把 `seen` 解释为银行核验时间，不把机构名称模糊匹配为旧八银行关联。具体恢复及接口见[银行示例](bank-resource-examples.md)，导入验证与当前门禁见[银行交付报告](bank-resource-report.md)。


## 行业快照（完整本地交付）

|文件/数据集|版本|记录数|来源与覆盖|
|---|---|---:|---|
|vehicles|v2026.10.4|55,609|VehiclesDB，934 厂商及 54,675 车型/国家证据行；CC BY 4.0 与上游署名|
|wikidata-airlines|2026-10-25|6,284|Wikidata 航空公司及子类，包含历史记录；CC0；排除 91 个重复键，不代表真实航线|
|ror|v2.15-2026-10-20|146,716|ROR 完整机构快照，保留原名和 78,918 条机构关系；CC0；非所有中小学目录|
|crossref-books|2026-10-25|100,000|Crossref 图书游标采集；仅事实书目，无摘要和全文；CC0；排除 7 条缺 DOI/标题、993 条超过采集上限|
|icd10cm|FY2027|98,403|CDC/NCHS 美国诊断分类，合并表格与完整有序代码清单，包含第七字符扩展；保留父级和 billable 标识|
|rxnorm-cpc|2026-10-05|329,717|82,689 个概念代表词及 247,028 个来源原子词；NLM CPC 公开子集|
|rxnorm-cpc-relations-1…7|2026-10-05|2,589,722|完整 RXNREL 来源关系，按 400,000 行拆片，末片 189,722 行；按 STYPE 区分 RXAUI/RXCUI 引用|

行业原始文件、下载地址、许可说明、输入 SHA256、转换后 SHA256、排除原因和每种记录数量见 `Data/industry.manifest.json` 及各 `*.manifest.json`。原始输入在 `tools/IndustryImport/raw/`；运行 `python tools/IndustryImport/import.py <vehicles|ror|icd|rxnorm|books|airlines>` 可从保存输入重建。图书原始页含被上限排除的事实书目，清单明确记录排除数量。

所有 RxNorm 关系端点均可在主文件中按概念或原子词查询；它们是来源药品术语关系，不能解释成诊断与药品的适应关系。七个关系分片加主文件共八个文件，符合默认单上下文目录数量上限。无需关系查询的业务场景只需挂载主文件。

ICD 输入来自 `icd10cm-table-and-index-2027.zip` 和 `icd10cm-code-descriptions-2027.zip`；保留美国政府公开编码数据及来源通知，不包含其他国家修订或 ICD-10-PCS。所有行业转换均无缺失键的静默替代；输出名称保留来源文本，不自动翻译。

## 本次关联复核（2026-10-25）

CLDR 导入 v2 从相同 pinned raw 补齐历史区域及币种端点，未联网更新来源，地区/币种外键缺失均为 0；OurAirports 国家/地区引用缺失为 0。PCI 的设备、设备父级、分类层级一致，但 46 条 subsystem.subvendor 引用缺少 vendor 定义；与原始 pci.ids 逐项一致，属于 VERIFIED_SOURCE_BOUNDARY，保留原始记录和缺失计数，没有制造厂商。严格关系门禁仍为 FAIL。当前 21 个 SQLite 共 5,413,352 条记录。详情及证据见 [本轮交付报告](domain-gap-closure-report.md)。
