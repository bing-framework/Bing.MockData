using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Extensions;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>可指定名姓顺序的模拟邮箱生成器。</summary>
    public class EmailAddressRandomizer : RandomizerBase<EmailAddressFieldOptions>, IStringRandomizer
    {
        private readonly NameParts _names;
        /// <summary>复制配置并验证性别选择。</summary>
        public EmailAddressRandomizer(EmailAddressFieldOptions options) : base(options)
        { _names = new NameParts(Rnd, Options.Male, Options.Female); }
        /// <summary>生成邮箱。</summary>
        public string Generate() => Execute(() =>
        {
            var first = _names.First().ToLowerInvariant();
            var last = _names.Last().ToLowerInvariant();
            var company = LipsumWords.Word(Rnd);
            var domains = CommonData.Instance.TopLevelDomains;
            var domain = domains[Rnd.Next(domains.Count)];
            var local = Options.Left2Right ? first + "." + last : last + "." + first;
            return local + "@" + company + "." + domain;
        });
    }
}
