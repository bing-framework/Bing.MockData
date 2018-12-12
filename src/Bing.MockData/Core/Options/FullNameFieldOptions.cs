using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 全名字段配置
    /// </summary>
    public class FullNameFieldOptions:FieldOptionsBase,IStringFieldOptions
    {
        /// <summary>
        /// 是否男性
        /// </summary>
        public bool Male { get; set; } = true;

        /// <summary>
        /// 是否女性
        /// </summary>
        public bool Female { get; set; } = true;

        /// <summary>
        /// 是否左右
        /// </summary>
        public bool Left2Right { get; set; } = true;
    }
}
