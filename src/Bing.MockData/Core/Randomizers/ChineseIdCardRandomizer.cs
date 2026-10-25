using System;
using System.Globalization;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>生成带校验位的模拟身份证号码、签发机关和有效期。</summary>
    public class ChineseIdCardRandomizer : RandomizerBase<ChineseIdCardFieldOptions>, IIdCardRandomizer
    {
        private readonly RandomGenerator _generator;
        private static readonly char[] Codes = { '1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2' };
        private static readonly int[] Weights = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
        /// <summary>验证有效期年份配置。</summary>
        public ChineseIdCardRandomizer(ChineseIdCardFieldOptions options) : base(options)
        {
            if (Options.BeginYear < 1 || Options.BeginYear > 9999) throw new ArgumentOutOfRangeException(nameof(options.BeginYear));
            if (Options.EndYear < Options.BeginYear || Options.EndYear > 9999) throw new ArgumentOutOfRangeException(nameof(options.EndYear));
            if (Options.ValidYear <= 0 || Options.ValidYear > 9999 - Options.EndYear)
                throw new ArgumentOutOfRangeException(nameof(options.ValidYear));
            _generator = new RandomGenerator(Rnd);
        }
        /// <summary>生成 18 位号码；地址编码为模拟值。</summary>
        public string Generate() => Execute(() =>
        {
            var areas = CommonData.Instance.AreaCodeDict;
            var area = areas[Rnd.Next(areas.Count)].Key + _generator.GenerateInt(9999).ToString("D4", CultureInfo.InvariantCulture);
            var body = area + _generator.GenerateDate().ToString("yyyyMMdd", CultureInfo.InvariantCulture) + _generator.GenerateNumber(3);
            var sum = 0;
            for (var i = 0; i < Weights.Length; i++) sum += (body[i] - '0') * Weights[i];
            return body + Codes[sum % 11];
        });
        /// <summary>生成模拟签发机关。</summary>
        public string GenerateIssueOrg() => Execute(() => CommonData.Instance.CityNameList[Rnd.Next(CommonData.Instance.CityNameList.Count)] + "公安局某某分局");
        /// <summary>生成闭区间年份内起始的有效期。</summary>
        public string GenerateValidPeriod() => Execute(() =>
        {
            var begin = _generator.GenerateDate(Options.BeginYear, Options.EndYear);
            return begin.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" +
                begin.AddYears(Options.ValidYear).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        });
    }
}
