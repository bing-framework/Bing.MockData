using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 手机号码随机生成器
    /// </summary>
    public class MobileRandomizer:RandomizerBase<MobileFieldOptions>,IMobileRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly RandomGenerator _generator;

        /// <summary>
        /// 初始化一个<see cref="MobileRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options"></param>
        public MobileRandomizer(MobileFieldOptions options) : base(options)
        {
            _generator = new RandomGenerator();
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

            var prefix = GetMobilePrefix();
            return $"{prefix}{_generator.GenerateNumber(Options.Length - prefix.Length)}";
        }

        /// <summary>
        /// 获取手机号码前缀
        /// </summary>
        /// <returns></returns>
        private string GetMobilePrefix()
        {
            if (Options.UseFake)
            {
                return Options.FakePrefix;
            }

            return Options.MobilePrefixs[_generator.GenerateInt(0, Options.MobilePrefixs.Length)].ToString();
        }
    }
}
