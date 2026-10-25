using System;

namespace Bing.MockData.Catalogs
{
    /// <summary>Describes a path-backed catalog to be opened and owned by a catalog set.</summary>
    public sealed class CatalogFile
    {
        private readonly CatalogOpenOptions _options;
        public string Name { get; }
        public string Path { get; }
        internal CatalogOpenOptions Options => Copy(_options);

        public CatalogFile(string name, string path, CatalogOpenOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("目录名称不能为空。", nameof(name));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("目录路径不能为空。", nameof(path));
            Name = name;
            Path = path;
            _options = Copy(options ?? new CatalogOpenOptions());
            _options.Validate();
        }

        private static CatalogOpenOptions Copy(CatalogOpenOptions source) => new CatalogOpenOptions
        {
            MaxFileBytes = source.MaxFileBytes,
            PageCacheKiB = source.PageCacheKiB
        };
    }
}
