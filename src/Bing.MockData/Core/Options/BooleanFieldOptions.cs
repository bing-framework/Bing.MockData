namespace Bing.MockData.Core.Options
{
    /// <summary>布尔值闭区间配置，默认同时允许 false 和 true。</summary>
    public class BooleanFieldOptions : NumberFieldOptions<bool>
    {
        /// <summary>最小值。</summary>
        public override bool Min { get; set; }
        /// <summary>最大值。</summary>
        public override bool Max { get; set; } = true;
    }
}
