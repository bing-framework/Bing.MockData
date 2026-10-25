namespace Bing.MockData.Datas.Models
{
    /// <summary>IBAN/BBAN 格式元数据，不持有随机状态。</summary>
    public class IBAN
    {
        /// <summary>国家名称。</summary>
        public string CountryName { get; set; }
        /// <summary>两位国家代码。</summary>
        public string CountryCode { get; set; }
        /// <summary>生成格式使用的正则模式。</summary>
        public string Pattern { get; set; }
        internal bool IsIban { get; set; }
    }
}
