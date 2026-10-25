using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>可指定名姓顺序的英文全名生成器。</summary>
    public class FullNameRandomizer : RandomizerBase<FullNameFieldOptions>, IStringRandomizer
    {
        private readonly NameParts _names;
        /// <summary>复制配置并验证性别选择。</summary>
        public FullNameRandomizer(FullNameFieldOptions options) : base(options)
        { _names = new NameParts(Rnd, Options.Male, Options.Female); }
        /// <summary>生成全名；Left2Right 为 true 时名字在前。</summary>
        public string Generate() => Execute(() =>
        {
            var first = _names.First(); var last = _names.Last();
            return Options.Left2Right ? first + " " + last : last + " " + first;
        });
    }
}
