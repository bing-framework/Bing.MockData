using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Bing.MockData.Core.Enums;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;
using Bing.MockData.Datas;
using Xunit;

namespace Bing.MockData.Tests.Regression
{
    public class ResourceAndFormatTests
    {
        // SWIFT IBAN Registry v101：独立于生产资源的国家长度和字符类别。
        private const string CountryFormats = @"
AL 8!n16!c
AD 4!n4!n12!c
AT 5!n11!n
BH 4!a14!c
BE 3!n7!n2!n
BA 3!n3!n8!n2!n
BG 4!a4!n2!n8!c
HR 7!n10!n
CY 3!n5!n16!c
CZ 4!n6!n10!n
DK 4!n9!n1!n
DO 4!c20!n
EE 2!n14!n
FI 3!n11!n
FR 5!n5!n11!c2!n
GE 2!a16!n
DE 8!n10!n
GI 4!a15!c
GR 3!n4!n16!c
HU 3!n4!n1!n15!n1!n
IS 4!n2!n6!n10!n
IE 4!a6!n8!n
IL 3!n3!n13!n
IT 1!a5!n5!n12!c
KZ 3!n13!c
KW 4!a22!c
LV 4!a13!c
LB 4!n20!c
LI 5!n12!c
LT 5!n11!n
LU 3!n13!c
MK 3!n10!c2!n
MT 4!a5!n18!c
MR 5!n5!n11!n2!n
MU 4!a2!n2!n12!n3!n3!a
MC 5!n5!n11!c2!n
ME 3!n13!n2!n
NL 4!a10!n
NO 4!n6!n1!n
PL 8!n16!n
PT 4!n4!n11!n2!n
RO 4!a16!c
SM 1!a5!n5!n12!c
SA 2!n18!c
RS 3!n13!n2!n
SK 4!n6!n10!n
SI 5!n8!n2!n
ES 4!n4!n1!n1!n10!n
SE 3!n16!n1!n
CH 5!n12!c
TN 2!n3!n13!n2!n
TR 5!n1!n16!c
AE 3!n16!n
GB 4!a6!n8!n
";

        [Fact]
        public void Every_supported_country_matches_registry_format_and_mod97()
        {
            var specs = CountryFormats.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim().Split(' ')).ToArray();
            Assert.Equal(54, specs.Length);
            Assert.Equal(specs.Select(x => x[0]).OrderBy(x => x), CommonData.Instance.IBANs.Select(x => x.CountryCode).OrderBy(x => x));
            foreach (var spec in specs)
            {
                var country = spec[0];
                var bbanPattern = Regex.Replace(spec[1], @"(\d+)!([nac])", match =>
                    (match.Groups[2].Value == "n" ? "[0-9]" : match.Groups[2].Value == "a" ? "[A-Z]" : "[A-Z0-9]") + "{" + match.Groups[1].Value + "}");
                var iban = new IBANRandomizer(new IBANFieldOptions { CountryCode = country.ToLowerInvariant(), Seed = 29 });
                var bban = new IBANRandomizer(new IBANFieldOptions { CountryCode = country, Type = "bban", Seed = 29 });
                var both = new IBANRandomizer(new IBANFieldOptions { CountryCode = country, Type = "BOTH", Seed = 29 });
                for (var i = 0; i < 8; i++)
                {
                    var value = iban.Generate();
                    Assert.Matches("^" + country + "[0-9]{2}" + bbanPattern + "$", value);
                    Assert.Equal(BigInteger.One, IbanRemainder(value));
                    Assert.Matches("^" + bbanPattern + "$", bban.Generate());
                    Assert.Matches("^(?:" + country + "[0-9]{2})?" + bbanPattern + "$", both.Generate());
                }
            }
            Assert.Throws<ArgumentException>(() => new IBANRandomizer(new IBANFieldOptions { CountryCode = "XX" }));
            Assert.Throws<ArgumentException>(() => new IBANRandomizer(new IBANFieldOptions { Type = "unknown" }));
        }

        private static BigInteger IbanRemainder(string value)
        {
            var rearranged = value.Substring(4) + value.Substring(0, 4);
            var digits = string.Concat(rearranged.Select(c => char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString(CultureInfo.InvariantCulture)));
            return BigInteger.Parse(digits, CultureInfo.InvariantCulture) % 97;
        }

