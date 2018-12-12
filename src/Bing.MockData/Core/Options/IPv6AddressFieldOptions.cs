using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// IP6地址字段配置
    /// </summary>
    public class IPv6AddressFieldOptions:FieldOptionsBase,IStringFieldOptions
    {
        /// <summary>
        /// 是否大写字符
        /// </summary>
        public bool Uppercase { get; set; }

        /// <summary>
        /// 最小值
        /// </summary>
        public string Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public string Max { get; set; }
    }
}
