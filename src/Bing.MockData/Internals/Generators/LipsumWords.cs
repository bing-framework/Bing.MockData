using System;
using System.Linq;
using System.Text;
using NLipsum.Core;
namespace Bing.MockData.Internals.Generators
{
    internal static class LipsumWords
    {
        // NLipsum 只提供一次性词库；其全局随机 API 不参与生成。
        private static readonly Lazy<string[]> Words = new Lazy<string[]>(() =>
            new LipsumGenerator().PreparedWords.Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.Length > 0 && w.All(c => c >= 'a' && c <= 'z')).ToArray());
        public static string Word(Random random)
        {
            var words = Words.Value;
            return words[random.Next(words.Length)];
        }
        public static string GenerateWords(Random random, int count, int maxLength = 100000, System.Threading.CancellationToken cancellationToken = default)
        {
            if (count == 0) return string.Empty;
            var result = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var word = Word(random);
                if ((long)result.Length + word.Length + (i > 0 ? 1 : 0) > maxLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), maxLength);
                if (i > 0) result.Append(' ');
                result.Append(word);
            }
            return result.ToString();
        }
        public static string Paragraphs(Random random, int count, int maxLength = 100000, System.Threading.CancellationToken cancellationToken = default)
        {
            var result = new StringBuilder();
            for (var p = 0; p < count; p++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (p > 0) result.Append("\n\n");
                var sentences = RandomValueGenerator.NextInt(random, 3, 7);
                for (var s = 0; s < sentences; s++)
                {
                    if (s > 0) result.Append(' ');
                    var sentence = GenerateWords(random, RandomValueGenerator.NextInt(random, 4, 12), maxLength, cancellationToken);
                    if ((long)result.Length + sentence.Length + 1 > maxLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), maxLength);
                    result.Append(char.ToUpperInvariant(sentence[0])).Append(sentence.Substring(1)).Append('.');
                }
            }
            return result.ToString();
        }
    }
}
