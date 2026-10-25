using System;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>中文姓名生成器；Length 为姓氏之后名字的最大长度。</summary>
    public class ChineseNameRandomizer : RandomizerBase<ChineseNameFieldOptions>, IStringRandomizer
    {
        private readonly RandomGenerator _generator;
        /// <summary>复制配置并验证名字长度。</summary>
        public ChineseNameRandomizer(ChineseNameFieldOptions options) : base(options)
        {
            if (Options.Length < 1) throw new ArgumentOutOfRangeException(nameof(options.Length));
            _generator = new RandomGenerator(Rnd);
        }
        /// <summary>生成姓名。</summary>
        public string Generate() => Execute(() => CommonData.Instance.ChineseFirstNames[Rnd.Next(CommonData.Instance.ChineseFirstNames.Count)] +
            _generator.GenerateRandomLengthChinese(Options.Length));
    }
}
