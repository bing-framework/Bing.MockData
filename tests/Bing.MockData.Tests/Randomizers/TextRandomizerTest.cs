using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class TextRandomizerTest:TestBase
    {
        public TextRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<TextFieldOptions, string>(
                new TextFieldOptions { Min = 3, Max = 20, UseLetter = true, UseNumber = true },
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.InRange(value.Length, 3, 20));
        }
    }
}
