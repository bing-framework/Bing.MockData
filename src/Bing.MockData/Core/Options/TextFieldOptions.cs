using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 文本配置
    /// </summary>
    public class TextFieldOptions:FieldOptionsBase,IStringFieldOptions
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public int Min { get; set; } = 8;

        /// <summary>
        /// 最大值
        /// </summary>
        public int Max { get; set; } = 8;

        /// <summary>
        /// 使用大写字母
        /// </summary>
        public bool UseUppercase { get; set; } = true;

        /// <summary>
        /// 使用小写字母
        /// </summary>
        public bool UseLowercase { get; set; } = true;

        /// <summary>
        /// 使用字母
        /// </summary>
        public bool UseLetter { get; set; } = true;

        /// <summary>
        /// 使用数字
        /// </summary>
        public bool UseNumber { get; set; } = true;

        /// <summary>
        /// 使用空格
        /// </summary>
        public bool UseSpace { get; set; } = true;

        /// <summary>
        /// 使用特殊符号
        /// </summary>
        public bool UseSpecial { get; set; } = true;
    }
}
