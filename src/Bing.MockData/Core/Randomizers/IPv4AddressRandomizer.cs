using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>按完整地址闭区间生成 IPv4Address。</summary>
    public class IPv4AddressRandomizer : RandomizerBase<IPv4AddressFieldOptions>, IStringRandomizer
    {
        private readonly AddressRange _range;
        /// <summary>解析并校验地址范围。</summary>
        public IPv4AddressRandomizer(IPv4AddressFieldOptions options) : base(options)
        {
            _range = new AddressRange(AddressRange.ParseIp(Options.Min, System.Net.Sockets.AddressFamily.InterNetwork, false, nameof(options.Min)),
                AddressRange.ParseIp(Options.Max, System.Net.Sockets.AddressFamily.InterNetwork, true, nameof(options.Max)));
        }
        /// <summary>生成地址。</summary>
        public string Generate() => Execute(() =>
        {
            var bytes = _range.Generate(Rnd);
            return new System.Net.IPAddress(bytes).ToString();
        });
    }
}
