namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 银行卡号随机生成器
    /// </summary>
    public interface IBankCardRandomizer
    {
        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        string Generate();
    }
}
