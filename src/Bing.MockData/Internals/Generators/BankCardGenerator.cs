using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.MockData.Datas;
using Bing.MockData.Datas.Models.Banks;
using Bing.MockData.Internals.Utils;

namespace Bing.MockData.Internals.Generators
{
    /// <summary>共享固定规则快照；随机状态和取消状态始终由调用方传入。</summary>
    internal static class BankCardGenerator
    {
        internal const string SourceVersion = "bankcardinfo@2.0.4";
        private static readonly Lazy<RuleSets> Sets = new Lazy<RuleSets>(() => new RuleSets());

        internal sealed class Selection
        {
            internal readonly CardRuleItem[][] Banks;
            internal readonly int MaximumLength;

            internal Selection(IEnumerable<BankInfo> banks, CardType? type)
            {
                Banks = banks.Select(b => b.AllRules.Where(r => !type.HasValue || r.Type == type.Value)
                    .Select(r => new CardRuleItem { CardPrefix = r.CardPrefix, Length = r.Length, Type = r.Type }).ToArray())
                    .Where(r => r.Length != 0).ToArray();
                MaximumLength = Banks.SelectMany(r => r).Select(r => checked(r.CardPrefix.Length + r.Length)).DefaultIfEmpty(0).Max();
            }
        }

        private sealed class RuleSets
        {
            internal readonly Selection[] All;
            internal readonly Dictionary<string, Selection[]> ByBank = new Dictionary<string, Selection[]>(StringComparer.Ordinal);

            internal RuleSets()
            {
                var banks = CommonData.Instance.Banks;
                All = Create(banks);
                foreach (var bank in banks) ByBank.Add(bank.Code, Create(new[] { bank }));
            }

            private static Selection[] Create(IEnumerable<BankInfo> banks) => Enumerable.Range(0, 5)
                .Select(i => new Selection(banks, i == 0 ? (CardType?)null : (CardType)i)).ToArray();
        }

        internal static Selection Select(string bankCode, CardType? cardType, int maxTextLength, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (bankCode != null && (bankCode.Length != 3 || bankCode.Any(c => c < '0' || c > '9')))
                throw new ArgumentException("银行代码必须为三位 ASCII bing-legacy 代码。", nameof(bankCode));
            if (cardType.HasValue && (cardType.Value < CardType.DebitCard || cardType.Value > CardType.PrepaidCard))
                throw new ArgumentOutOfRangeException(nameof(cardType), "需要明确的银行卡类型，或 null 表示不限。");
            Selection[] choices;
            if (bankCode == null) choices = Sets.Value.All;
            else if (!Sets.Value.ByBank.TryGetValue(bankCode, out choices)) throw Coverage(bankCode, cardType);
            var selected = choices[(int)(cardType ?? CardType.Unknown)];
            if (selected.Banks.Length == 0) throw Coverage(bankCode, cardType);
            if (selected.MaximumLength > maxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), maxTextLength);
            return selected;
        }

        private static InvalidOperationException Coverage(string bankCode, CardType? type) => new InvalidOperationException(
            $"银行卡规则没有覆盖：bank={bankCode ?? "*"}, type={type?.ToString() ?? "*"}, data={SourceVersion}/{ReplayInfo.CurrentDataVersion}。");

        internal static string Generate(Random random, Selection selection, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var rules = selection.Banks[random.Next(selection.Banks.Length)];
            return GenerateRule(random, rules[random.Next(rules.Length)], token);
        }

        internal static string GenerateRule(Random random, CardRuleItem rule, CancellationToken token)
        {
            // Length 是前缀后的位数，最后一位专用于 Luhn。
            var body = rule.CardPrefix + new RandomGenerator(random).GenerateNumber(rule.Length - 1, token);
            var value = body + ((10 - LuhnUtil.GetLuhnSum(body.ToCharArray()) % 10) % 10);
            token.ThrowIfCancellationRequested();
            return value;
        }
    }
}
