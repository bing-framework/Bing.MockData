using System.Linq;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 文本随机生成器
    /// </summary>
    public class TextRandomizer:RandomizerBase<TextFieldOptions>,IStringRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly RandomStringGenerator _generator;

        /// <summary>
        /// 初始化一个<see cref="TextRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">文本配置</param>
        public TextRandomizer(TextFieldOptions options) : base(options)
        {
            _generator = new RandomStringGenerator(options.UseUppercase, options.UseLowercase, options.UseNumber,
                options.UseSpecial, options.UseSpace);
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            return IsNull() ? null : _generator.Generate(Options.Min, Options.Max);
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
