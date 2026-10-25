namespace Bing.MockData.Geography
{
    /// <summary>中国行政区划记录的层级。</summary>
    public enum ChinaDivisionLevel
    {
        /// <summary>省级行政区。</summary>
        Province = 1,

        /// <summary>地级行政区。</summary>
        Prefecture = 2,

        /// <summary>县级行政区。</summary>
        County = 3
    }

    /// <summary>不可变的中国行政区划记录。</summary>
    public sealed class ChinaDivision
    {
        internal ChinaDivision(string code, string name, ChinaDivisionLevel level, string parentCode)
        {
            Code = code;
            Name = name;
            Level = level;
            ParentCode = parentCode;
        }

        /// <summary>六位行政区划代码。</summary>
        public string Code { get; }

        /// <summary>行政区划名称。</summary>
        public string Name { get; }

        /// <summary>行政区划层级。</summary>
        public ChinaDivisionLevel Level { get; }

        /// <summary>直接父级代码；省级行政区为 <see langword="null"/>。</summary>
        public string ParentCode { get; }
    }
}
