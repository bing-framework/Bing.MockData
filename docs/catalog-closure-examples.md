# 目录覆盖收口迁移示例

## 国家与非地理号码

```csharp
using var catalog = SqliteMockCatalog.Open(Path.Combine(dataRoot, "libphonenumber.sqlite3"));
using var context = new MockContext(new MockOptions {
    Seed = 39,
    ReferenceTime = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero)
}.UsePhoneMetadataCatalog(catalog)); // 借用 catalog，调用方负责释放

var cn = context.Phone.Number("CN", PhoneNumberType.Mobile, extension: true);
var satellite = context.Phone.NonGeographicNumber("870", extension: true);
// satellite.CountryCode == "001"，E164 不含分机。
// 881、882 同样可显式寻址；不存在的呼叫码/类型抛出 CatalogCoverageException。
```

呼叫码为无 `+`、无前导零的 1–3 位 ASCII 数字。现有 `IPhoneMetadataProvider` 无需修改；支持非地理号码的自定义 Provider 额外实现 `INonGeographicPhoneMetadataProvider`。旧 Provider 调用新入口抛出 `NotSupportedException`。

已导入 492 条移动/固定电话规则的稳定键清单位于 `tools/CatalogImport/phone-rule-keys.txt`；覆盖的是该固定导入范围，不宣称包括源数据中的所有电话业务类型。生成号码遵守原始模式与 E.164 长度，不能证明实际分配。本地格式保留元数据规则，例如阿根廷移动号码移除国际形式的 `9` 并插入国内移动拨号标记 `15`；格式前缀应用于第一个实际捕获引用。

## 显式选择 PCI 派生快照

```csharp
using var context = new MockContext(new MockOptions { Seed = 39 }
    .UseCatalogFile("devices", Path.Combine(dataRoot, "pci-ids-resolved.sqlite3")));
var device = context.Devices().Device().Build().At(42);
```

路径入口由上下文拥有。仍可使用 `pci-ids.sqlite3` 保留完整原始来源记录；派生版本排除 46 条 subvendor 无法解析的子系统记录，保留记录字段和稳定键。两者是不同数据身份，不能用彼此替代旧重放记录。转换方法、排除清单和关系证据见 [PCI 派生快照](pci-resolved.md)。

## 重放与资源变化

本轮生成契约为 `domain-v6`、内置数据身份为 `china-2025-12-31-domain-v5`，随机派生算法仍为 `fnv64-mix-v1`。内置词库来源替换和电话格式修正会改变部分结果；旧契约或旧数据身份会明确拒绝重放。没有提升 NuGet、程序集或文件版本。

旧接口保持资源结构，但候选覆盖以 [历史资源台账](legacy-resource-ledger.md) 为准。IBAN/BBAN 保留 54 个国家，格式数据使用固定 MIT 许可来源；通用账户模式不保证真实账户或额外国别银行校验。`ChineseAreaCode` 是旧身份证的省级前缀，不是电话区号；旧算法补齐的六位数字不具备真实县级代码含义。需要真实路径时使用现代结构化地址/人物入口。

数据包、干净目录恢复和 CI 配置见 [数据分发与迁移](catalog-data-migration.md)。释放上下文前应取消并等待活动任务结束；枚举器提前结束应 `Dispose`，然后释放拥有目录的上下文。
