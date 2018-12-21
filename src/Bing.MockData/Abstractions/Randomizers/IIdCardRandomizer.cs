namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 身份证随机生成器
    /// </summary>
    public interface IIdCardRandomizer
    {
        /// <summary>
        /// 生成身份证号码
        /// </summary>
        /// <returns></returns>
        string Generate();

        /// <summary>
        /// 生成签发机关：XXX公安局/XX区分局
        /// </summary>
        /// <returns></returns>
        string GenerateIssueOrg();

        /// <summary>
        /// 生成有效期限
        /// </summary>
        /// <returns></returns>
        string GenerateValidPeriod();
    }
}
