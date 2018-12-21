# Bing.MockData
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)
这是一个创建随机数据的简单生成器。可进行自定义配置，可生成城市、IP地址、MAC地址、Email地址、文章段落、手机号码、身份证号码、姓名、英文名等。
## Nuget

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

## Nuget 依赖
- NLipsum
- Fare
- Json.Net