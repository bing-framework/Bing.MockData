using System;
using System.Globalization;
using System.Linq;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Datas.Models;
using Bing.MockData.Extensions;
using Fare;

namespace Bing.MockData.Core.Randomizers
{
    /// <summary>生成符合国家格式的模拟账号；IBAN 同时满足 MOD 97 校验。</summary>
    public class IBANRandomizer : RandomizerBase<IBANFieldOptions>, IStringRandomizer
    {
        private readonly IBAN[] _rules;
        private readonly Xeger[] _generators;

        /// <summary>创建生成器。类型和国家代码不区分大小写。</summary>
        public IBANRandomizer(IBANFieldOptions options) : base(options)
        {
            var type = Options.Type?.ToUpperInvariant();
            if (type != "IBAN" && type != "BBAN" && type != "BOTH")
                throw new ArgumentException("类型必须是 IBAN、BBAN 或 BOTH。", nameof(options.Type));
            var rules = type == "BBAN" ? CommonData.Instance.BBANs : CommonData.Instance.IBANs;
            if (type == "BOTH") rules = rules.Concat(CommonData.Instance.BBANs);
            if (!string.IsNullOrEmpty(Options.CountryCode))
                rules = rules.Where(rule => string.Equals(rule.CountryCode, Options.CountryCode, StringComparison.OrdinalIgnoreCase));
            _rules = rules.ToArray();
            if (_rules.Length == 0) throw new ArgumentException("不支持指定国家。", nameof(options.CountryCode));
            _generators = _rules.Select(rule => new Xeger(rule.Pattern, Rnd)).ToArray();
        }

        /// <summary>生成账号；不保证账号真实存在或满足额外的国内银行校验。</summary>
        public string Generate() => Execute(() =>
        {
            var index = Rnd.Next(_rules.Length);
            var value = _generators[index].Generate();
            if (!_rules[index].IsIban) return value;
            var bban = value.Substring(4);
            var country = _rules[index].CountryCode;
            var remainder = 0;
            foreach (var character in bban + country + "00")
                remainder = character >= 'A' && character <= 'Z'
                    ? (remainder * 100 + character - 'A' + 10) % 97
                    : (remainder * 10 + character - '0') % 97;
            return country + (98 - remainder).ToString("D2", CultureInfo.InvariantCulture) + bban;
        });

    }
}
