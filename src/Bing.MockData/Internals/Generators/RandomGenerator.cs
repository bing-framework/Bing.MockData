using System;
using Bing.MockData.Datas;
namespace Bing.MockData.Internals.Generators
{
    internal sealed class RandomGenerator
    {
        private readonly Random _random;
        public RandomGenerator(Random random) { _random = random; }
        public string GenerateNumber(int length, System.Threading.CancellationToken cancellationToken = default) => GenerateText(length, "0123456789", cancellationToken);
        // 内部索引重载的上界不包含；公开闭区间使用 NextInt。
        public int GenerateInt(int min, int exclusiveMax) => _random.Next(min, exclusiveMax);
        public int GenerateInt(int inclusiveMax) => RandomValueGenerator.NextInt(_random, 0, inclusiveMax);
        public string GenerateText(int length, string text, System.Threading.CancellationToken cancellationToken = default)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("字符集不能为空。", nameof(text));
            var chars = new char[length];
            for (var i = 0; i < length; i++) { cancellationToken.ThrowIfCancellationRequested(); chars[i] = text[_random.Next(text.Length)]; }
            return new string(chars);
        }
        public string GenerateRandomLengthChinese(int maxLength) => GenerateRandomLengthChinese(1, maxLength);
        public string GenerateRandomLengthChinese(int minLength, int maxLength) =>
            GenerateText(RandomValueGenerator.NextInt(_random, minLength, maxLength), CommonData.Instance.SimplifiedChinese);
        public DateTime GenerateDate(int beginYear = 1970, int endYear = 2080)
        {
            var year = RandomValueGenerator.NextInt(_random, beginYear, endYear);
            var month = RandomValueGenerator.NextInt(_random, 1, 12);
            var day = RandomValueGenerator.NextInt(_random, 1, DateTime.DaysInMonth(year, month));
            return new DateTime(year, month, day);
        }
    }
}
