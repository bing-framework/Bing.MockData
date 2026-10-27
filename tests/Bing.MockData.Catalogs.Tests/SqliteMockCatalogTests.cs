using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Bing.MockData;
using Bing.MockData.Catalogs;
using Bing.MockData.Catalogs.Internal;
using Xunit;

namespace Bing.MockData.Catalogs.Tests;

public sealed class SqliteMockCatalogTests
{
    [Fact]
    public void OpensReadOnlyQueriesByStableKeyAndSamplesFromCallerSelector()
    {
        var path = CreateCatalog();
        using (var catalog = SqliteMockCatalog.Open(path))
        {
            Assert.Equal("mime-test", catalog.Info.Id);
            Assert.Equal(5, catalog.Info.RecordCount);
            Assert.True(catalog.TryGet("file|txt|text/plain", out var txt));
            Assert.Equal("text/plain", txt!.Name);
            Assert.False(catalog.TryGet("missing", out _));

            var keys = catalog.Query(new CatalogQuery { KeyPrefix = "file|", PageSize = 1 }, TestContext.Current.CancellationToken)
                .Select(x => x.Key).ToArray();
            Assert.Equal(new[] { "file|json|application/json", "file|txt|text/plain", "file|xml|application/xml" }, keys);
            Assert.Equal(catalog.GetAt((long)(37UL % 5)).Key, catalog.Sample(37UL).Key);
            Assert.Equal("file|json|application/json", catalog.Query(new CatalogQuery { NamePrefix = "application/" }, TestContext.Current.CancellationToken).First().Key);
            var mimeQuery = new CatalogQuery { Kind = "mime-type", KeyPrefix = "file|" };
            Assert.Equal(3, catalog.Count(mimeQuery));
            Assert.Equal("file|xml|application/xml", catalog.Sample(5, mimeQuery).Key);
            Assert.Equal(1, catalog.Count(new CatalogQuery { CountryCode = "CN" }));
            var coverage = Assert.Throws<CatalogCoverageException>(() => catalog.Sample(0, new CatalogQuery { Kind = "missing" }));
            Assert.Equal("mime-test", coverage.DataSetId);
            Assert.Equal("Kind=missing", coverage.RequestedValue);
        }

        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM Records;";
            Assert.Equal(5L, command.ExecuteScalar());
        }
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void QuerySnapshotsFiltersAndHonorsCancellation()
    {
        var path = CreateCatalog();
        using var catalog = SqliteMockCatalog.Open(path);
        var query = new CatalogQuery { Kind = "mime-type", KeyPrefix = "file|", PageSize = 2 };
        var records = catalog.Query(query, TestContext.Current.CancellationToken).GetEnumerator();
        query.KeyPrefix = "does-not-match";
        Assert.True(records.MoveNext());
        records.Dispose();

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => catalog.Query(new CatalogQuery(), canceled.Token).GetEnumerator().MoveNext());
    }

    [Fact]
    public void DisposeIsIdempotentAndAllOperationsFailAfterRelease()
    {
        var path = CreateCatalog();
        var catalog = SqliteMockCatalog.Open(path);
        catalog.Dispose();
        catalog.Dispose();
        Assert.Throws<ObjectDisposedException>(() => catalog.TryGet("file|txt|text/plain", out _));
        Assert.Throws<ObjectDisposedException>(() => catalog.GetAt(0));
        Assert.Throws<ObjectDisposedException>(() => catalog.Query(new CatalogQuery(), TestContext.Current.CancellationToken));
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void RejectsFilesOverConfiguredSizeBeforeOpening()
    {
        var path = CreateCatalog();
        Assert.Throws<InvalidDataException>(() => SqliteMockCatalog.Open(path, new CatalogOpenOptions { MaxFileBytes = 32 }));
    }

    [Fact]
    public void CatalogFileAndPageCacheLimitsCanBeRaisedAboveDefaults()
    {
        var path = CreateCatalog();
        using (var catalog = SqliteMockCatalog.Open(path, new CatalogOpenOptions
        {
            MaxFileBytes = 1024L * 1024 * 1024,
            PageCacheKiB = 16 * 1024
        }))
            Assert.Equal(5, catalog.Info.RecordCount);
        File.Delete(path);
    }

    [Fact]
    public void CatalogSetReleasesOnlyOwnedInstancesAndEnforcesFileLimit()
    {
        var borrowed = new FakeCatalog("borrowed");
        var owned = new FakeCatalog("owned");
        var set = new CatalogSet(new[]
        {
            new CatalogAttachment("borrowed", borrowed),
            new CatalogAttachment("owned", owned, CatalogOwnership.Owned)
        });
        set.Dispose();
        set.Dispose();
        Assert.Equal(0, borrowed.DisposeCount);
        Assert.Equal(1, owned.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => set.Get("borrowed"));

        var eight = Enumerable.Range(0, CatalogSet.MaxCatalogFiles)
            .Select(i => new CatalogAttachment("catalog-" + i, new FakeCatalog("data-" + i))).ToArray();
        Assert.Equal(CatalogSet.MaxCatalogFiles, new CatalogSet(eight).Catalogs.Count);
        Assert.Throws<InvalidOperationException>(() => new CatalogSet(eight.Concat(new[]
        {
            new CatalogAttachment("ninth", new FakeCatalog("data-9"))
        })));
        using (var configurable = new CatalogSet(eight.Concat(new[]
        {
            new CatalogAttachment("ninth", new FakeCatalog("data-9"))
        }), maxCatalogFiles: 9))
            Assert.Equal(9, configurable.MaximumCatalogFiles);
    }

    [Fact]
    public void ContextMountsMultipleFilesIntoOneVersionedSetAndOwnsOpenedConnections()
    {
        var path = CreateCatalog();
        var options = new MockOptions { Seed = 17 }
            .UseCatalogFile("first", path)
            .UseCatalogFile("second", path);
        using (var first = new MockContext(options))
        using (var second = new MockContext(options))
        {
            Assert.Equal(2, first.Catalogs().Catalogs.Count);
            Assert.Equal("mime-test", first.Catalog("first").Info.Id);
            Assert.Equal("mime-test", first.Catalog("second").Info.Id);
            Assert.Equal(first.Catalogs().Version, first.ExtensionVersions["catalogs"]);
            Assert.NotSame(first.Catalogs(), second.Catalogs());
        }
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DirectCatalogMountHonorsBorrowedAndTransferredOwnership()
    {
        var path = CreateCatalog();
        var borrowed = SqliteMockCatalog.Open(path);
        using (var context = new MockContext(new MockOptions().UseCatalog("borrowed", borrowed)))
            Assert.Equal(5, context.Catalog("borrowed").Info.RecordCount);
        Assert.Equal(5, borrowed.Count(new CatalogQuery()));
        borrowed.Dispose();

        var owned = SqliteMockCatalog.Open(path);
        using (var context = new MockContext(new MockOptions().UseCatalog("owned", owned, ownsResource: true)))
            Assert.Equal(5, context.Catalog("owned").Info.RecordCount);
        Assert.Throws<ObjectDisposedException>(() => owned.GetAt(0));
        File.Delete(path);
    }

    [Fact]
    public void MimeProviderIsCreatedPerContextAndItsCatalogCountsTowardTheSharedLimit()
    {
        var path = CreateCatalog();
        var catalog = SqliteMockCatalog.Open(path);
        var options = new MockOptions().UseMimeTypeCatalog(catalog);
        var first = new MockContext(options);
        var second = new MockContext(options);
        var firstProvider = first.Extension<CatalogMimeTypeProvider>("mime-types");
        var secondProvider = second.Extension<CatalogMimeTypeProvider>("mime-types");
        Assert.NotSame(firstProvider, secondProvider);
        Assert.Single(first.Catalogs().Catalogs);
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => firstProvider.Find("txt"));
        Assert.Contains(secondProvider.Find("txt"), x => x.MimeType == "text/plain");
        second.Dispose();
        Assert.Equal(5, catalog.Count(new CatalogQuery()));
        catalog.Dispose();

        var capped = new MockOptions();
        for (var i = 0; i < CatalogSet.MaxCatalogFiles; i++) capped.UseCatalogFile("catalog-" + i, path);
        var overLimit = SqliteMockCatalog.Open(path);
        Assert.Throws<InvalidOperationException>(() => capped.UseMimeTypeCatalog(overLimit));
        overLimit.Dispose();
        File.Delete(path);
    }

    [Fact]
    public void CldrSnapshotIncludesHistoricalTargetsAndExplicitlyMarksMissingLocalizedNames()
    {
        var path = FindRepositoryData("cldr.sqlite3");
        using var catalog = SqliteMockCatalog.Open(path);
        Assert.Equal("49.0.0-import-v2", catalog.Info.Version);

        var relations = catalog.Query(new CatalogQuery { Kind = "region-currency", PageSize = 512 },
            TestContext.Current.CancellationToken).ToArray();
        Assert.NotEmpty(relations);
        foreach (var relation in relations)
        {
            using var payload = JsonDocument.Parse(relation.PayloadJson);
            Assert.True(catalog.TryGet("region|" + payload.RootElement.GetProperty("region").GetString(), out _));
            Assert.True(catalog.TryGet("currency|" + payload.RootElement.GetProperty("currencyCode").GetString(), out _));
        }

        Assert.True(catalog.TryGet("currency|ADP", out var historicCurrency));
        using (var payload = JsonDocument.Parse(historicCurrency!.PayloadJson))
        {
            Assert.False(payload.RootElement.GetProperty("isCurrent").GetBoolean());
            Assert.False(payload.RootElement.GetProperty("nameEnMissing").GetBoolean());
        }

        Assert.True(catalog.TryGet("region|BU", out var historicRegion));
        Assert.Equal("BU", historicRegion!.Name);
        using (var payload = JsonDocument.Parse(historicRegion.PayloadJson))
        {
            Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("nameEn").ValueKind);
            Assert.True(payload.RootElement.GetProperty("nameEnMissing").GetBoolean());
            Assert.True(payload.RootElement.GetProperty("nameZhMissing").GetBoolean());
        }
    }

    private static string FindRepositoryData(string filename)
        => Bing.MockData.Tests.Infrastructure.CatalogData.FilePath(filename);

    private static string CreateCatalog()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bing-mockdata-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "catalog.sqlite3");
        using (var writer = new CatalogDatabaseBuilder(path))
        {
            writer.Add("file|txt|text/plain", "mime-type", null, "text/plain", "{\"extension\":\"txt\",\"mimeType\":\"text/plain\"}");
            writer.Add("file|xml|application/xml", "mime-type", null, "application/xml", "{\"extension\":\"xml\",\"mimeType\":\"application/xml\"}");
            writer.Add("file|json|application/json", "mime-type", null, "application/json", "{\"extension\":\"json\",\"mimeType\":\"application/json\"}");
            writer.Add("other|abc|application/abc", "other", null, "application/abc", "{}");
            writer.Add("cn|sample", "sample", "CN", "China", "{}");
            writer.Complete(new DataSetInfo("mime-test", "test-v1", "https://example.invalid/source", "MIT", "2026-01-01", 5,
                new string('a', 64), "test fixture"));
        }
        return path;
    }

    private sealed class FakeCatalog : IBingMockCatalog
    {
        public DataSetInfo Info { get; }
        public int DisposeCount { get; private set; }
        public FakeCatalog(string id) => Info = new DataSetInfo(id, "v1", "https://example.invalid", "CC0-1.0", "2026-01-01", 0, new string('b', 64), "fixture");
        public bool TryGet(string key, out CatalogRecord? record) { record = null; return false; }
        public CatalogRecord GetAt(long ordinal) => throw new ArgumentOutOfRangeException(nameof(ordinal));
        public CatalogRecord Sample(ulong selector) => throw new InvalidOperationException();
        public long Count(CatalogQuery query) => 0;
        public CatalogRecord Sample(ulong selector, CatalogQuery query) => throw new InvalidOperationException();
        public IEnumerable<CatalogRecord> Query(CatalogQuery query, CancellationToken cancellationToken = default) => Array.Empty<CatalogRecord>();
        public void Dispose() => DisposeCount++;
    }
}
