using System;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>每段 3～7 句，每句 4～12 词的可重放文章生成器。</summary>
    public class TextLipsumRandomizer : RandomizerBase<TextLipsumFieldOptions>, IStringRandomizer
    {
        /// <summary>校验非负段落数。</summary>
        public TextLipsumRandomizer(TextLipsumFieldOptions options) : base(options)
        {
            if (Options.Paragraphs > Options.MaxTextLength) throw new MockLimitException(nameof(Options.MaxTextLength), Options.MaxTextLength);
            if (Options.Paragraphs < 0) throw new ArgumentOutOfRangeException(nameof(options.Paragraphs));
        }
        /// <summary>生成以两个换行符分隔的段落。</summary>
        public string Generate() => Execute(() => LipsumWords.Paragraphs(Rnd, Options.Paragraphs, Options.MaxTextLength, Options.CancellationToken));
    }
}
