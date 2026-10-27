#if NET10_0_OR_GREATER
using Bing.MockData;
using Bing.MockData.Geography;
using System.Linq;
using System;
using Bing.MockData.Core.Enums;
using Bing.MockData.Core.Options;
using Bing.MockData.Datas;
using Bing.MockData.Factories;

var englishColors = new DataLoader().GetResourceFromJson<string[]>("Colors", LanguageType.En);
var chineseColors = new DataLoader().GetResourceFromJson<string[]>("Colors", LanguageType.Cn);
if (Array.IndexOf(englishColors, "black") < 0 || Array.IndexOf(chineseColors, "黑色") < 0)
{
    return 1;
}

var text = RandomizerFactory.GetRandomizer(new TextFieldOptions
{
    Min = 1,
    Max = 12,
    Seed = 173
}).Generate();
if (string.IsNullOrEmpty(text) || text.Length > 12)
{
    return 2;
}

var words = RandomizerFactory.GetRandomizer(new TextWordsFieldOptions
{
    Min = 3,
    Max = 8,
    Seed = 173
}).Generate();
var wordCount = words.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
if (wordCount < 3 || wordCount > 8)
{
    return 3;
}

var iban = RandomizerFactory.GetRandomizer(new IBANFieldOptions
{
    CountryCode = "DE",
    Seed = 173
}).Generate();
if (string.IsNullOrEmpty(iban) || Mod97(iban) != 1)
{
    return 4;
}

var number = RandomizerFactory.GetRandomizer<int>(new NumberFieldOptions<int>
{
    Min = 4,
    Max = 9,
    Seed = 173
}).Generate();
if (number < 4 || number > 9)
{
    return 5;
}

using var context = new MockContext(new MockOptions { Seed = 173 });
if (ChinaDivisions.All.Count != 3212 || context.Templates.Person("110000").Build().Generate().Address.ProvinceCode != "110000") return 6;
if (!context.Sequence().Enumerate(20).SequenceEqual(context.Sequence().EnumerateParallel(20, 4))) return 7;
if (context.Dates.Between(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 1)) != new DateOnly(2025, 1, 1)) return 8;
var bankCard = context.Finance.BankCard("308", Bing.MockData.Datas.Models.Banks.CardType.CreditCard);
if (bankCard.Length < 15 || bankCard.Length > 19 || context.Replay.GenerationContractVersion != "domain-v6") return 9;
return 0;

static int Mod97(string iban)
{
    var rearranged = iban.Substring(4) + iban.Substring(0, 4);
    var remainder = 0;
    foreach (var character in rearranged)
    {
        if (character >= '0' && character <= '9')
        {
            remainder = (remainder * 10 + character - '0') % 97;
        }
        else if (character >= 'A' && character <= 'Z')
        {
            remainder = (remainder * 100 + character - 'A' + 10) % 97;
        }
        else
        {
            return -1;
        }
    }

    return remainder;
}

#else
using Bing.MockData;
using Bing.MockData.Geography;
namespace Bing.MockData.PackageSmoke
{
    public static class StandardConsumer
    {
        public static string CompileContract()
        {
            using (var context = new MockContext(new MockOptions { Seed = 173 }))
                return context.Text.Email() + context.Numbers.Decimal(0, 1) + ChinaDivisions.All.Count;
        }
    }
}
#endif
