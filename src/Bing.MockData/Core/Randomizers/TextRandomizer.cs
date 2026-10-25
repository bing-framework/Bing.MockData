using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>从启用字符集中生成闭区间长度的文本。</summary>
    public class TextRandomizer : RandomizerBase<TextFieldOptions>, IStringRandomizer
    {
        private readonly RandomStringGenerator _generator;
        /// <summary>复制配置并校验长度与字符集。</summary>
        public TextRandomizer(TextFieldOptions options) : base(options)
        {
            _generator = new RandomStringGenerator(Rnd, Options.UseLetter && Options.UseUppercase,
                Options.UseLetter && Options.UseLowercase, Options.UseNumber, Options.UseSpecial, Options.UseSpace);
            if (Options.Max > Options.MaxTextLength) throw new MockLimitException(nameof(Options.MaxTextLength), Options.MaxTextLength);
            _generator.Validate(Options.Min, Options.Max);
        }
        /// <summary>生成文本；长度为零时返回空字符串。</summary>
        public string Generate() => Execute(() => _generator.Generate(Options.Min, Options.Max, Options.CancellationToken));
    }
}
