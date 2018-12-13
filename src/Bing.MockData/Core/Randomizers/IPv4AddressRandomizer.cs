using System.Linq;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// IP4地址随机生成器
    /// </summary>
    public class IPv4AddressRandomizer:RandomizerBase<IPv4AddressFieldOptions>,IStringRandomizer
    {
        /// <summary>
        /// 默认最小值
        /// </summary>
        private readonly byte[] _defaultMin = new byte[] {0, 0, 0, 0};

        /// <summary>
        /// 默认最大值
        /// </summary>
        private readonly byte[] _defaultMax = new byte[] {0xff, 0xff, 0xff, 0xff};

        /// <summary>
        /// 生成器
        /// </summary>
        private readonly RandomThingsGenerator<byte>[] _generators = new RandomThingsGenerator<byte>[4];

        /// <summary>
        /// 初始化一个<see cref="IPv4AddressRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">IP4配置</param>
        public IPv4AddressRandomizer(IPv4AddressFieldOptions options) : base(options)
        {
            byte[] octetsMin = string.IsNullOrEmpty(options.Min)
                ? _defaultMin
                : options.Min.Split('.').Select(byte.Parse).ToArray();
            byte[] octetsMax = string.IsNullOrEmpty(options.Max)
                ? _defaultMax
                : options.Max.Split('.').Select(byte.Parse).ToArray();

            for (var i = 0; i < 4; i++)
            {
                _generators[i] = new RandomThingsGenerator<byte>(octetsMin[i], octetsMax[i]);
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

            return string.Join(".", _generators.Select(gen => $"{gen.Generate()}").ToArray());
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
