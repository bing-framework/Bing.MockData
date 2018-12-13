using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using NLipsum.Core;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 段落随机生成器
    /// </summary>
    public class TextLipsumRandomizer : RandomizerBase<TextLipsumFieldOptions>, IStringRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly LipsumGenerator _generator = new LipsumGenerator();

        /// <summary>
        /// 初始化一个<see cref="TextLipsumRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">段落配置</param>
        public TextLipsumRandomizer(TextLipsumFieldOptions options) : base(options)
        {
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            return IsNull() ? null : _generator.GenerateLipsum(Options.Paragraphs);
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
