namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// byte 字段配置
    /// </summary>
    public class ByteFieldOptions : NumberFieldOptions<byte>
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public override byte Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public override byte Max { get; set; } = byte.MaxValue;
    }
}
