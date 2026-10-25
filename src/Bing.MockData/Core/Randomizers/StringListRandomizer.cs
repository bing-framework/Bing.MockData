using System;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>从配置快照中的列表抽取字符串。</summary>
    public class StringListRandomizer : RandomizerBase<StringListFieldOptions>, IStringRandomizer
    {
        private readonly RandomStringFromListGenerator _generator;
        /// <summary>复制并验证候选列表。</summary>
        public StringListRandomizer(StringListFieldOptions options) : base(options)
        {
            if (Options.Values == null || Options.Values.Count == 0 || Options.Values.Contains(null))
                throw new ArgumentException("候选列表不能为空或包含 null。", nameof(options.Values));
            _generator = new RandomStringFromListGenerator(Rnd, Options.Values);
        }
        /// <summary>生成结果。</summary>
        public string Generate() => Execute(() => _generator.Generate());
    }
}
