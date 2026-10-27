using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Bing.MockData.Catalogs
{
    /// <summary>Groups a bounded number of catalogs and coordinates their explicit ownership.</summary>
    public sealed class CatalogSet : IDisposable
    {
        public const int MaxCatalogFiles = 8;
        private readonly IReadOnlyDictionary<string, CatalogAttachment> _items;
        private readonly object _gate = new object();
        private bool _disposed;

        public IReadOnlyCollection<IBingMockCatalog> Catalogs { get; }

        public string Version { get; }

        public int MaximumCatalogFiles { get; }

        public CatalogSet(IEnumerable<CatalogAttachment> attachments, int maxCatalogFiles = MaxCatalogFiles)
        {
            if (attachments == null) throw new ArgumentNullException(nameof(attachments));
            if (maxCatalogFiles < 1) throw new ArgumentOutOfRangeException(nameof(maxCatalogFiles), "目录文件上限必须大于零。");
            MaximumCatalogFiles = maxCatalogFiles;
            var items = new Dictionary<string, CatalogAttachment>(StringComparer.Ordinal);
            foreach (var attachment in attachments)
            {
                if (attachment == null) throw new ArgumentException("目录挂载项不能为 null。", nameof(attachments));
                if (items.Count >= MaximumCatalogFiles) throw new InvalidOperationException($"同一上下文最多挂载 {MaximumCatalogFiles} 个目录文件。");
                if (items.ContainsKey(attachment.Name)) throw new ArgumentException($"目录名称重复：{attachment.Name}", nameof(attachments));
                items.Add(attachment.Name, attachment);
            }
            _items = new ReadOnlyDictionary<string, CatalogAttachment>(items);
            Catalogs = Array.AsReadOnly(items.Values.Select(x => x.Catalog).ToArray());
            Version = ComputeVersion(items.Values);
        }

        /// <summary>Opens each path and transfers all opened connections to the resulting set.</summary>
        public static CatalogSet Open(IEnumerable<CatalogFile> files, int maxCatalogFiles = MaxCatalogFiles)
            => Open(Array.Empty<CatalogAttachment>(), files, maxCatalogFiles);

        internal static CatalogSet Open(IEnumerable<CatalogAttachment> borrowedAttachments, IEnumerable<CatalogFile> files,
            int maxCatalogFiles = MaxCatalogFiles)
        {
            if (borrowedAttachments == null) throw new ArgumentNullException(nameof(borrowedAttachments));
            if (files == null) throw new ArgumentNullException(nameof(files));
            if (maxCatalogFiles < 1) throw new ArgumentOutOfRangeException(nameof(maxCatalogFiles), "目录文件上限必须大于零。");
            var attachments = new List<CatalogAttachment>();
            var opened = new List<IBingMockCatalog>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (var attachment in borrowedAttachments)
                {
                    if (attachment == null) throw new ArgumentException("目录挂载项不能为 null。", nameof(borrowedAttachments));
                    if (attachments.Count >= maxCatalogFiles) throw new InvalidOperationException($"同一上下文最多挂载 {maxCatalogFiles} 个目录文件。");
                    if (!names.Add(attachment.Name)) throw new ArgumentException($"目录名称重复：{attachment.Name}", nameof(borrowedAttachments));
                    attachments.Add(attachment);
                }
                foreach (var file in files)
                {
                    if (file == null) throw new ArgumentException("目录文件项不能为 null。", nameof(files));
                    if (attachments.Count >= maxCatalogFiles) throw new InvalidOperationException($"同一上下文最多挂载 {maxCatalogFiles} 个目录文件。");
                    if (!names.Add(file.Name)) throw new ArgumentException($"目录名称重复：{file.Name}", nameof(files));
                    var catalog = SqliteMockCatalog.Open(file.Path, file.Options);
                    opened.Add(catalog);
                    attachments.Add(new CatalogAttachment(file.Name, catalog, CatalogOwnership.Owned));
                }
                return new CatalogSet(attachments, maxCatalogFiles);
            }
            catch
            {
                foreach (var catalog in opened) catalog.Dispose();
                throw;
            }
        }

        public IBingMockCatalog Get(string name)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (!_items.TryGetValue(name, out var attachment)) throw new KeyNotFoundException($"未挂载目录：{name}");
                return attachment.Catalog;
            }
        }

        public bool TryGet(string name, out IBingMockCatalog? catalog)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_items.TryGetValue(name, out var attachment)) { catalog = attachment.Catalog; return true; }
                catalog = null;
                return false;
            }
        }

        private static string ComputeVersion(IEnumerable<CatalogAttachment> items)
        {
            var ordered = items.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                WriteVersionString(writer, "catalog-set-v2");
                writer.Write(ordered.Length);
                foreach (var item in ordered)
                {
                    var info = item.Catalog.Info;
                    WriteVersionString(writer, item.Name);
                    WriteVersionString(writer, info.Id);
                    WriteVersionString(writer, info.Version);
                    WriteVersionString(writer, info.SourceSha256);
                }
                writer.Flush();
                using (var sha = SHA256.Create())
                    return "catalog-set-v2-" + string.Concat(sha.ComputeHash(stream.ToArray()).Select(x => x.ToString("x2")));
            }
        }

        private static void WriteVersionString(BinaryWriter writer, string value)
        {
            writer.Write(value.Length);
            foreach (var codeUnit in value)
                writer.Write((ushort)codeUnit);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CatalogSet));
        }

        public void Dispose()
        {
            List<Exception> errors = null;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                var seen = new HashSet<IBingMockCatalog>(ReferenceComparer.Instance);
                foreach (var attachment in _items.Values)
                {
                    if (attachment.Ownership != CatalogOwnership.Owned || !seen.Add(attachment.Catalog)) continue;
                    try { attachment.Catalog.Dispose(); }
                    catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
                }
            }
            if (errors != null) throw new AggregateException("一个或多个自有目录未能释放。", errors);
        }

        private sealed class ReferenceComparer : IEqualityComparer<IBingMockCatalog>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(IBingMockCatalog? x, IBingMockCatalog? y) => ReferenceEquals(x, y);
            public int GetHashCode(IBingMockCatalog obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
