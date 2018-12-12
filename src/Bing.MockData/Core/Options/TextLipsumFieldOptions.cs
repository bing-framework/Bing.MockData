using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 段落字段配置
    /// </summary>
    public class TextLipsumFieldOptions:FieldOptionsBase,IStringFieldOptions
    {
        /// <summary>
        /// 段落
        /// </summary>
        public int Paragraphs { get; set; } = 1;
    }
}
