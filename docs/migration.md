# 配置与生成契约迁移（此前修复记录）

本文保留上一轮迁移记录。其中可空、正则和目标框架契约已由[能力升级迁移对照](capabilities-migration.md)取代；新接入请以新指南为准。

本次包含已批准的破坏性 API 和行为调整，库仍面向 `netstandard2.0`。库版本号未自动提升。

## 工厂、配置和随机源

- `RandomizerFactory` 每次创建独立实例，不再缓存配置/生成器。需要连续序列时，请持有并复用生成器实例。
- 所有内置配置新增 `int? Seed`。同一库版本、运行时、完整配置和顺序调用序列可重放；省略时为实例自动初始化种子。构造时复制配置，列表和手机号前缀数组会单独复制，构造之后修改原配置不影响已有实例。
- 同一实例允许多线程调用，每次完整生成在实例锁内完成。并发调用的先后次序不固定，因此不保证某个线程收到哪一项。多个实例的随机状态相互独立。
- 自定义配置继承 `FieldOptionsBase` 时，如果包含可变引用成员，应重写 `Clone()` 并复制这些成员。工厂只支持内置配置类型及其继承类型，无法识别的自定义实现会立即抛出 `NotSupportedException`。
- 删除未实现的 `FieldOptionsBase.ValueAsString`、`FieldOptionsBase.Language`。使用对应的 `GenerateAsString()` 或显式格式化；内嵌 JSON 的语言仍由 `DataLoader` 的 `LanguageType` 参数选择。
- `UseNullValues=true` 时，每次调用先按 10% 概率返回 null；包括中文地址、地区、身份证、签发机关、有效期、银行卡、手机号。允许重复输出，生成器不提供唯一性保证。

```csharp
var options = new IntFieldOptions { Min = 1, Max = 6, Seed = 42 };
var first = RandomizerFactory.GetRandomizer<int>(options);
var second = RandomizerFactory.GetRandomizer<int>(options);
// 顺序调用时，first 与 second 的第 n 个结果相同。
options.Min = 100; // 不改变以上两个实例；新配置必须满足 Min <= Max。
```

## 范围与格式

| 配置 | 新契约 |
|---|---|
| 数值 Min / Max | 两端包含；相等返回固定值；倒置拒绝；float/double 拒绝 NaN/无穷。支持 bool、byte、short、int、long、float、double、DateTime、TimeSpan；decimal 等类型立即拒绝。浮点结果是离散插值样本，不表示全部可表示浮点数等概率。 |
| 日期 From / To | 两端包含并保留 Kind；不同 Kind 拒绝，不隐式转换时区。`IncludeTime=false` 只生成原区间内的午夜；不存在这样的值时拒绝配置。 |
| 时间跨度 | `IncludeMilliseconds=true` 保留 tick 精度；false 只生成原区间内的整秒，负值同样遵循向内对齐；没有有效值时拒绝。 |
| 文本 Min / Max | 长度闭区间；0 可以生成空字符串；仅空格合法；非零长度且字符集为空时拒绝。 |
| 单词/段落 | 单词数闭区间，0 为空文本；每段 3～7 句，每句 4～12 词，首字母大写并以句点结束，段落之间为 `\n\n`；0 段为空文本。 |
| IPv4 / IPv6 | 以完整地址数值抽样，支持跨网段。IPv4 要求四段十进制，IPv6 支持压缩格式；非零 scope ID 不支持。空边界分别表示地址族最小/最大值。 |
| MAC | 接受六个十六进制字节的冒号、连字符或无分隔符形式。输出 Separator 为 `:`、`-` 或空字符串；Uppercase 控制默认大小写。 |
| 中文姓名 Length | 名字字符数的最大值（最小为 1），不含单字/复姓；总长度还需加上姓氏长度。 |
| 手机号 Length | 总长度必须至少容纳所选全部前缀。前缀列表不可为空；FakePrefix 必须为非空 ASCII 数字串。 |

`TextFieldOptions.UseLetter` 统一控制大写和小写字符开关；`FullNameFieldOptions.Left2Right` 和 `EmailAddressFieldOptions.Left2Right` 控制名/姓顺序。字符串生成器的 `Generate(bool)` 在生成后转换大小写；对正则生成器而言，转换后的结果可能不再满足大小写敏感模式。正则采用 Fare 支持的语法，不保证支持任意 .NET Regex 语法。

日期和时间字符串使用配置格式及 `InvariantCulture`。GUID 按实例种子生成并保留版本 4 和 RFC variant 位。本库用于模拟数据，随机序列和生成的 GUID 不适合作为安全令牌。

## IBAN 与资源

- `IBANFieldOptions.Type` 仅接受 IBAN、BBAN、BOTH，类型和国家代码不区分大小写；未知类型/国家在构造时拒绝。
- IBAN 同时满足内置国家格式和 MOD 97 校验；BBAN 仅保证对应格式。BOTH 在两类规则中抽样。不会保证真实账号存在，也不会计算各国额外的国内校验位。
- `Datas.Models.IBAN.Generator` 已移除，改为不含随机状态的 `Pattern` 字符串。各实例维护自己的 Fare 生成器。
- 已核对原有 54 个国家规则，纠正 DK、KZ、KW、PL、MR、TN、TR 等错误；未扩展国家范围。来源：[SWIFT IBAN Registry v101（2025-12）](https://www.swift.com/sites/default/files/files/iban-registry-v101.pdf)。
- `DataLoader` 正确传递语言，缺失资源抛出含完整名称的 `FileNotFoundException`；文本行会在返回前读取完并关闭流。JSON 使用明确的最大嵌套深度。
- 中英文 Colors 资源原先标反，现已对调；Jobs.en 原先错误包含邮箱后缀，现改为英文职业样本。多语言 JSON 固定嵌入主程序集。

## 构建与发布

测试迁移至 net10.0；SDK 固定 10.0.400。Newtonsoft.Json 升至 13.0.4，升级后的测试依赖使用锁文件。Debug 与 Release 输出分别位于 `output/Debug/`、`output/Release/`。

`build/pack.bat` 现在是 PowerShell 7 脚本的入口，不再接受旧式位置参数密钥。默认只验证并打包；发布必须显式指定 `-Publish`，通过环境变量 `NUGET_API_KEY` 传入密钥。前序还原、审计、测试、打包或消费失败都必须中止。每轮包输出隔离，不通配推送历史包。若历史日志曾实际包含有效密钥，需要由持有人在 NuGet 轮换；本次不访问或修改账户凭据。

本地测试和包验证不替代远程 Windows/Linux CI 的实际运行；工作流已配置，执行证据需在推送后获得。
