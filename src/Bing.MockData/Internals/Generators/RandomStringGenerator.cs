using System;
using System.Text;
namespace Bing.MockData.Internals.Generators
{
    internal sealed class RandomStringGenerator
    {
        private readonly Random _random;
        private readonly string _characters;
        public RandomStringGenerator(Random random, bool upper, bool lower, bool number, bool special, bool space)
        {
            _random = random;
            var chars = new StringBuilder();
            if (upper) chars.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            if (lower) chars.Append("abcdefghijklmnopqrstuvwxyz");
            if (number) chars.Append("0123456789");
            if (special) chars.Append(",.;:?!/@#$%^&()=+*-_{}[]<>|~");
            if (space) chars.Append(' ');
            _characters = chars.ToString();
        }
        public void Validate(int min, int max)
        {
            if (min < 0) throw new ArgumentOutOfRangeException(nameof(min));
            if (max < min) throw new ArgumentOutOfRangeException(nameof(max));
            if (max > 0 && _characters.Length == 0)
                throw new ArgumentException("非空文本需要至少一个启用的字符集。", "options");
        }
        public string Generate(int min, int max, System.Threading.CancellationToken cancellationToken = default)
        {
            var length = RandomValueGenerator.NextInt(_random, min, max);
            if (length == 0) return string.Empty;
            var characters = new char[length];
            for (var i = 0; i < characters.Length; i++)
            { cancellationToken.ThrowIfCancellationRequested(); characters[i] = _characters[_random.Next(_characters.Length)]; }
            return new string(characters);
        }
    }
}
