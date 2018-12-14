using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// MAC地址配置
    /// </summary>
    public class MACAddressFieldOptions:FieldOptionsBase,IStringFieldOptions
    {
        /// <summary>
        /// 分隔符
        /// </summary>
        public string Separator { get; set; } = ":";

        /// <summary>
        /// 是否大写字符
        /// </summary>
        public bool Uppercase { get; set; } = true;

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
