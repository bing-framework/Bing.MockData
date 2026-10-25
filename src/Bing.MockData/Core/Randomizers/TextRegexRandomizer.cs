using System;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 根据有限正则子集生成文本，支持字面量、点、常见字符转义、字符类、普通或非捕获分组、
    /// 选择、问号、惰性或贪婪有界量词以及模式两端的 ^、$、\A 和 \z。无界量词、回溯引用、
    /// 零宽断言、命名组和内联选项会被拒绝。
    /// </summary>
    public class TextRegexRandomizer : RandomizerBase<TextRegexFieldOptions>, IStringRandomizer
    {
        private readonly BoundedRegexGenerator _generator;
        /// <summary>验证有限正则模式，并在生成前检查最大文本长度预算。</summary>
        public TextRegexRandomizer(TextRegexFieldOptions options) : base(options)
        {
            if (string.IsNullOrEmpty(Options.Pattern)) throw new ArgumentException("正则模式不能为空。", nameof(options.Pattern));
            _generator = new BoundedRegexGenerator(Options.Pattern, Options.MaxTextLength);
        }
        /// <summary>生成匹配模式的文本；生成期间检查取消令牌。</summary>
        public string Generate() => Execute(() => _generator.Generate(Rnd, Options.CancellationToken));
    }
}
