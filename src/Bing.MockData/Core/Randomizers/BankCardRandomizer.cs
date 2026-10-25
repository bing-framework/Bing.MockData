using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Internals.Generators;
using Bing.MockData.Internals.Utils;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>按内嵌前缀规则生成具有 Luhn 校验位的模拟卡号。</summary>
    public class BankCardRandomizer : RandomizerBase<BankCardFieldOptions>, IBankCardRandomizer
    {
        private readonly RandomGenerator _generator;
        /// <summary>复制生成配置。</summary>
        public BankCardRandomizer(BankCardFieldOptions options) : base(options)
        { _generator = new RandomGenerator(Rnd); }
        /// <summary>生成卡号。</summary>
        public string Generate() => Execute(() =>
        {
            var banks = CommonData.Instance.Banks;
            var rules = banks[Rnd.Next(banks.Count)].AllRules;
            var rule = rules[Rnd.Next(rules.Count)];
            // 内嵌规则 Length 表示前缀之后的长度，包含最终校验位。
            var body = rule.CardPrefix + _generator.GenerateNumber(rule.Length - 1);
            return body + ((10 - LuhnUtil.GetLuhnSum(body.ToCharArray()) % 10) % 10);
        });
    }
}
