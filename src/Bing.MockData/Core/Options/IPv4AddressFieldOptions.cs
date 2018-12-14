using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// IP4地址配置
    /// </summary>
    public class IPv4AddressFieldOptions:FieldOptionsBase,IStringFieldOptions
    {
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
