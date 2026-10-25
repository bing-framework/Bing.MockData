using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class FirstNameRandomizerTest:TestBase
    {
        public FirstNameRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<FirstNameFieldOptions, string>(
                new FirstNameFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.False(string.IsNullOrWhiteSpace(value)));
        }
    }
}
