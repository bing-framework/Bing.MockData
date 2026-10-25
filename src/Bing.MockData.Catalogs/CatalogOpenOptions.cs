using System;

namespace Bing.MockData.Catalogs
{
    /// <summary>Sets per-file catalog resource limits.</summary>
    public sealed class CatalogOpenOptions
    {
        public long MaxFileBytes { get; set; } = 512L * 1024 * 1024;
        public int PageCacheKiB { get; set; } = 8192;

        internal void Validate()
        {
            if (MaxFileBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxFileBytes), "目录文件上限必须大于零。");
            if (PageCacheKiB < 1)
                throw new ArgumentOutOfRangeException(nameof(PageCacheKiB), "页面缓存配置必须大于零。");
        }
    }
}
