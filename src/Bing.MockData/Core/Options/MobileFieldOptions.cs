using Bing.MockData.Abstractions.Options;

namespace Bing.MockData.Core.Options
{
    /// <summary>
    /// 手机号码配置
    /// </summary>
    public class MobileFieldOptions : FieldOptionsBase, IMobileFieldOptions
    {
        /// <summary>
        /// 手机号码前缀
        /// </summary>
        public int[] MobilePrefixs { get; set; } = new int[]
        {
            133, 153, 177, 180,
            181, 189, 134, 135, 136, 137, 138, 139, 150, 151, 152, 157, 158, 159,
            178, 182, 183, 184, 187, 188, 130, 131, 132, 155, 156, 176, 185, 186,
            145, 147, 170
        };

        /// <summary>
        /// 是否生成虚假号码
        /// </summary>
        public bool UseFake { get; set; } = false;

        /// <summary>
        /// 虚假号码前缀
        /// </summary>
        public string FakePrefix { get; set; } = "19";

        /// <summary>
        /// 手机号码长度
        /// </summary>
        public int Length { get; set; } = 11;
    }
}
