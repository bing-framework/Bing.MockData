using System;
using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

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
            var from = TimeSpan.FromHours(6);
            var to = TimeSpan.FromHours(30);
            AssertSeeded<TimeSpanFieldOptions, TimeSpan>(
                new TimeSpanFieldOptions { From = from, To = to, IncludeMilliseconds = false },
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value =>
                {
                    Assert.InRange(value, from, to);
                    Assert.Equal(0, value.Ticks % TimeSpan.TicksPerSecond);
                });
        }
    }
}
