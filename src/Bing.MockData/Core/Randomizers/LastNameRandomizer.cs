using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>从固定资源中抽取 LastName。</summary>
    public class LastNameRandomizer : RandomizerBase<LastNameFieldOptions>, IStringRandomizer
    {
        private readonly RandomStringFromListGenerator _generator;
        /// <summary>复制配置并绑定资源。</summary>
        public LastNameRandomizer(LastNameFieldOptions options) : base(options)
        { _generator = new RandomStringFromListGenerator(Rnd, CommonData.Instance.LastNames); }
        /// <summary>生成结果。</summary>
        public string Generate() => Execute(() => _generator.Generate());
    }
}
