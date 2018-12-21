using System;
using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Randomizers
{
    public class TimeSpanRandomizerTest:TestBase
    {
        public TimeSpanRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            var randomizer = RandomizerFactory.GetRandomizer(new TimeSpanFieldOptions()
                {From = DateTime.Now.TimeOfDay, To = DateTime.Now.AddDays(20).TimeOfDay});
            for (var i = 0; i < 100; i++)
            {
                var result = randomizer.GenerateAsString();
                Output.WriteLine(result);
            }
        }
    }
}
