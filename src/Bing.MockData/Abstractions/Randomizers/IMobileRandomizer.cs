namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 手机号码随机生成器
    /// </summary>
    public interface IMobileRandomizer
    {
        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        string Generate();
    }
}
