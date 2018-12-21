using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Bing.MockData.Datas.Models;
using Bing.MockData.Datas.Models.Banks;
using Fare;
using Newtonsoft.Json;

namespace Bing.MockData.Datas
{
    /// <summary>
    /// 列表数据
    /// </summary>
    internal sealed class ListData
    {
        /// <summary>
        /// 姓氏
        /// </summary>
        public IEnumerable<string> LastNames { get; private set; }

        /// <summary>
        /// 男性名称
        /// </summary>
        public IEnumerable<string> MaleNames { get; private set; }

        /// <summary>
        /// 女性名称
        /// </summary>
        public IEnumerable<string> FemaleNames { get; private set; }

        /// <summary>
        /// 城市名称
        /// </summary>
        public IEnumerable<string> CityNames { get; private set; }

        /// <summary>
        /// 国家名称
        /// </summary>
        public IEnumerable<string> CountryNames { get; private set; }

        /// <summary>
        /// 方向
        /// </summary>
        public IEnumerable<string> Directions { get; private set; }

        /// <summary>
        /// 街道类型
        /// </summary>
        public IEnumerable<string> StreetType { get; private set; }

        /// <summary>
        /// 顶级域名
        /// </summary>
        public IEnumerable<string> TopLevelDomains { get; private set; }

        /// <summary>
        /// 国际银行帐户号码
        /// </summary>
        public IEnumerable<IBAN> IBANs { get; private set; }

        /// <summary>
        /// 基本银行帐户号码
        /// </summary>
        public IEnumerable<IBAN> BBANs { get; private set; }

        /// <summary>
        /// 常用简体汉字
        /// </summary>
        public string SimplifiedChinese { get; private set; }

        /// <summary>
        /// 省市 列表
        /// </summary>
        public List<string> ProvinceCityList { get; private set; }

        /// <summary>
        /// 城市 列表
        /// </summary>
        public List<string> CityNameList { get; private set; }

        /// <summary>
        /// 行政代码 列表
        /// </summary>
        public List<KeyValuePair<string,string>> AreaCodeDict { get; private set; }

        /// <summary>
        /// 大区 列表
        /// </summary>
        public List<string> RegionList { get; private set; }

        /// <summary>
        /// 中文姓氏
        /// </summary>
        public List<string> ChineseFirstNames { get; private set; }

        /// <summary>
        /// 银行信息列表
        /// </summary>
        public List<BankInfo> Banks { get; private set; }

        /// <summary>
        /// 初始化一个<see cref="ListData"/>类型的实例
        /// </summary>
        ListData()
        {
            LastNames = GetResourceAsLines("LastNames");
            MaleNames = GetResourceAsLines("MaleNames");
            FemaleNames = GetResourceAsLines("FemaleNames");
            CityNames = GetResourceAsLines("CityNames");
            CountryNames = GetResourceAsLines("CountryNames");
            Directions = new[] {"North", "East", "South", "West"};
            StreetType = new[] {"St.", "Ln.", "Ave.", "Way", "Blvd.", "Ct."};
            TopLevelDomains = new[] {"com", "net", "org", "us", "gov", "nl"};

            Func<string[], IBAN> ibanFunc = (columns) => new IBAN()
            {
                CountryName = columns[0],
                CountryCode = columns[1],
                Generator = new Xeger(columns[2])
            };
            IBANs = GetResourceAsItems("IBAN", ibanFunc);
            BBANs = GetResourceAsItems("BBAN", ibanFunc);
            SimplifiedChinese = GetResourceAsString("SimplifiedChinese", "txt");
            ProvinceCityList = GetResourceAsLines("ProvinceCity").ToList();
            CityNameList = GetResourceAsLines("ChineseCityNames").ToList();
            AreaCodeDict = GetResourceFromJson<List<KeyValuePair<string, string>>>("ChineseAreaCode");
            RegionList = GetResourceAsLines("ChineseRegionList").ToList();
            ChineseFirstNames = new string[]
            {
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
            }.ToList();
            Banks = GetResourceFromJson<List<BankInfo>>("Banks");
        }

        /// <summary>
        /// 实例
        /// </summary>
        public static ListData Instance => Nested.TextInstance;

        /// <summary>
        /// 嵌套类
        /// </summary>
        private class Nested
        {
            /// <summary>
            /// 静态构造函数
            /// </summary>
            static Nested() { }

            /// <summary>
            /// 文本实例
            /// </summary>
            internal static readonly ListData TextInstance = new ListData();
        }

        /// <summary>
        /// 从指定资源名称中获取流
        /// </summary>
        /// <param name="resourceName">资源名称</param>
        /// <param name="suffix">后缀名</param>
        /// <returns></returns>
        private Stream GetResourceAsStream(string resourceName,string suffix)
        {
            return typeof(ListData).GetTypeInfo().Assembly
                .GetManifestResourceStream($"Bing.MockData.Datas.Resources.{resourceName}.{suffix}");
        }

        /// <summary>
        /// 从指定资源中获取字符串
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <param name="suffix">后缀名</param>
        /// <returns></returns>
        private string GetResourceAsString(string fileName,string suffix)
        {
            var stream = GetResourceAsStream(fileName, suffix);
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// 从指定资源中获取行列表
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <returns></returns>
        private IEnumerable<string> GetResourceAsLines(string fileName)
        {
            var stream = GetResourceAsStream(fileName, "txt");
            using (var reader=new StreamReader(stream,Encoding.UTF8))
            {
                string line;
                while ((line=reader.ReadLine())!=null)
                {
                    yield return line;
                }
            }
        }

        /// <summary>
        /// 从指定资源中获取列表项
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="fileName">文件名称</param>
        /// <param name="convert">转换方式</param>
        /// <returns></returns>
        private IEnumerable<T> GetResourceAsItems<T>(string fileName, Func<string[], T> convert)
        {
            var lines = GetResourceAsLines(fileName);
            foreach (var line in lines)
            {
                yield return convert(line.Split('\t'));                
            }
        }

        /// <summary>
        /// 从指定资源中获取Json对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="fileName">文件名称</param>
        /// <returns></returns>
        private T GetResourceFromJson<T>(string fileName)
        {
            var json = GetResourceAsString(fileName, "json");
            if (string.IsNullOrWhiteSpace(json))
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// 加载自定义资源
        /// </summary>
        /// <param name="action">加载配置操作</param>
        public void LoadCustomResource(Action<ListData> action)
        {
            action.Invoke(this);
        }
    }
}