        [Fact]
        public void Bank_cards_and_id_cards_have_valid_checksums_and_calendar_dates()
        {
            var cards = new BankCardRandomizer(new BankCardFieldOptions { Seed = 67 });
            var ids = new ChineseIdCardRandomizer(new ChineseIdCardFieldOptions { Seed = 67, BeginYear = 2024, EndYear = 2024, ValidYear = 1 });
            var weights = new[] { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
            for (var i = 0; i < 100; i++)
            {
                var card = cards.Generate();
                var sum = card.Reverse().Select((digit, index) => (digit - '0') * (index % 2 == 1 ? 2 : 1)).Sum(number => number > 9 ? number - 9 : number);
                Assert.Equal(0, sum % 10);
                var id = ids.Generate();
                Assert.Matches("^[0-9]{17}[0-9X]$", id);
                Assert.Equal("10X98765432"[id.Take(17).Select((digit, index) => (digit - '0') * weights[index]).Sum() % 11], id[17]);
                _ = DateTime.ParseExact(id.Substring(6, 8), "yyyyMMdd", CultureInfo.InvariantCulture);
                var period = ids.GenerateValidPeriod().Split('-');
                var start = DateTime.ParseExact(period[0], "yyyyMMdd", CultureInfo.InvariantCulture);
                var end = DateTime.ParseExact(period[1], "yyyyMMdd", CultureInfo.InvariantCulture);
                Assert.Equal(2024, start.Year);
                Assert.Equal(start.AddYears(1), end);
            }
        }

        [Fact]
        public void Auxiliary_chinese_generation_methods_are_seeded_and_non_null()
        {
            var first = new ChineseIdCardRandomizer(new ChineseIdCardFieldOptions { Seed = 9 });
            var second = new ChineseIdCardRandomizer(new ChineseIdCardFieldOptions { Seed = 9 });
            for (var i = 0; i < 100; i++)
            {
                var period = first.GenerateValidPeriod();
                var org = first.GenerateIssueOrg();
                Assert.False(string.IsNullOrWhiteSpace(period));
                Assert.False(string.IsNullOrWhiteSpace(org));
                Assert.Equal(period, second.GenerateValidPeriod());
                Assert.Equal(org, second.GenerateIssueOrg());
            }
            var firstRegions = new ChineseAddressRandomizer(new ChineseAddressFieldOptions { Seed = 9 });
            var secondRegions = new ChineseAddressRandomizer(new ChineseAddressFieldOptions { Seed = 9 });
            for (var i = 0; i < 100; i++)
            {
                var region = firstRegions.GenerateRegion();
                Assert.False(string.IsNullOrWhiteSpace(region));
                Assert.Equal(region, secondRegions.GenerateRegion());
            }
        }

        [Fact]
        public void Loader_honors_language_and_reports_missing_resources()
        {
            var loader = new DataLoader();
            var chinese = loader.GetResourceFromJson<string[]>("Colors", LanguageType.Cn);
            var english = loader.GetResourceFromJson<string[]>("Colors", LanguageType.En);
            Assert.Contains("黑色", chinese);
            Assert.Contains("black", english);
            Assert.All(loader.GetResourceFromJson<string[]>("Jobs", LanguageType.En), job => Assert.Matches("^[A-Za-z ]+$", job));
            var missing = Assert.Throws<FileNotFoundException>(() => DataLoader.GetResourceAsString("does-not-exist", "txt"));
            Assert.Contains("does-not-exist.txt", missing.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => DataLoader.GetResourceAsString("Colors", "json", (LanguageType)99));
            Assert.Throws<ArgumentException>(() => DataLoader.GetResourceAsStream("", "txt"));
            Assert.Throws<ArgumentNullException>(() => loader.GetResourceAsItems<string>("IBAN", null));
            var lines = DataLoader.GetResourceAsLines("IBAN");
            Assert.Equal(lines, lines.ToArray());
        }

        [Fact]
        public void Paragraphs_have_documented_sentence_and_word_counts()
        {
            Assert.Equal("", new TextLipsumRandomizer(new TextLipsumFieldOptions { Paragraphs = 0 }).Generate());
            Assert.Equal("", new TextWordsRandomizer(new TextWordsFieldOptions { Min = 0, Max = 0 }).Generate());
            var text = new TextLipsumRandomizer(new TextLipsumFieldOptions { Seed = 21, Paragraphs = 3 }).Generate();
            var paragraphs = text.Split("\n\n");
            Assert.Equal(3, paragraphs.Length);
            foreach (var paragraph in paragraphs)
            {
                Assert.EndsWith(".", paragraph);
                var sentences = paragraph.Split('.', StringSplitOptions.RemoveEmptyEntries);
                Assert.InRange(sentences.Length, 3, 7);
                foreach (var sentence in sentences)
                {
                    Assert.Matches("^[A-Z][a-z]*( [a-z]+){3,11}$", sentence.Trim());
                }
            }
        }

        [Fact]
        public void Malformed_options_fail_during_construction()
        {
            Assert.Throws<ArgumentException>(() => new StringListRandomizer(new StringListFieldOptions { Values = null }));
            Assert.Throws<ArgumentException>(() => new StringListRandomizer(new StringListFieldOptions { Values = new List<string>() }));
            Assert.Throws<ArgumentException>(() => new StringListRandomizer(new StringListFieldOptions { Values = new List<string> { null } }));
            Assert.Throws<ArgumentException>(() => new FirstNameRandomizer(new FirstNameFieldOptions { Male = false, Female = false }));
            Assert.Throws<ArgumentException>(() => new FullNameRandomizer(new FullNameFieldOptions { Male = false, Female = false }));
            Assert.Throws<ArgumentException>(() => new EmailAddressRandomizer(new EmailAddressFieldOptions { Male = false, Female = false }));
            Assert.Throws<ArgumentException>(() => new MobileRandomizer(new MobileFieldOptions { MobilePrefixs = Array.Empty<int>() }));
            Assert.Throws<ArgumentException>(() => new MobileRandomizer(new MobileFieldOptions { UseFake = true, FakePrefix = "a" }));
            Assert.Throws<ArgumentException>(() => new MobileRandomizer(new MobileFieldOptions { Length = 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChineseNameRandomizer(new ChineseNameFieldOptions { Length = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChineseIdCardRandomizer(new ChineseIdCardFieldOptions { BeginYear = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChineseIdCardRandomizer(new ChineseIdCardFieldOptions { BeginYear = 2001, EndYear = 2000 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChineseIdCardRandomizer(new ChineseIdCardFieldOptions { EndYear = 9999 }));
            Assert.Throws<ArgumentException>(() => new TextRegexRandomizer(new TextRegexFieldOptions { Pattern = "" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TextLipsumRandomizer(new TextLipsumFieldOptions { Paragraphs = -1 }));
        }
    }
}
