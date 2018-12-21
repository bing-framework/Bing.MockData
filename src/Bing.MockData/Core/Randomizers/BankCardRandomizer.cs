using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Datas.Models.Banks;
using Bing.MockData.Internals.Generators;
using Bing.MockData.Internals.Utils;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>
    /// 银行卡号随机生成器
    /// </summary>
    public class BankCardRandomizer:RandomizerBase<BankCardFieldOptions>,IBankCardRandomizer
    {
        /// <summary>
        /// 生成器
        /// </summary>
        private readonly RandomGenerator _generator;

        /// <summary>
        /// 初始化一个<see cref="BankCardRandomizer"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        public BankCardRandomizer(BankCardFieldOptions options) : base(options)
        {
            _generator = new RandomGenerator();
        }

        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        public string Generate()
        {
            var bankInfo = GetBankInfo();
            var cardRule = GetCardRule(bankInfo);
            var cardNum = cardRule.CardPrefix + _generator.GenerateNumber(cardRule.Length - 1);
            return $"{cardNum}{GetCheckCode(cardNum)}";
        }

        /// <summary>
        /// 获取银行信息
        /// </summary>
        /// <returns></returns>
        private BankInfo GetBankInfo()
        {
            var length = ListData.Instance.Banks.Count;
            var bankInfo = ListData.Instance.Banks[_generator.GenerateInt(0, length)];
            return bankInfo;
        }

        /// <summary>
        /// 获取银行卡规则
        /// </summary>
        /// <param name="bankInfo">银行信息</param>
        /// <returns></returns>
        private CardRuleItem GetCardRule(BankInfo bankInfo)
        {
            var length = bankInfo.AllRules.Count;
            var cardRule = bankInfo.AllRules[_generator.GenerateInt(0, length)];
            return cardRule;
        }

        /// <summary>
        /// 获取银行卡校验码
        /// </summary>
        /// <param name="cardNo">卡号</param>
        /// <returns></returns>
        private string GetCheckCode(string cardNo)
        {
            int luhnSum = LuhnUtil.GetLuhnSum(cardNo.Trim().ToCharArray());
            char checkCode = luhnSum % 10 == 0 ? '0' : (char)(10 - luhnSum % 10 + '0');
            return checkCode.ToString();
        }
    }
}
