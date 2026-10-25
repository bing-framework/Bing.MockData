using System.Collections.Generic;
using System.Threading;

namespace Bing.MockData.Catalogs
{
    /// <summary>Provides bounded, deterministic access to one immutable offline dataset.</summary>
    public interface IBingMockCatalog : System.IDisposable
    {
        DataSetInfo Info { get; }

        bool TryGet(string key, out CatalogRecord? record);

        CatalogRecord GetAt(long ordinal);

        CatalogRecord Sample(ulong selector);

        long Count(CatalogQuery query);

        CatalogRecord Sample(ulong selector, CatalogQuery query);

        IEnumerable<CatalogRecord> Query(CatalogQuery query, CancellationToken cancellationToken = default(CancellationToken));
    }
}
