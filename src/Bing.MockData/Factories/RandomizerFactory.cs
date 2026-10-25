using System;
using Bing.MockData.Abstractions.Options;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;

namespace Bing.MockData.Factories
{
    /// <summary>按内置配置显式创建生成器，每次调用返回独立实例。</summary>
    public static class RandomizerFactory
    {
        /// <summary>创建字符串生成器。</summary>
        public static IStringRandomizer GetRandomizer(IStringFieldOptions options) => (IStringRandomizer)Create(options);
        /// <summary>创建 GUID 生成器。</summary>
        public static IGuidRandomizer GetRandomizer(IGuidFieldOptions options) => (IGuidRandomizer)Create(options);
        /// <summary>创建闭区间数值生成器。</summary>
        public static INumberRandomizer<T> GetRandomizer<T>(INumberFieldOptions<T> options) where T : struct
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options is NumberFieldOptions<T> number) return new NumberRandomizer<T>(number);
            throw Unsupported(options);
        }
        /// <summary>创建日期生成器。</summary>
        public static IDateTimeRandomizer GetRandomizer(IDateTimeFieldOptions options) => (IDateTimeRandomizer)Create(options);
        /// <summary>创建时间跨度生成器。</summary>
        public static ITimeSpanRandomizer GetRandomizer(ITimeSpanFieldOptions options) => (ITimeSpanRandomizer)Create(options);
        /// <summary>创建手机号生成器。</summary>
        public static IMobileRandomizer GetRandomizer(IMobileFieldOptions options) => (IMobileRandomizer)Create(options);
        /// <summary>创建身份证生成器。</summary>
        public static IIdCardRandomizer GetRandomizer(IIdCardFieldOptions options) => (IIdCardRandomizer)Create(options);
        /// <summary>创建地址生成器。</summary>
        public static IAddressRandomizer GetRandomizer(IAddressFieldOptions options) => (IAddressRandomizer)Create(options);
        /// <summary>创建银行卡生成器。</summary>
        public static IBankCardRandomizer GetRandomizer(IBankCardFieldOptions options) => (IBankCardRandomizer)Create(options);
        private static NotSupportedException Unsupported(object options) =>
            new NotSupportedException($"配置类型 '{options.GetType()}' 尚未支持。");
        internal static object Create(object options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options is NumberFieldOptions<sbyte> n0) return new NumberRandomizer<sbyte>(n0);
            if (options is NumberFieldOptions<ushort> n1) return new NumberRandomizer<ushort>(n1);
            if (options is NumberFieldOptions<uint> n2) return new NumberRandomizer<uint>(n2);
            if (options is NumberFieldOptions<ulong> n3) return new NumberRandomizer<ulong>(n3);
            if (options is NumberFieldOptions<decimal> n4) return new NumberRandomizer<decimal>(n4);
            if (options is NumberFieldOptions<bool> boolean) return new NumberRandomizer<bool>(boolean);
            if (options is NumberFieldOptions<byte> b) return new NumberRandomizer<byte>(b);
            if (options is NumberFieldOptions<short> s) return new NumberRandomizer<short>(s);
            if (options is NumberFieldOptions<int> i) return new NumberRandomizer<int>(i);
            if (options is NumberFieldOptions<long> l) return new NumberRandomizer<long>(l);
            if (options is NumberFieldOptions<float> f) return new NumberRandomizer<float>(f);
            if (options is NumberFieldOptions<double> d) return new NumberRandomizer<double>(d);
            if (options is NumberFieldOptions<DateTime> dt) return new NumberRandomizer<DateTime>(dt);
            if (options is NumberFieldOptions<TimeSpan> ts) return new NumberRandomizer<TimeSpan>(ts);
            if (options is TextFieldOptions text) return new TextRandomizer(text);
            if (options is TextRegexFieldOptions regex) return new TextRegexRandomizer(regex);
            if (options is TextWordsFieldOptions words) return new TextWordsRandomizer(words);
            if (options is TextLipsumFieldOptions lipsum) return new TextLipsumRandomizer(lipsum);
            if (options is StringListFieldOptions list) return new StringListRandomizer(list);
            if (options is FirstNameFieldOptions first) return new FirstNameRandomizer(first);
            if (options is LastNameFieldOptions last) return new LastNameRandomizer(last);
            if (options is FullNameFieldOptions full) return new FullNameRandomizer(full);
            if (options is EmailAddressFieldOptions email) return new EmailAddressRandomizer(email);
            if (options is CityFieldOptions city) return new CityRandomizer(city);
            if (options is CountryFieldOptions country) return new CountryRandomizer(country);
            if (options is ChineseNameFieldOptions name) return new ChineseNameRandomizer(name);
            if (options is IBANFieldOptions iban) return new IBANRandomizer(iban);
            if (options is IPv4AddressFieldOptions ipv4) return new IPv4AddressRandomizer(ipv4);
            if (options is IPv6AddressFieldOptions ipv6) return new IPv6AddressRandomizer(ipv6);
            if (options is MACAddressFieldOptions mac) return new MACAddressRandomizer(mac);
            if (options is GuidFieldOptions guid) return new GuidRandomizer(guid);
            if (options is DateTimeFieldOptions date) return new DateTimeRandomizer(date);
            if (options is TimeSpanFieldOptions span) return new TimeSpanRandomizer(span);
            if (options is MobileFieldOptions mobile) return new MobileRandomizer(mobile);
            if (options is ChineseIdCardFieldOptions id) return new ChineseIdCardRandomizer(id);
            if (options is ChineseAddressFieldOptions address) return new ChineseAddressRandomizer(address);
            if (options is BankCardFieldOptions card) return new BankCardRandomizer(card);
            throw Unsupported(options);
        }
    }
}
