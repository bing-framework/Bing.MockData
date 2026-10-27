using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>按内嵌前缀规则生成具有 Luhn 校验位的模拟卡号。</summary>
    public class BankCardRandomizer : RandomizerBase<BankCardFieldOptions>, IBankCardRandomizer
    {
        /// <summary>复制生成配置。</summary>
        public BankCardRandomizer(BankCardFieldOptions options) : base(options)
        { }
        /// <summary>生成卡号。</summary>
        public string Generate() => Execute(() =>
        {
            var selection = BankCardGenerator.Select(null, null, Options.MaxTextLength, Options.CancellationToken);
            return BankCardGenerator.Generate(Rnd, selection, Options.CancellationToken);
        });
    }
}
