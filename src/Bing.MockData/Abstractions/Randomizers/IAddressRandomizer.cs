namespace Bing.MockData.Abstractions.Randomizers
{
    /// <summary>
    /// 地址随机生成器
    /// </summary>
    public interface IAddressRandomizer
    {
        /// <summary>
        /// 生成
        /// </summary>
        /// <returns></returns>
        string Generate();

        /// <summary>
        /// 生成大区
        /// </summary>
        /// <returns></returns>
        string GenerateRegion();
    }
}
