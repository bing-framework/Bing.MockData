using Bing.MockData.Datas.Models.Banks;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData
{
    public readonly partial struct FinanceData
    {
        /// <summary>按历史银行规则生成模拟卡号，满足来源前缀、长度和 Luhn；不表示真实账户。</summary>
        /// <param name="bankCode">三位 bing-legacy 银行代码；null 表示不限。</param>
        /// <param name="cardType">来源卡类型；null 表示不限，Unknown 不可作为筛选条件。</param>
        /// <returns>具有校验位的数字字符串。</returns>
        public string BankCard(string bankCode = null, CardType? cardType = null)
        {
            _c.Check();
            var token = _c.CancellationToken;
            var selection = BankCardGenerator.Select(bankCode, cardType, _c.Budget.MaxTextLength, token);
            return _c.WithRandom(random => BankCardGenerator.Generate(random, selection, token));
        }
    }
}
