using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;
using Xunit.Abstractions;

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
            var randomizer = RandomizerFactory.GetRandomizer(new ChineseIdCardFieldOptions());
            for (var i = 0; i < 100; i++)
            {
                var idcard = randomizer.Generate();
                var time = randomizer.GenerateValidPeriod();
                var address = randomizer.GenerateIssueOrg();
                Output.WriteLine($"{idcard}/{time}/{address}");
            }
        }
    }
}
