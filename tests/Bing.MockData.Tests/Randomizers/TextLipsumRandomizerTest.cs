using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class TextLipsumRandomizerTest:TestBase
    {
        public TextLipsumRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<TextLipsumFieldOptions, string>(
                new TextLipsumFieldOptions { Paragraphs = 1 },
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.False(string.IsNullOrWhiteSpace(value)));
        }
    }
}
