using System;

namespace Bing.MockData.Catalogs
{
    /// <summary>Names a catalog and states whether a catalog set owns its lifetime.</summary>
    public sealed class CatalogAttachment
    {
        public string Name { get; }
        public IBingMockCatalog Catalog { get; }
        public CatalogOwnership Ownership { get; }

        public CatalogAttachment(string name, IBingMockCatalog catalog, CatalogOwnership ownership = CatalogOwnership.Borrowed)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("目录名称不能为空。", nameof(name));
            Name = name;
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (ownership != CatalogOwnership.Borrowed && ownership != CatalogOwnership.Owned)
                throw new ArgumentOutOfRangeException(nameof(ownership));
            Ownership = ownership;
        }
    }
}
