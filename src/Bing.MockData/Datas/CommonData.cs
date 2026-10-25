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
        public IReadOnlyList<string> TopLevelDomains { get; } = new[] { "com", "net", "org", "us", "gov", "nl" };
        public IEnumerable<IBAN> IBANs => _ibans.Value;
        public IEnumerable<IBAN> BBANs => _bbans.Value;
        public IReadOnlyList<string> ChineseFirstNames { get; } = new string[] {
                "李", "王", "张",
                "刘", "陈", "杨", "黄", "赵", "周", "吴", "徐", "孙", "朱", "马", "胡", "郭", "林",
                "何", "高", "梁", "郑", "罗", "宋", "谢", "唐", "韩", "曹", "许", "邓", "萧", "冯",
                "曾", "程", "蔡", "彭", "潘", "袁", "於", "董", "余", "苏", "叶", "吕", "魏", "蒋",
                "田", "杜", "丁", "沈", "姜", "范", "江", "傅", "钟", "卢", "汪", "戴", "崔", "任",
                "陆", "廖", "姚", "方", "金", "邱", "夏", "谭", "韦", "贾", "邹", "石", "熊", "孟",
                "秦", "阎", "薛", "侯", "雷", "白", "龙", "段", "郝", "孔", "邵", "史", "毛", "常",
                "万", "顾", "赖", "武", "康", "贺", "严", "尹", "钱", "施", "牛", "洪", "龚", "东方",
                "夏侯", "诸葛", "尉迟", "皇甫", "宇文", "鲜于", "西门", "司马", "独孤", "公孙", "慕容", "轩辕",
                "左丘", "欧阳", "上官", "闾丘", "令狐", "太史", "端木", "东方", "南宫", "万俟", "闻人",
                "公羊", "赫连", "澹台", "宗政", "濮阳", "公冶", "太叔", "申屠", "仲孙", "钟离", "长孙", "司徒", "司空",
                "闾丘", "子车", "亓官", "司寇", "巫马", "公西", "颛孙", "壤驷", "公良", "漆雕", "乐正", "宰父", "谷梁", "拓跋", "夹谷", "段干",
                "百里", "呼延", "东郭", "南门", "羊舌", "微生", "公户", "公玉", "公仪", "梁丘", "公仲", "公上", "公门", "公山", "公坚", "公伯",
                "公祖", "第五", "公乘", "贯丘", "公皙", "南荣", "东里", "东宫", "仲长", "子书", "子桑", "即墨", "达奚", "褚师", "吴铭"
            };
        private static IBAN[] LoadIban(string name, bool isIban) => DataLoader.GetResourceAsLines(name).Select(line =>
        {
            var columns = line.Split('\t');
            return new IBAN { CountryName = columns[0], CountryCode = columns[1], Pattern = columns[2], IsIban = isIban };
        }).ToArray();
    }
}
