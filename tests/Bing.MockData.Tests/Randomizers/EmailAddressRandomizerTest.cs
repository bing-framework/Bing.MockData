using Bing.MockData.Core.Options;
using Bing.MockData.Factories;
using Xunit;

namespace Bing.MockData.Tests.Randomizers
{
    public class EmailAddressRandomizerTest:TestBase
    {
        public EmailAddressRandomizerTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Generate()
        {
            AssertSeeded<EmailAddressFieldOptions, string>(
                new EmailAddressFieldOptions(),
                options =>
                {
                    var randomizer = RandomizerFactory.GetRandomizer(options);
                    return randomizer.Generate;
                },
                value => Assert.Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", value));
        }
    }
}
