# Bing.MockData
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)

这是一个创建随机数据的简单生成器。可进行自定义配置，可生成城市、IP地址、MAC地址、Email地址、文章段落、手机号码、身份证号码、姓名、英文名等。

## Nuget
|Nuget|版本号|说明|
|---|---|---|
|Bing.MockData|[![NuGet Badge](https://buildstats.info/nuget/Bing.MockData?includePreReleases=true)](https://www.nuget.org/packages/Bing.MockData)|

## 支持的随机数据
- 身份证号码：`ChineseIdCardRandomizer`
- 城市：`CityRandomizer`
- 国家：`CountryRandomizer`
- 日期时间：`DateTimeRandomizer`
- Email 地址：`EmailAddressRandomizer`
- 英文名（FirstName、LastName、FullName）：`FirstNameRandomizer`、`LastNameRandomizer`、`FullNameRandomizer`
- Guid：`GuidRandomizer`
- IBAN：`IBANRandomizer`
- IP 地址（IP4、IP6）：`IPv4AddressRandomizer`、`IPv6AddressRandomizer`
- MAC 地址：`MACAddressRandomizer`
- 手机号码：`MobileRandomizer`
- 随机数（int、long、float、double....）：`NumberRandomizer<T>`
- 自定义字符串列表：`StringListRandomizer`、`TextRegexRandomizer`、`TextRandomizer`
- 单词：`TextWordsRandomizer`
- 时间跨度：`TimeSpanRandomizer`
- 文章段落：`TextLipsumRandomizer`
- 地址：`ChineseAddressRandomizer`
- 姓名：`ChineseNameRandomizer`
- 银行卡号：`BankCardRandomizer`

## 尚未支持的随机数据


## 使用方式
```c#
// 生成随机身份证
var randomizer = RandomizerFactory.GetRandomizer(new ChineseIdCardFieldOptions());
var idcard = randomizer.Generate();
var time = randomizer.GenerateValidPeriod();
var address = randomizer.GenerateIssueOrg();

// 生成随机城市(国外)
var randomizer = RandomizerFactory.GetRandomizer(new CityFieldOptions());
var result = randomizer.Generate();

// 生成随机国家(英文名)
var randomizer = RandomizerFactory.GetRandomizer(new CountryFieldOptions());
var result = randomizer.Generate();

// 生成随机时间
var randomizer = RandomizerFactory.GetRandomizer(new DateTimeFieldOptions());
var result = randomizer.Generate();

// 生成随机Email地址
var randomizer = RandomizerFactory.GetRandomizer(new EmailAddressFieldOptions());
var result = randomizer.Generate();

// 生成随机英文名(FirstName)
var randomizer = RandomizerFactory.GetRandomizer(new FirstNameFieldOptions());
var result = randomizer.Generate();

// 生成随机英文名(LastName)
var randomizer = RandomizerFactory.GetRandomizer(new LastNameFieldOptions());
var result = randomizer.Generate();

// 生成随机英文名(FullName)
var randomizer = RandomizerFactory.GetRandomizer(new FullNameFieldOptions());
var result = randomizer.Generate();

// 生成随机GUID
var randomizer = RandomizerFactory.GetRandomizer(new GuidFieldOptions());
var result = randomizer.Generate();

// 生成随机IBAN
var randomizer = RandomizerFactory.GetRandomizer(new IBANFieldOptions());
var result = randomizer.Generate();

// 生成随机IP地址(IP4)
var randomizer = RandomizerFactory.GetRandomizer(new IPv4AddressFieldOptions());
var result = randomizer.Generate();

// 生成随机IP地址(IP6)
var randomizer = RandomizerFactory.GetRandomizer(new IPv6AddressFieldOptions());
var result = randomizer.Generate();

// 生成随机MAC地址
var randomizer = RandomizerFactory.GetRandomizer(new MACAddressFieldOptions());
var result = randomizer.Generate();

// 生成随机手机号码
var randomizer = RandomizerFactory.GetRandomizer(new MobileFieldOptions());
var result = randomizer.Generate();

// 生成随机数值(int,long,float,double...)
var randomizer = RandomizerFactory.GetRandomizer<int>(new NumberFieldOptions<int>() {Min = 0, Max = 1000});
var result = randomizer.Generate();

// 生成随机自定义字符串
var randomizer = RandomizerFactory.GetRandomizer(new StringListFieldOptions(){Values = new List<string>() {"张三", "李四", "王五"}});
var result = randomizer.Generate();

// 生成随机段落(英文)
var randomizer = RandomizerFactory.GetRandomizer(new TextLipsumFieldOptions());
var result = randomizer.Generate();

// 生成随机文本
var randomizer = RandomizerFactory.GetRandomizer(new TextFieldOptions() {Min = 3, Max = 20, UseLetter = true, UseNumber = true});
var result = randomizer.Generate();

// 按照正则表达式随机生成文本
var randomizer = RandomizerFactory.GetRandomizer(new TextRegexFieldOptions() {Pattern = @"^[0-9]{4}[A-Z]{2}"});
var result = randomizer.Generate();

// 生成随机单词
var randomizer = RandomizerFactory.GetRandomizer(new TextWordsFieldOptions() {Min = 3, Max = 20});
var result = randomizer.Generate();

// 生成随机时间跨度
var randomizer = RandomizerFactory.GetRandomizer(new TimeSpanFieldOptions() {From = DateTime.Now.TimeOfDay, To = DateTime.Now.AddDays(20).TimeOfDay});
var result = randomizer.GenerateAsString();

// 生成随机地址
var randomizer = RandomizerFactory.GetRandomizer(new ChineseAddressFieldOptions());
var result = randomizer.Generate();
var region = randomizer.GenerateRegion();

// 生成随机姓名
var randomizer = RandomizerFactory.GetRandomizer(new ChineseNameFieldOptions());
var result = randomizer.Generate();

// 生成随机银行卡号
var randomizer = RandomizerFactory.GetRandomizer(new BankCardFieldOptions());
var result = randomizer.Generate();
```

## 依赖类库
- NLipsum
- Fare
- Json.Net

## 作者

简玄冰

## 贡献与反馈

> 如果你在阅读或使用Bing中任意一个代码片断时发现Bug，或有更佳实现方式，请通知我们。

> 为了保持代码简单，目前很多功能只建立了基本结构，细节特性未进行迁移，在后续需要时进行添加，如果你发现某个类无法满足你的需求，请通知我们。

> 你可以通过github的Issue或Pull Request向我们提交问题和代码，如果你更喜欢使用QQ进行交流，请加入我们的交流QQ群。

> 对于你提交的代码，如果我们决定采纳，可能会进行相应重构，以统一代码风格。

> 对于热心的同学，将会把你的名字放到**贡献者**名单中。

## 免责声明
- 虽然我们对代码已经进行高度审查，并用于自己的项目中，但依然可能存在某些未知的BUG，如果你的生产系统蒙受损失，Bing 团队不会对此负责。
- 出于成本的考虑，我们不会对已发布的API保持兼容，每当更新代码时，请注意该问题。

## 开源地址
[https://github.com/bing-framework/Bing.QRCode](https://github.com/bing-framework/Bing.QRCode)

## License

**MIT**

> 这意味着你可以在任意场景下使用 Bing 应用框架而不会有人找你要钱。

> Bing 会尽量引入开源免费的第三方技术框架，如有意外，还请自行了解。
