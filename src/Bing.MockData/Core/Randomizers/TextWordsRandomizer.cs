using System;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>按闭区间词数生成可重放文本。</summary>
    public class TextWordsRandomizer : RandomizerBase<TextWordsFieldOptions>, IStringRandomizer
    {
        /// <summary>校验词数范围。</summary>
        public TextWordsRandomizer(TextWordsFieldOptions options) : base(options)
        {
            if (Options.Max > Options.MaxTextLength) throw new MockLimitException(nameof(Options.MaxTextLength), Options.MaxTextLength);
            if (Options.Min < 0) throw new ArgumentOutOfRangeException(nameof(options.Min));
            if (Options.Max < Options.Min) throw new ArgumentOutOfRangeException(nameof(options.Max));
        }
        /// <summary>生成单词，词数为零时返回空字符串。</summary>
        public string Generate() => Execute(() => LipsumWords.GenerateWords(Rnd, RandomValueGenerator.NextInt(Rnd, Options.Min, Options.Max), Options.MaxTextLength, Options.CancellationToken));
    }
}
