using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 正则表达式配置
    /// </summary>
    public class TextRegexFieldOptions:FieldOptionsBase,IStringFieldOptions
    {
        /// <summary>
        /// 正则表达式
        /// </summary>
        public string Pattern { get; set; }
    }
}
