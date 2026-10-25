using System;
using System.Globalization;
using System.Linq;
using Bing.MockData.Abstractions.Randomizers;
using Bing.MockData.Core.Options;
using Bing.MockData.Internals.Generators;
namespace Bing.MockData.Core.Randomizers
{
    /// <summary>根据前缀与总长度生成模拟手机号。</summary>
    public class MobileRandomizer : RandomizerBase<MobileFieldOptions>, IMobileRandomizer
    {
        private readonly string[] _prefixes;
        private readonly RandomGenerator _generator;
        /// <summary>校验前缀及总长度。</summary>
        public MobileRandomizer(MobileFieldOptions options) : base(options)
        {
            if (Options.Length > Options.MaxTextLength) throw new MockLimitException(nameof(Options.MaxTextLength), Options.MaxTextLength);
            if (Options.Length <= 0) throw new ArgumentOutOfRangeException(nameof(options.Length));
            if (Options.UseFake)
            {
                if (string.IsNullOrEmpty(Options.FakePrefix) || Options.FakePrefix.Any(c => c < '0' || c > '9'))
                    throw new ArgumentException("模拟前缀必须为数字。", nameof(options.FakePrefix));
                _prefixes = new[] { Options.FakePrefix };
            }
            else
            {
                if (Options.MobilePrefixs == null || Options.MobilePrefixs.Length == 0 || Options.MobilePrefixs.Any(p => p < 0))
                    throw new ArgumentException("手机号前缀必须为非空的非负数列表。", nameof(options.MobilePrefixs));
                _prefixes = Options.MobilePrefixs.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToArray();
            }
            if (_prefixes.Any(p => p.Length > Options.Length))
                throw new ArgumentException("总长度不能小于前缀长度。", nameof(options.Length));
            _generator = new RandomGenerator(Rnd);
        }
        /// <summary>生成指定长度的手机号。</summary>
        public string Generate() => Execute(() =>
        {
            var prefix = _prefixes[Rnd.Next(_prefixes.Length)];
            return prefix + _generator.GenerateNumber(Options.Length - prefix.Length, Options.CancellationToken);
        });
    }
}
