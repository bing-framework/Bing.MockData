using System.Collections.Generic;
using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class TextRegexRandomizerTest:TestBase
    {
        public TextRegexRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<TextRegexFieldOptions, string>(
                new TextRegexFieldOptions { Pattern = @"^[0-9]{4}[A-Z]{2}" },
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.Matches(@"^[0-9]{4}[A-Z]{2}", value));
        }
    }
}
