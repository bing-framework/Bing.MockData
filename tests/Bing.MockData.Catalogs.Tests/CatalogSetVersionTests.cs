using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bing.MockData;
using Xunit;

namespace Bing.MockData.Catalogs.Tests;

public sealed class CatalogSetVersionTests
{
    [Fact]
    public void VersionDoesNotCollideWhenFieldsContainTabs()
    {
        using var first = new CatalogSet(new[]
        {
            new CatalogAttachment("a\tb", new FakeCatalog("c"))
        });
        using var second = new CatalogSet(new[]
        {
            new CatalogAttachment("a", new FakeCatalog("b\tc"))
        });

        Assert.NotEqual(first.Version, second.Version);
        Assert.StartsWith("catalog-set-v2-", first.Version);
        Assert.StartsWith("catalog-set-v2-", second.Version);
    }

    [Fact]
    public void VersionIsStableRegardlessOfAttachmentOrder()
    {
        var first = new CatalogAttachment("north", new FakeCatalog("dataset-north"));
        var second = new CatalogAttachment("south", new FakeCatalog("dataset-south"));

        using var forward = new CatalogSet(new[] { first, second });
        using var reversed = new CatalogSet(new[] { second, first });

        Assert.Equal(forward.Version, reversed.Version);
    }

    [Fact]
    public void ReplayAcceptsTheSameCatalogConfigurationAndRejectsAChangedOne()
    {
        ReplayInfo replay;
        using (var original = new MockContext(CreateOptions()))
            replay = original.Replay;

        var sameConfiguration = replay.ToOptions()
            .UseCatalog("second", new FakeCatalog("dataset-second"))
            .UseCatalog("first", new FakeCatalog("dataset-first"));
        using (var replayed = new MockContext(sameConfiguration))
            Assert.Equal(replay.ExtensionVersions["catalogs"], replayed.ExtensionVersions["catalogs"]);

        var changedConfiguration = replay.ToOptions()
            .UseCatalog("second", new FakeCatalog("dataset-second"))
            .UseCatalog("first", new FakeCatalog("dataset-changed"));
        Assert.Throws<InvalidOperationException>(() => new MockContext(changedConfiguration));
    }

    private static MockOptions CreateOptions() => new MockOptions { Seed = 17 }
        .UseCatalog("first", new FakeCatalog("dataset-first"))
        .UseCatalog("second", new FakeCatalog("dataset-second"));

    private sealed class FakeCatalog : IBingMockCatalog
    {
        public DataSetInfo Info { get; }

        public FakeCatalog(string id)
        {
            Info = new DataSetInfo(id, "v1", "https://example.invalid/source", "CC0-1.0", "2026-01-01", 0,
                new string('a', 64), "catalog set version fixture");
        }

        public bool TryGet(string key, out CatalogRecord? record) { record = null; return false; }
        public CatalogRecord GetAt(long ordinal) => throw new ArgumentOutOfRangeException(nameof(ordinal));
        public CatalogRecord Sample(ulong selector) => throw new InvalidOperationException();
        public long Count(CatalogQuery query) => 0;
        public CatalogRecord Sample(ulong selector, CatalogQuery query) => throw new InvalidOperationException();
        public IEnumerable<CatalogRecord> Query(CatalogQuery query, CancellationToken cancellationToken = default) => Enumerable.Empty<CatalogRecord>();
        public void Dispose() { }
    }
}
