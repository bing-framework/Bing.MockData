using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Bing.MockData.Core.Enums;
using Newtonsoft.Json;
namespace Bing.MockData.Datas
{
    /// <summary>读取本程序集内嵌的固定资源。</summary>
    public class DataLoader
    {
        /// <summary>打开资源流；调用方负责释放。资源缺失时抛出明确异常。</summary>
        public static Stream GetResourceAsStream(string resourceName, string suffix, LanguageType? language = null)
        {
            if (string.IsNullOrWhiteSpace(resourceName)) throw new ArgumentException("资源名不能为空。", nameof(resourceName));
            if (string.IsNullOrWhiteSpace(suffix)) throw new ArgumentException("后缀不能为空。", nameof(suffix));
            var name = $"Bing.MockData.Datas.Resources.{resourceName}{GetLanguagePath(language)}.{suffix}";
            return typeof(DataLoader).GetTypeInfo().Assembly.GetManifestResourceStream(name)
                ?? throw new FileNotFoundException($"找不到内嵌资源 '{name}'。", name);
        }
        private static string GetLanguagePath(LanguageType? language)
        {
            switch (language)
            {
                case null: case LanguageType.None: return "";
                case LanguageType.Cn: return ".zh-cn";
                case LanguageType.En: return ".en";
                default: throw new ArgumentOutOfRangeException(nameof(language));
            }
        }
        /// <summary>读取完整资源文本并释放流。</summary>
        public static string GetResourceAsString(string fileName, string suffix, LanguageType? language = null)
        {
            using (var stream = GetResourceAsStream(fileName, suffix, language))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
        }
        /// <summary>读取文本行；返回前已释放资源流。</summary>
        public static IEnumerable<string> GetResourceAsLines(string fileName, LanguageType? language = null)
        {
            var lines = new List<string>();
            using (var stream = GetResourceAsStream(fileName, "txt", language))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            return lines.AsReadOnly();
        }
        /// <summary>按制表符分列并转换固定资源。</summary>
        public IEnumerable<T> GetResourceAsItems<T>(string fileName, Func<string[], T> convert, LanguageType? language = null)
        {
            if (convert == null) throw new ArgumentNullException(nameof(convert));
            return GetResourceAsLines(fileName, language).Select(line => convert(line.Split('\t'))).ToArray();
        }
        /// <summary>读取指定语言的 JSON 资源，限制嵌套深度。</summary>
        public T GetResourceFromJson<T>(string fileName, LanguageType? language = null)
        {
            var json = GetResourceAsString(fileName, "json", language);
            return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { MaxDepth = 64 });
        }
    }
}
