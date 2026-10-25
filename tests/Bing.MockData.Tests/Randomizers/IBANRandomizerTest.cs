using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class IBANRandomizerTest:TestBase
    {
        public IBANRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<IBANFieldOptions, string>(
                new IBANFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value =>
                {
                    Assert.Matches(@"^[A-Z]{2}[0-9]{2}[A-Z0-9]+$", value);
                    Assert.Equal(1, Mod97(value));
                });
        }

        private static int Mod97(string iban)
        {
            var rearranged = iban.Substring(4) + iban.Substring(0, 4);
            var remainder = 0;
            foreach (var character in rearranged)
            {
                if (character >= '0' && character <= '9')
                {
                    remainder = (remainder * 10 + character - '0') % 97;
                }
                else
                {
                    var value = character - 'A' + 10;
                    Assert.InRange(value, 10, 35);
                    remainder = (remainder * 100 + value) % 97;
                }
            }
            return remainder;
        }
    }
}
