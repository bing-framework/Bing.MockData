using System;
using System.Collections.Generic;
using System.Linq;
using Bing.MockData.Datas.Models;
using Bing.MockData.Datas.Models.Banks;
namespace Bing.MockData.Datas
{
    // 按资源懒加载固定数据，不共享生成器或任何随机状态。
    internal sealed class CommonData
    {
        public static CommonData Instance { get; } = new CommonData();
        private static Lazy<string[]> Lines(string name) => new Lazy<string[]>(() => DataLoader.GetResourceAsLines(name).ToArray());
        private readonly Lazy<string[]> _lastNames = Lines("LastNames");
        private readonly Lazy<string[]> _maleNames = Lines("MaleNames");
        private readonly Lazy<string[]> _femaleNames = Lines("FemaleNames");
        private readonly Lazy<string[]> _cities = Lines("CityNames");
        private readonly Lazy<string[]> _countries = Lines("CountryNames");
        private readonly Lazy<string[]> _provinceCities = Lines("ProvinceCity");
        private readonly Lazy<string[]> _chineseCities = Lines("ChineseCityNames");
        private readonly Lazy<string[]> _regions = Lines("ChineseRegionList");
        private readonly Lazy<string> _chinese = new Lazy<string>(() => DataLoader.GetResourceAsString("SimplifiedChinese", "txt"));
        private readonly Lazy<List<string>> _emailDomains =
            new Lazy<List<string>>(() => new DataLoader().GetResourceFromJson<List<string>>("Emails"));
        private readonly Lazy<List<KeyValuePair<string, string>>> _areas =
            new Lazy<List<KeyValuePair<string, string>>>(() => new DataLoader().GetResourceFromJson<List<KeyValuePair<string, string>>>("ChineseAreaCode"));
        private readonly Lazy<List<BankInfo>> _banks =
            new Lazy<List<BankInfo>>(() => new DataLoader().GetResourceFromJson<List<BankInfo>>("Banks"));
        private readonly Lazy<IBAN[]> _ibans = new Lazy<IBAN[]>(() => LoadIban("IBAN", true));
        private readonly Lazy<IBAN[]> _bbans = new Lazy<IBAN[]>(() => LoadIban("BBAN", false));
        public IEnumerable<string> LastNames => _lastNames.Value;
        public IEnumerable<string> MaleNames => _maleNames.Value;
        public IEnumerable<string> FemaleNames => _femaleNames.Value;
        public IEnumerable<string> CityNames => _cities.Value;
        public IEnumerable<string> CountryNames => _countries.Value;
        public IReadOnlyList<string> ProvinceCityList => _provinceCities.Value;
        public IReadOnlyList<string> CityNameList => _chineseCities.Value;
        public IReadOnlyList<string> RegionList => _regions.Value;
        public IReadOnlyList<KeyValuePair<string, string>> AreaCodeDict => _areas.Value;
        public IReadOnlyList<BankInfo> Banks => _banks.Value;
        public string SimplifiedChinese => _chinese.Value;
        public IReadOnlyList<string> EmailDomains => _emailDomains.Value;
        public IEnumerable<IBAN> IBANs => _ibans.Value;
        public IEnumerable<IBAN> BBANs => _bbans.Value;
        private readonly Lazy<string[]> _chineseLastNames = Lines("ChineseLastNames");
        public IReadOnlyList<string> ChineseFirstNames => _chineseLastNames.Value;
        private static IBAN[] LoadIban(string name, bool isIban) => DataLoader.GetResourceAsLines(name).Select(line =>
        {
            var columns = line.Split('\t');
            return new IBAN { CountryName = columns[0], CountryCode = columns[1], Pattern = columns[2], IsIban = isIban };
        }).ToArray();
    }
}
