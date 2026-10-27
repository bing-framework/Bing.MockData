# PCI 原始与关系闭合快照

PCI 目录提供两个有意区分的数据集。`pci-ids.sqlite3` 保留固定上游快照中的全部 42,549 条记录，包括 46 条指向源数据未定义 subvendor 的 subsystem 记录；其严格关系检查继续失败并作为来源边界证据保留。`pci-ids-resolved.sqlite3` 是可选的关系闭合派生快照，供需要所有引用均可解析的调用方显式挂载。

派生版本为 `fe7fceb2e1153d495e1deefacf59c5d61c36ef8e-resolved-v1`，数据集 ID 为 `pci-ids-resolved`。转换只排除 `subsystem` 中目标 `vendor|<subvendorId>` 不存在的记录，不合成厂商或修改其余记录。派生记录按原始顺序保留键和内容，序号重新连续编号；逐条排除记录保存在 `src/Bing.MockData.Catalogs/Data/pci-ids-resolved.exclusions.json`，含原始记录、目标键、原因、源版本及原序号。独立 manifest 记录来源、许可、版本、源文件与派生文件摘要、记录数和闭合核对结果。

从仓库固定输入离线重建：

```powershell
python tools/IndustryImport/derive_pci.py
```

转换脚本读取但不改写 `pci-ids.sqlite3`，不访问网络。每次运行会校验输入身份、原始关系证据、SQLite 完整性、文件格式和大小上限，并确认“派生记录与排除记录”构成原始记录的无交集完整分区。真实转换及验证结果保存在 `artifacts/catalog-closure/pci/derivation-validation.json`。

记录和关系验证摘要：

|检查|结果|
|---|---:|
|原始记录|42,549|
|派生记录|42,503|
|排除 subsystem|46|
|排除目标|6 个未定义 vendor ID|
|派生五类关系缺失|0|
|派生与排除分区闭合|通过|
|派生文件大小|20,488,192 字节|

原始缺失引用按 PCI 原始目录语义继续记为 `VERIFIED_SOURCE_BOUNDARY`，而不是全局允许缺失。关系闭合派生集是有意减少覆盖的消费视图，不能描述成完整 PCI 原始目录；两种数据集应按使用场景明确选择。
