using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>从固定资源中抽取 Country。</summary>
    public class CountryRandomizer : RandomizerBase<CountryFieldOptions>, IStringRandomizer
    {
        private readonly RandomStringFromListGenerator _generator;
        /// <summary>复制配置并绑定资源。</summary>
        public CountryRandomizer(CountryFieldOptions options) : base(options)
        { _generator = new RandomStringFromListGenerator(Rnd, CommonData.Instance.CountryNames); }
        /// <summary>生成结果。</summary>
        public string Generate() => Execute(() => _generator.Generate());
    }
}
