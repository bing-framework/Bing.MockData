using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// Guid字段配置
    /// </summary>
    public class GuidFieldOptions : FieldOptionsBase, IGuidFieldOptions
    {
        /// <summary>
        /// 是否大写字符
        /// </summary>
        public bool Uppercase { get; set; } = true;
    }
}
