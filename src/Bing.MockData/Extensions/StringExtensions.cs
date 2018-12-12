namespace Bing.MockData.Extensions
{
    /// <summary>
    /// 字符串扩展
    /// </summary>
    internal static class StringExtensions
    {
        /// <summary>
        /// 将字符串进行大小写处理
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="toUpper">转换成大写</param>
        /// <returns></returns>
        public static string ToCasedInvariant(this string str, bool toUpper)
        {
            if (str == null)
            {
                return null;
            }

            return toUpper ? str.ToUpperInvariant() : str.ToLowerInvariant();
        }
    }
}
