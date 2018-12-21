using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Bing.MockData.Datas.Models;
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
