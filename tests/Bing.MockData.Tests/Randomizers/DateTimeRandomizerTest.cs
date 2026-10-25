using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class DateTimeRandomizerTest:TestBase
    {
        public DateTimeRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            var from = new System.DateTime(2030, 1, 2, 0, 0, 0, System.DateTimeKind.Utc);
            var to = new System.DateTime(2030, 1, 10, 0, 0, 0, System.DateTimeKind.Utc);
            AssertSeeded<DateTimeFieldOptions, System.DateTime>(
                new DateTimeFieldOptions { From = from, To = to, IncludeTime = false },
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value =>
                {
                    Assert.InRange(value, from, to);
                    Assert.Equal(System.TimeSpan.Zero, value.TimeOfDay);
                });
        }
    }
}
