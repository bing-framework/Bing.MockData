using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bing.MockData;

namespace Bing.MockData.Catalogs
{
    /// <summary>Connects immutable data catalogs to mock context lifetimes.</summary>
    public static class MockCatalogExtensions
    {
        private static readonly ConditionalWeakTable<MockOptions, CatalogMountPlan> MountPlans = new ConditionalWeakTable<MockOptions, CatalogMountPlan>();

        /// <summary>Adds an in-memory or caller-opened catalog to the context catalog set.</summary>
        public static MockOptions UseCatalog(this MockOptions options, string name, IBingMockCatalog catalog,
            bool ownsResource = false, int? maxCatalogFiles = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var attachment = new CatalogAttachment(name, catalog);
            return Add(options, new[] { attachment }, Array.Empty<CatalogFile>(), maxCatalogFiles, catalog, ownsResource);
        }

        /// <summary>Adds a path-backed catalog that is opened separately for each context.</summary>
        public static MockOptions UseCatalogFile(this MockOptions options, string name, string path,
            CatalogOpenOptions? openOptions = null, int? maxCatalogFiles = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            return Add(options, Array.Empty<CatalogAttachment>(), new[] { new CatalogFile(name, path, openOptions) }, maxCatalogFiles);
        }

        /// <summary>Adds several path-backed catalogs in one validated operation.</summary>
        public static MockOptions UseCatalogFiles(this MockOptions options, IEnumerable<CatalogFile> files, int? maxCatalogFiles = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (files == null) throw new ArgumentNullException(nameof(files));
            return Add(options, Array.Empty<CatalogAttachment>(), SnapshotFiles(files), maxCatalogFiles);
        }

        /// <summary>Registers an already-created catalog set as the catalogs extension.</summary>
        public static MockOptions UseCatalogSet(this MockOptions options, CatalogSet catalogSet, bool ownsResource = false)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (catalogSet == null) throw new ArgumentNullException(nameof(catalogSet));
            options.UseExtension("catalogs", catalogSet, catalogSet.Version, ownsResource);
            return options;
        }

        /// <summary>Finds one mounted catalog by its stable mount name.</summary>
        public static IBingMockCatalog Catalog(this MockContext context, string name)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return context.Extension<CatalogSet>("catalogs").Get(name);
        }

        /// <summary>Returns the immutable set of catalogs mounted in the context.</summary>
        public static CatalogSet Catalogs(this MockContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return context.Extension<CatalogSet>("catalogs");
        }

        /// <summary>Registers a phone metadata provider backed by a catalog.</summary>
        public static MockOptions UsePhoneMetadataCatalog(this MockOptions options, IBingMockCatalog catalog,
            bool ownsCatalog = false, int? maxCatalogFiles = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            options.UseCatalog("phone-metadata", catalog, ownsCatalog, maxCatalogFiles);
            options.UseExtensionFactory<CatalogPhoneMetadataProvider>("phone-metadata",
                () => new CatalogPhoneMetadataProvider(catalog), provider => provider.Version);
            return options;
        }

        /// <summary>Registers a MIME type provider backed by a catalog.</summary>
        public static MockOptions UseMimeTypeCatalog(this MockOptions options, IBingMockCatalog catalog,
            bool ownsCatalog = false, int? maxCatalogFiles = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            options.UseCatalog("mime-types", catalog, ownsCatalog, maxCatalogFiles);
            options.UseExtensionFactory<CatalogMimeTypeProvider>("mime-types",
                () => new CatalogMimeTypeProvider(catalog), provider => provider.Version);
            return options;
        }

        private static MockOptions Add(MockOptions options, IReadOnlyList<CatalogAttachment> attachments,
            IReadOnlyList<CatalogFile> files, int? maximum, IBingMockCatalog? ownedCatalog = null, bool ownsCatalog = false)
        {
            var plan = MountPlans.GetValue(options, _ => new CatalogMountPlan());
            lock (plan.Gate)
            {
                plan.Add(attachments, files, maximum);
                if (!plan.Registered)
                {
                    try
                    {
                        options.UseExtensionFactory("catalogs", plan.Open, catalogSet => catalogSet.Version);
                        plan.Registered = true;
                    }
                    catch
                    {
                        MountPlans.Remove(options);
                        throw;
                    }
                }
                if (ownedCatalog != null && ownsCatalog) options.UseResource(ownedCatalog, ownsResource: true);
            }
            return options;
        }

        private static CatalogFile[] SnapshotFiles(IEnumerable<CatalogFile> files)
        {
            var result = new List<CatalogFile>();
            foreach (var file in files)
            {
                if (file == null) throw new ArgumentException("目录文件项不能为 null。", nameof(files));
                result.Add(file);
            }
            return result.ToArray();
        }

        private sealed class CatalogMountPlan
        {
            internal readonly object Gate = new object();
            private readonly List<CatalogAttachment> _attachments = new List<CatalogAttachment>();
            private readonly List<CatalogFile> _files = new List<CatalogFile>();
            private int _maximum = CatalogSet.MaxCatalogFiles;
            internal bool Registered;

            internal void Add(IReadOnlyList<CatalogAttachment> attachments, IReadOnlyList<CatalogFile> files, int? maximum)
            {
                var candidateAttachments = new List<CatalogAttachment>(_attachments);
                var candidateFiles = new List<CatalogFile>(_files);
                var limit = maximum ?? _maximum;
                if (limit < 1) throw new ArgumentOutOfRangeException(nameof(maximum), "目录文件上限必须大于零。");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var attachment in candidateAttachments) names.Add(attachment.Name);
                foreach (var file in candidateFiles) names.Add(file.Name);
                foreach (var attachment in attachments)
                {
                    if (attachment == null) throw new ArgumentException("目录挂载项不能为 null。", nameof(attachments));
                    if (!names.Add(attachment.Name)) throw new ArgumentException("目录名称重复：" + attachment.Name, nameof(attachments));
                    candidateAttachments.Add(attachment);
                }
                foreach (var file in files)
                {
                    if (file == null) throw new ArgumentException("目录文件项不能为 null。", nameof(files));
                    if (!names.Add(file.Name)) throw new ArgumentException("目录名称重复：" + file.Name, nameof(files));
                    candidateFiles.Add(file);
                }
                if (candidateAttachments.Count + candidateFiles.Count > limit)
                    throw new InvalidOperationException($"同一上下文最多挂载 {limit} 个目录文件。");
                _attachments.Clear();
                _attachments.AddRange(candidateAttachments);
                _files.Clear();
                _files.AddRange(candidateFiles);
                _maximum = limit;
            }

            internal CatalogSet Open()
            {
                CatalogAttachment[] attachments;
                CatalogFile[] files;
                int maximum;
                lock (Gate)
                {
                    attachments = _attachments.ToArray();
                    files = _files.ToArray();
                    maximum = _maximum;
                }
                return CatalogSet.Open(attachments, files, maximum);
            }
        }
    }
}
