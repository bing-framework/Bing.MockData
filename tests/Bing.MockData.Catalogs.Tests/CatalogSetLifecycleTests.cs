using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.MockData.Catalogs;
using Xunit;

namespace Bing.MockData.Catalogs.Tests;

public sealed class CatalogSetLifecycleTests
{
    private sealed class FakeCatalog : IBingMockCatalog
    {
        public DataSetInfo Info { get; }
        public int DisposeCalls;
        public Exception DisposeError { get; }

        public FakeCatalog(string id, Exception disposeError = null)
        {
            Info = new DataSetInfo(id, "v1", "https://example.invalid", "CC0-1.0", "2026-01-01", 0,
                new string('a', 64), "lifecycle fixture");
            DisposeError = disposeError;
        }

        public bool TryGet(string key, out CatalogRecord record) { record = null; return false; }
        public CatalogRecord GetAt(long ordinal) => throw new ArgumentOutOfRangeException(nameof(ordinal));
        public CatalogRecord Sample(ulong selector) => throw new InvalidOperationException();
        public long Count(CatalogQuery query) => 0;
        public CatalogRecord Sample(ulong selector, CatalogQuery query) => throw new InvalidOperationException();
        public IEnumerable<CatalogRecord> Query(CatalogQuery query, CancellationToken cancellationToken = default) => Enumerable.Empty<CatalogRecord>();

        public void Dispose()
        {
            DisposeCalls++;
            if (DisposeError != null) throw DisposeError;
        }
    }

    [Fact]
    public void Dispose_attempts_each_owned_catalog_aggregates_errors_and_remains_idempotent()
    {
        var disposalError = new InvalidOperationException("catalog cleanup failure");
        var failing = new FakeCatalog("owned-failing", disposalError);
        var survivor = new FakeCatalog("owned-survivor");
        var borrowed = new FakeCatalog("borrowed");
        var set = new CatalogSet(new[]
        {
            new CatalogAttachment("failing", failing, CatalogOwnership.Owned),
            new CatalogAttachment("survivor", survivor, CatalogOwnership.Owned),
            new CatalogAttachment("borrowed", borrowed)
        });

        var error = Assert.Throws<AggregateException>(() => set.Dispose());
        set.Dispose();

        Assert.Contains(disposalError, error.InnerExceptions);
        Assert.Equal(1, failing.DisposeCalls);
        Assert.Equal(1, survivor.DisposeCalls);
        Assert.Equal(0, borrowed.DisposeCalls);
    }
}
