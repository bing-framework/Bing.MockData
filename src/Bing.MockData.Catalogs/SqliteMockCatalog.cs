using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace Bing.MockData.Catalogs
{
    /// <summary>Reads one versioned catalog from a bounded, read-only SQLite connection.</summary>
    public sealed class SqliteMockCatalog : IBingMockCatalog
    {
        public const int FileFormatVersion = 1;
        private const long SqliteApplicationId = 1112364081;
        private readonly object _gate = new object();
        private readonly SqliteConnection _connection;
        private bool _disposed;

        public DataSetInfo Info { get; }

        private SqliteMockCatalog(SqliteConnection connection, DataSetInfo info)
        {
            _connection = connection;
            Info = info;
        }

        /// <summary>Opens a file and owns its connection until this catalog is disposed.</summary>
        public static SqliteMockCatalog Open(string path, CatalogOpenOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("目录路径不能为空。", nameof(path));
            options = options ?? new CatalogOpenOptions();
            options.Validate();
            var fullPath = Path.GetFullPath(path);
            var file = new FileInfo(fullPath);
            if (!file.Exists) throw new FileNotFoundException("目录文件不存在。", fullPath);
            if (file.Length > options.MaxFileBytes)
                throw new InvalidDataException($"目录文件为 {file.Length} 字节，超过 {options.MaxFileBytes} 字节上限。");

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = fullPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                DefaultTimeout = 30
            };
            var connection = new SqliteConnection(builder.ToString());
            try
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA query_only=ON; PRAGMA cache_size=-" + options.PageCacheKiB + "; PRAGMA case_sensitive_like=ON;";
                    command.ExecuteNonQuery();
                }

                if (ExecuteInt64(connection, "PRAGMA application_id;") != SqliteApplicationId || ExecuteInt64(connection, "PRAGMA user_version;") != FileFormatVersion)
                    throw new InvalidDataException("文件不是受支持格式的 Bing.MockData 目录。");

                var info = ReadInfo(connection);
                var actualCount = ExecuteInt64(connection, "SELECT count(*) FROM Records;");
                if (actualCount != info.RecordCount)
                    throw new InvalidDataException($"目录 {info.Id} 声明 {info.RecordCount} 条记录，实际为 {actualCount} 条。");
                if (actualCount > 0 && (ExecuteInt64(connection, "SELECT min(Ordinal) FROM Records;") != 1 ||
                    ExecuteInt64(connection, "SELECT max(Ordinal) FROM Records;") != actualCount))
                    throw new InvalidDataException("目录记录序号必须从 1 开始且连续。");
                return new SqliteMockCatalog(connection, info);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>Reads snapshot identity while opening and closing a temporary connection.</summary>
        public static DataSetInfo ReadInfo(string path, CatalogOpenOptions? options = null)
        {
            using (var catalog = Open(path, options)) return catalog.Info;
        }

        public bool TryGet(string key, out CatalogRecord? record)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (_gate)
            {
                ThrowIfDisposed();
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "SELECT Key, Kind, CountryCode, Name, PayloadJson FROM Records WHERE Key = $key;";
                    command.Parameters.AddWithValue("$key", key);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) { record = null; return false; }
                        record = ReadRecord(reader);
                        return true;
                    }
                }
            }
        }

        public CatalogRecord GetAt(long ordinal)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (ordinal < 0 || ordinal >= Info.RecordCount) throw new ArgumentOutOfRangeException(nameof(ordinal));
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "SELECT Key, Kind, CountryCode, Name, PayloadJson FROM Records WHERE Ordinal = $ordinal;";
                    command.Parameters.AddWithValue("$ordinal", ordinal + 1);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) throw new InvalidDataException("目录序号超出实际记录范围。");
                        return ReadRecord(reader);
                    }
                }
            }
        }

        /// <summary>Selects deterministically using caller-owned bits, without retaining random state.</summary>
        public CatalogRecord Sample(ulong selector)
        {
            lock (_gate) ThrowIfDisposed();
            if (Info.RecordCount == 0) throw new InvalidOperationException($"目录 {Info.Id} 没有可抽样记录。");
            return GetAt((long)(selector % (ulong)Info.RecordCount));
        }

        /// <summary>Counts matching rows in SQLite without materializing them.</summary>
        public long Count(CatalogQuery query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var snapshot = query.Copy();
            snapshot.Validate();
            snapshot.CancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                return CountMatching(snapshot);
            }
        }

        /// <summary>Samples uniformly by caller-supplied selector from the filtered stable-key order.</summary>
        public CatalogRecord Sample(ulong selector, CatalogQuery query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var snapshot = query.Copy();
            snapshot.Validate();
            snapshot.CancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                var count = CountMatching(snapshot);
                if (count == 0) throw new CatalogCoverageException(Info.Id, DescribeFilters(snapshot));

                using (var command = _connection.CreateCommand())
                {
                    var clauses = new List<string>();
                    AddFilters(command, clauses, snapshot);
                    command.CommandText = "SELECT Key, Kind, CountryCode, Name, PayloadJson FROM Records" +
                        (clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses)) +
                        " ORDER BY Key COLLATE BINARY LIMIT 1 OFFSET $offset;";
                    command.Parameters.AddWithValue("$offset", (long)(selector % (ulong)count));
                    snapshot.CancellationToken.ThrowIfCancellationRequested();
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) throw new InvalidDataException("目录在抽样过程中发生了变化。");
                        return ReadRecord(reader);
                    }
                }
            }
        }

        public IEnumerable<CatalogRecord> Query(CatalogQuery query, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var snapshot = query.Copy();
            snapshot.Validate();
            lock (_gate) ThrowIfDisposed();
            if (cancellationToken.CanBeCanceled) snapshot.CancellationToken = cancellationToken;
            return Enumerate(snapshot);
        }

        private IEnumerable<CatalogRecord> Enumerate(CatalogQuery query)
        {
            string? lastKey = null;
            while (true)
            {
                query.CancellationToken.ThrowIfCancellationRequested();
                List<CatalogRecord> page;
                lock (_gate)
                {
                    ThrowIfDisposed();
                    page = ReadPage(query, lastKey);
                }

                foreach (var record in page)
                {
                    query.CancellationToken.ThrowIfCancellationRequested();
                    yield return record;
                }
                if (page.Count < query.PageSize) yield break;
                lastKey = page[page.Count - 1].Key;
            }
        }

        private List<CatalogRecord> ReadPage(CatalogQuery query, string? lastKey)
        {
            var clauses = new List<string>();
            using (var command = _connection.CreateCommand())
            {
                if (lastKey != null) AddFilter(command, clauses, "Key COLLATE BINARY > $lastKey", "$lastKey", lastKey);
                AddFilters(command, clauses, query);

                command.CommandText = "SELECT Key, Kind, CountryCode, Name, PayloadJson FROM Records" +
                    (clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses)) +
                    " ORDER BY Key COLLATE BINARY LIMIT $limit;";
                command.Parameters.AddWithValue("$limit", query.PageSize);
                var page = new List<CatalogRecord>(query.PageSize);
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) page.Add(ReadRecord(reader));
                return page;
            }
        }

        private long CountMatching(CatalogQuery query)
        {
            using (var command = _connection.CreateCommand())
            {
                var clauses = new List<string>();
                AddFilters(command, clauses, query);
                command.CommandText = "SELECT count(*) FROM Records" +
                    (clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses)) + ";";
                query.CancellationToken.ThrowIfCancellationRequested();
                return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static void AddFilters(SqliteCommand command, List<string> clauses, CatalogQuery query)
        {
            if (query.Kind != null) AddFilter(command, clauses, "Kind = $kind", "$kind", query.Kind);
            if (query.CountryCode != null) AddFilter(command, clauses, "CountryCode = $country", "$country", query.CountryCode);
            if (query.KeyPrefix != null) AddPrefix(command, clauses, "Key", "$keyPrefix", query.KeyPrefix);
            if (query.NamePrefix != null) AddPrefix(command, clauses, "Name", "$namePrefix", query.NamePrefix);
        }

        private static string DescribeFilters(CatalogQuery query)
        {
            var filters = new List<string>();
            if (query.Kind != null) filters.Add("Kind=" + query.Kind);
            if (query.CountryCode != null) filters.Add("CountryCode=" + query.CountryCode);
            if (query.KeyPrefix != null) filters.Add("KeyPrefix=" + query.KeyPrefix);
            if (query.NamePrefix != null) filters.Add("NamePrefix=" + query.NamePrefix);
            return filters.Count == 0 ? "任意记录" : string.Join(", ", filters);
        }

        private static void AddFilter(SqliteCommand command, List<string> clauses, string clause, string parameter, string value)
        {
            clauses.Add(clause);
            command.Parameters.AddWithValue(parameter, value);
        }

        private static void AddPrefix(SqliteCommand command, List<string> clauses, string column, string parameter, string value)
        {
            clauses.Add(column + " LIKE " + parameter + " ESCAPE '\\'");
            command.Parameters.AddWithValue(parameter, EscapeLike(value) + "%");
        }

        private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        private static CatalogRecord ReadRecord(SqliteDataReader reader)
        {
            return new CatalogRecord(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3), reader.GetString(4));
        }

        private static DataSetInfo ReadInfo(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, Version, SourceUri, License, SnapshotDate, RecordCount, SourceSha256, Coverage, FileFormatVersion FROM CatalogInfo LIMIT 1;";
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidDataException("CatalogInfo 表为空。");
                    if (reader.GetInt32(8) != FileFormatVersion) throw new InvalidDataException("目录文件格式版本不受支持。");
                    var info = new DataSetInfo(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                        reader.GetString(4), reader.GetInt64(5), reader.GetString(6), reader.GetString(7));
                    if (reader.Read()) throw new InvalidDataException("CatalogInfo 必须恰好包含一行。");
                    return info;
                }
            }
        }

        private static long ExecuteInt64(SqliteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SqliteMockCatalog));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _connection.Dispose();
            }
        }
    }
}
