using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Bing.MockData.Datas.Models;
using Fare;

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
        public IEnumerable<string> LastNames { get; }

        /// <summary>
        /// 男性名称
        /// </summary>
        public IEnumerable<string> MaleNames { get; }

        /// <summary>
        /// 女性名称
        /// </summary>
        public IEnumerable<string> FemaleNames { get; }

        /// <summary>
        /// 城市名称
        /// </summary>
        public IEnumerable<string> CityNames { get; }

        /// <summary>
        /// 国家名称
        /// </summary>
        public IEnumerable<string> CountryNames { get; }

        /// <summary>
        /// 方向
        /// </summary>
        public IEnumerable<string> Directions { get; }

        /// <summary>
        /// 街道类型
        /// </summary>
        public IEnumerable<string> StreetType { get; }

        /// <summary>
        /// 顶级域名
        /// </summary>
        public IEnumerable<string> TopLevelDomains { get; }

        /// <summary>
        /// 国际银行帐户号码
        /// </summary>
        public IEnumerable<IBAN> IBANs { get; }

        /// <summary>
        /// 基本银行帐户号码
        /// </summary>
        public IEnumerable<IBAN> BBANs { get; }

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
        /// <returns></returns>
        private Stream GetResourceAsStream(string resourceName)
        {
            return typeof(ListData).GetTypeInfo().Assembly
                .GetManifestResourceStream($"Bing.MockData.Datas.Resources.{resourceName}.txt");
        }

        /// <summary>
        /// 从指定资源中获取行列表
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <returns></returns>
        private IEnumerable<string> GetResourceAsLines(string fileName)
        {
            var stream = GetResourceAsStream(fileName);
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
    }
}
