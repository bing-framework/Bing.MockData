using System.Globalization;
using System.Linq;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// IP6地址随机生成器
    /// </summary>
    public class IPv6AddressRandomizer : RandomizerBase<IPv6AddressFieldOptions>, IStringRandomizer
    {
        /// <summary>
        /// 默认最小值
        /// </summary>
        private readonly int[] _defaultMin = new int[] {0, 0, 0, 0, 0, 0, 0, 0};

        /// <summary>
        /// 默认最大值
        /// </summary>
        private readonly int[] _defaultMax = new int[] {0xffff, 0xffff, 0xffff, 0xffff, 0xffff, 0xffff, 0xffff, 0xffff};

        /// <summary>
        /// 生成器
        /// </summary>
        private readonly RandomThingsGenerator<int>[] _generators = new RandomThingsGenerator<int>[8];

        /// <summary>
        /// 初始化一个<see cref="IPv6AddressRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">IP6配置</param>
        public IPv6AddressRandomizer(IPv6AddressFieldOptions options) : base(options)
        {
            int[] hextetsMin = string.IsNullOrEmpty(options.Min)
                ? _defaultMin
                : options.Min.Split(':').Select(x => int.Parse(x, NumberStyles.HexNumber)).ToArray();
            int[] hextetsMax = string.IsNullOrEmpty(options.Max)
                ? _defaultMax
                : options.Max.Split(':').Select(x => int.Parse(x, NumberStyles.HexNumber)).ToArray();

            for (var i = 0; i < 8; i++)
            {
                _generators[i] = new RandomThingsGenerator<int>(hextetsMin[i], hextetsMax[i]);
            }
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            if (IsNull())
            {
                return null;
            }

            return string.Join(":", _generators.Select(gen => $"{gen.Generate():X4}").ToArray());
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="upperCase">是否大写</param>
        /// <returns></returns>
        public string Generate(bool upperCase)
        {
            return Generate().ToCasedInvariant(upperCase);
        }
    }
}
