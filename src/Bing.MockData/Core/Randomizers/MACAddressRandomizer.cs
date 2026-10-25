using System;
using System.Globalization;
using System.Linq;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>十六进制 MAC 地址闭区间生成器。</summary>
    public class MACAddressRandomizer : RandomizerBase<MACAddressFieldOptions>, IStringRandomizer
    {
        private readonly AddressRange _range;
        /// <summary>解析范围并验证输出分隔符。</summary>
        public MACAddressRandomizer(MACAddressFieldOptions options) : base(options)
        {
            if (Options.Separator != "" && Options.Separator != ":" && Options.Separator != "-")
                throw new ArgumentException("分隔符只能为空、冒号或连字符。", nameof(options.Separator));
            _range = new AddressRange(AddressRange.ParseMac(Options.Min, false, nameof(options.Min)),
                AddressRange.ParseMac(Options.Max, true, nameof(options.Max)));
        }
        /// <summary>生成地址。</summary>
        public string Generate() => Execute(() => string.Join(Options.Separator, _range.Generate(Rnd).Select(b => b.ToString(Options.Uppercase ? "X2" : "x2", CultureInfo.InvariantCulture))));
    }
}
