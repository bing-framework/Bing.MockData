using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Bing.MockData.Core.Enums;
using Newtonsoft.Json;

namespace Bing.MockData.Datas
{
    /// <summary>
    /// 数据加载器
    /// </summary>
    public class DataLoader
    {
        /// <summary>
        /// 从指定资源名称中获取流
        /// </summary>
        /// <param name="resourceName">资源名称</param>
        /// <param name="suffix">后缀名</param>
        /// <param name="language">语言</param>
        /// <returns></returns>
        public static Stream GetResourceAsStream(string resourceName, string suffix, LanguageType? language = null)
        {
            return typeof(CommonData).GetTypeInfo().Assembly
                .GetManifestResourceStream(
                    $"Bing.MockData.Datas.Resources.{resourceName}{GetLanguagePath(language)}.{suffix}");
        }

        /// <summary>
        /// 获取语言路径
        /// </summary>
        /// <param name="language">语言</param>
        /// <returns></returns>
        private static string GetLanguagePath(LanguageType? language)
        {
            if (language == null || language == LanguageType.None)
            {
                return string.Empty;
            }

            switch (language)
            {
                case LanguageType.Cn:
                    return ".zh-cn";
                case LanguageType.En:
                    return ".en";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 从指定资源中获取字符串
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <param name="suffix">后缀名</param>
        /// <param name="language">语言</param>
        /// <returns></returns>
        public static string GetResourceAsString(string fileName, string suffix, LanguageType? language = null)
        {
            using (var stream = GetResourceAsStream(fileName, suffix, language))
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// 从指定资源中获取字符串行列表
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <param name="language">语言</param>
        /// <returns></returns>
        public static IEnumerable<string> GetResourceAsLines(string fileName, LanguageType? language = null)
        {
            using (var stream = GetResourceAsStream(fileName, "txt", language))
            {
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        yield return line;
                    }
                }
            }
        }

        /// <summary>
        /// 从指定资源中获取泛型列表
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="fileName">文件名称</param>
        /// <param name="convert">转换方式</param>
        /// <param name="language">语言</param>
        /// <returns></returns>
        public IEnumerable<T> GetResourceAsItems<T>(string fileName, Func<string[], T> convert,
            LanguageType? language = null)
        {
            var lines = GetResourceAsLines(fileName, language);
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
        /// <param name="language">语言</param>
        /// <returns></returns>
        public T GetResourceFromJson<T>(string fileName, LanguageType? language = null)
        {
            var json = GetResourceAsString(fileName, "json");
            if (string.IsNullOrWhiteSpace(json))
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
