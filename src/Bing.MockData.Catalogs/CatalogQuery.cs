using System;
using System.Threading;

namespace Bing.MockData.Catalogs
{
    /// <summary>Defines indexed filters for a stable-key ordered catalog query.</summary>
    public sealed class CatalogQuery
    {
        public string? Kind { get; set; }
        public string? CountryCode { get; set; }
        public string? KeyPrefix { get; set; }
        public string? NamePrefix { get; set; }
        public int PageSize { get; set; } = 256;

        public CancellationToken CancellationToken { get; set; }

        internal void Validate()
        {
            if (PageSize < 1 || PageSize > 2048)
                throw new ArgumentOutOfRangeException(nameof(PageSize), "分页大小必须介于 1 和 2048 之间。");
            ValidateText(Kind, nameof(Kind));
            ValidateText(CountryCode, nameof(CountryCode));
            ValidateText(KeyPrefix, nameof(KeyPrefix));
            ValidateText(NamePrefix, nameof(NamePrefix));
        }

        private static void ValidateText(string? value, string name)
        {
            if (value != null && value.IndexOf('\0') >= 0) throw new ArgumentException("筛选条件不能含 NUL。", name);
        }

        internal CatalogQuery Copy() => new CatalogQuery
        {
            Kind = Kind,
            CountryCode = CountryCode,
            KeyPrefix = KeyPrefix,
            NamePrefix = NamePrefix,
            PageSize = PageSize,
            CancellationToken = CancellationToken
        };
    }
}
