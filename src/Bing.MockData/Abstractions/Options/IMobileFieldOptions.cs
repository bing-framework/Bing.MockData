namespace Bing.MockData.Abstractions.Options
{
    /// <summary>
    /// 手机号码配置
    /// </summary>
    public interface IMobileFieldOptions
    {
        /// <summary>
        /// 手机号码前缀
        /// </summary>
        int[] MobilePrefixs { get; set; }

        /// <summary>
        /// 是否生成虚假号码
        /// </summary>
        bool UseFake { get; set; } 

        /// <summary>
        /// 虚假号码前缀
        /// </summary>
        string FakePrefix { get; set; }

        /// <summary>
        /// 手机号码长度
        /// </summary>
        int Length { get; set; }
    }
}
