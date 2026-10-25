# 可筛选的模拟样本

这些样本由库内的原创模拟数据组成，不表示真实企业、品牌、职位、客户端使用分布或全球目录。调用 `Careers`、`Products`、`ClientProfiles` 创建生成器时，会复制 options 和候选列表；之后修改原配置不会改变已创建的生成器。候选配置数量受上下文 `MaxCollectionSize` 限制，生成值继续受文本长度预算约束。

## 职业组合

`CareerProfile` 将行业、部门和职业作为一个一致的组合生成；可用行业和部门筛选，筛选结果为空时立即抛出 `ArgumentException`。

```csharp
var career = context.People.Career(new CareerProfileOptions
{
    Industry = "信息技术",
    Department = "研发部"
});

var careers = context.People.Careers(new CareerProfileOptions
{
    Industry = "制造业"
});
var batch = careers.GenerateMany(100);
```

`People.Job(industry, department)` 与 `CompanyData.Departments(industry)` 只查询当前语言的内置模拟样本。`People.Job()`、`Companies.Industry()` 和 `Companies.Department()` 保留原有无参调用。

## 商品分类与品牌

`ProductSample` 返回根到末级的分类路径，品牌、材质、规格和单位来自同一个分类定义。内置分类包含多层父子关系、多项规格以及多个关联品牌；`en-US` 会在创建快照时将内置候选转换为配套英文样本，并让分类查询、路径和生成结果共享这份冻结定义。调用方传入的自定义分类及候选值保持原样，不会被自动翻译。自定义分类必须使用唯一代码、有效父级且无环；可选分类需要同时提供材质、规格、单位和品牌。

```csharp
var product = context.Commerce.Product(new ProductSampleOptions
{
    CategoryCode = "electronics", // 可传叶节点，也可传父节点
    Brand = "云帆数码（模拟）"
});

var categories = context.Commerce.Categories();
var brands = context.Commerce.Brands("electronics");
var products = context.Commerce.Products(new ProductSampleOptions { CategoryCode = "home" });
```

`ProductSampleOptions.Categories` 可用调用方数据完整替换内置分类树，品牌筛选只会匹配实际关联该品牌的分类，并在生成时固定返回所选品牌。`Commerce.Product`、`Products` 与 `Category(options)` 使用一致的分类抽样；对象模板可使用 `context.Templates.ConfiguredProduct(options)` 保持分类与品牌、材质、规格和单位一致。

## 客户端资料

客户端样本包含桌面、手机和平板的成组浏览器、操作系统、User-Agent 和设备类型。筛选使用浏览器名及系统名的区分大小写精确匹配；未找到匹配样本时抛出 `ArgumentException`。User-Agent 仅为格式样例，不代表真实流量分布。

```csharp
var mobile = context.Network.Client(new ClientProfileOptions
{
    Browser = "Chrome",
    OperatingSystem = "Android",
    DeviceType = ClientDeviceType.Mobile
});

var tablets = context.Network.ClientProfiles(new ClientProfileOptions
{
    DeviceType = ClientDeviceType.Tablet
}).GenerateMany(50);
```

`ClientProfileOptions.Profiles` 可传入自定义的只读候选列表；`ClientProfile` 本身不可变，生成器创建时会再复制一次。

## 命名颜色

`Colors.Palette()` 提供 CSS 标准的 148 个命名色及对应 RGB/HEX/HSL 值。色名使用标准英文标识，与上下文语言无关。色系按颜色的 HSL 值分组，`Named()` 保留原签名并扩展到完整表；需要限制色系时使用 `NamedColor`。

```csharp
var allCssColors = context.Colors.Palette();
var greens = context.Colors.Palette(ColorFamily.Green);
var accent = context.Colors.NamedColor(ColorFamily.Purple);
```
