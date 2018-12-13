using System;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
using NLipsum.Core;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 单词随机生成器
    /// </summary>
    public class TextWordsRandomizer : RandomizerBase<TextWordsFieldOptions>, IStringRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly LipsumGenerator _generator = new LipsumGenerator();

        /// <summary>
        /// 数量生成器
        /// </summary>
        private readonly RandomThingsGenerator<int> _numberGenerator;

        /// <summary>
        /// 初始化一个<see cref="TextWordsRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options"></param>
        public TextWordsRandomizer(TextWordsFieldOptions options) : base(options)
        {
            _numberGenerator = new RandomThingsGenerator<int>(Math.Min(options.Min, options.Max),
                Math.Max(options.Min, options.Max) + 1);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            return IsNull() ? null : string.Join(" ", _generator.GenerateWords(_numberGenerator.Generate()));
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
