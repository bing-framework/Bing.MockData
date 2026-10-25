using System;
using System.Collections.Generic;
using Bing.MockData;
using Newtonsoft.Json.Linq;

namespace Bing.MockData.Catalogs
{
    /// <summary>Adapts mime-db rows to the core file type generation contract.</summary>
    public sealed class CatalogMimeTypeProvider : IFileTypeProvider, IDisposable
    {
        private readonly IBingMockCatalog _catalog;
        private readonly bool _ownsCatalog;
        private bool _disposed;

        /// <summary>Returns the mounted dataset version used by this provider.</summary>
        public string Version => _catalog.Info.Version;

        public CatalogMimeTypeProvider(IBingMockCatalog catalog, bool ownsCatalog = false)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _ownsCatalog = ownsCatalog;
        }

        public IReadOnlyList<FileTypeDefinition> Find(string extension)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("扩展名不能为空。", nameof(extension));
            var normalized = extension.Trim().TrimStart('.').ToLowerInvariant();
            var result = new List<FileTypeDefinition>();
            var query = new CatalogQuery { Kind = "mime-type", KeyPrefix = "file|" + normalized + "|" };
            foreach (var record in _catalog.Query(query))
            {
                var payload = JObject.Parse(record.PayloadJson);
                result.Add(new FileTypeDefinition((string?)payload["extension"] ?? normalized,
                    (string?)payload["mimeType"] ?? throw new FormatException("MIME 记录缺少 mimeType。")));
            }
            if (result.Count == 0) throw new CatalogCoverageException(_catalog.Info.Id, normalized);
            return result.AsReadOnly();
        }

        public FileTypeDefinition Sample(ulong selector)
        {
            ThrowIfDisposed();
            var query = new CatalogQuery { Kind = "mime-type" };
            var record = _catalog.Sample(selector, query);
            var payload = JObject.Parse(record.PayloadJson);
            return new FileTypeDefinition((string?)payload["extension"] ?? throw new FormatException("MIME 记录缺少 extension。"),
                (string?)payload["mimeType"] ?? throw new FormatException("MIME 记录缺少 mimeType。"));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CatalogMimeTypeProvider));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_ownsCatalog) _catalog.Dispose();
        }
    }
}
