using Fare;

namespace Bing.MockData.Datas.Models
{
    /// <summary>
    /// 国际银行帐户号码
    /// </summary>
    public class IBAN
    {
        /// <summary>
        /// 国家名称
        /// </summary>
        public string CountryName { get; set; }

        /// <summary>
        /// 国家编码
        /// </summary>
        public string CountryCode { get; set; }

        /// <summary>
        /// 生成器
        /// </summary>
        public Xeger Generator { get; set; }
    }
}
