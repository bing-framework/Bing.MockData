using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>英文名生成器。</summary>
    public class FirstNameRandomizer : RandomizerBase<FirstNameFieldOptions>, IStringRandomizer
    {
        private readonly NameParts _names;
        /// <summary>复制配置并验证性别选择。</summary>
        public FirstNameRandomizer(FirstNameFieldOptions options) : base(options)
        { _names = new NameParts(Rnd, Options.Male, Options.Female); }
        /// <summary>生成名字。</summary>
        public string Generate() => Execute(() => _names.First());
    }
}
