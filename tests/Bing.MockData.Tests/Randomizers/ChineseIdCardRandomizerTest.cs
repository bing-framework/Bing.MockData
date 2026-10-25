using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class ChineseIdCardRandomizerTest:TestBase
    {
        public ChineseIdCardRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<ChineseIdCardFieldOptions, (string, string, string)>(
                new ChineseIdCardFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return () => (randomizer.Generate(), randomizer.GenerateValidPeriod(), randomizer.GenerateIssueOrg());
                },
                value =>
                {
                    Assert.Matches(@"^[0-9]{17}[0-9X]$", value.Item1);
                    Assert.False(string.IsNullOrWhiteSpace(value.Item2));
                    Assert.False(string.IsNullOrWhiteSpace(value.Item3));
                });
        }
    }
}
