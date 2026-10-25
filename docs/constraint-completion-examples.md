# 组合约束与自动对象生成

## 正则与长度

```csharp
using var context = new MockContext(new MockOptions { Seed = 91 });
string code = context.Text.Regex("^[A-Z]{1,10000}$", 10, 10);
```

新增重载按照 UTF-16 长度规划有限正则的分支、序列和重复次数。仍不支持无限量词、回溯引用及零宽断言；复杂稀疏长度可能耗尽尝试或步骤预算。旧 `Regex(pattern)` 保留按模式最大输出长度预检查的行为。

## 自动对象、字典和集合

```csharp
public class Line
{
    [Range(1, 20)] public int Quantity { get; set; }
    [RegularExpression("^[A-Z]{1,10000}$")]
    [StringLength(10, MinimumLength = 10)]
    public string Code { get; set; }
}
public class Document
{
    public Line Main { get; set; }
    [MinLength(2), MaxLength(5)]
    public IDictionary<string, Line> Lines { get; set; }
    [MinLength(3), MaxLength(3)]
    public ISet<int> Tags { get; set; }
}

var generator = context.AutoBuilder<Document>().Build();
var record = generator.At(42);
var stream = generator.Enumerate(1000);
```

添加 `System.ComponentModel.DataAnnotations` 与 `System.Collections.Generic`。无参 POCO、字典和集合的生成计划在 Build 时编译；支持 Dictionary/IDictionary/IReadOnlyDictionary、HashSet/ISet，以及原有一维数组/列表。嵌套类型上的 Compare 与验证特性继续生效。显式 RuleFor 优先，其次使用唯一类型注册；无无参构造的类型需要注册生成器或显式字段规则。循环类型在构建时拒绝，可通过显式规则终止循环。字典键和集合元素的去重受重试预算约束，有限候选耗尽时不会偷偷减少元素数量。

## Schema 联合约束

```csharp
using var schema = CompiledSchema.Compile(
    "{\"type\":\"string\",\"format\":\"email\",\"maxLength\":10}",
    options: new SchemaOptions { AssertFormat = true });
var sample = schema.Generator(context).At(42);
```

需引用 `Bing.MockData.Schema`。格式构造支持受限长度的邮箱、主机名、URI、IP、时间等；超短格式候选为语法样例，不声明域名实际存在。format 注解不会被强制变为断言。数组规划 contains 匹配数量，并在存在 maxContains 时为其他位置添加否定条件；已知 const 前缀计入匹配数量。

not 对数值区间、const/enum、anyOf/allOf 和嵌套 not 可分析子式计算补集；整数和小数刻度保留精确计算。有限值提前筛除已知不符合条件的候选。复杂引用、任意字符串否定及交叉组合仍可能需要根验证后的有界重试；这不是任意有解 Schema 都必定成功的求解器。每个返回样本仍经过完整验证，未找到候选报告 BudgetExhausted。

## 商品模板与重放迁移

```csharp
var products = context.Templates.ConfiguredProduct(
    new ProductSampleOptions { CategoryCode = "electronics" }).Build();
```

分类、材质、规格、单位及品牌来自同一个冻结样本。原 `Templates.Product()` 也使用该关系生成。配置样本、客户端与颜色见 [样本配置示例](configurable-samples.md)。

本轮核心生成契约更新为 `domain-v3`，内置数据身份为 `china-2025-12-31-domain-v3`；中国区划原始快照没有变化。Schema 搜索身份更新为 `schema-search-v3`。旧重放记录必须使用原实现重放，新实现会拒绝旧身份；NuGet 包版本未提升。
