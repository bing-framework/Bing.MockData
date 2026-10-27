using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Bing.MockData.Catalogs.Internal
{
    internal sealed class CatalogDatabaseBuilder : IDisposable
    {
        private readonly string _destinationPath;
        private readonly string _temporaryPath;
        private readonly SqliteConnection _connection;
        private readonly SqliteTransaction _transaction;
        private readonly SqliteCommand _insertRecord;
        private long _count;
        private long _duplicateCount;
        private bool _completed;
        private bool _disposed;

        public long RecordCount => _count;
        public long DuplicateCount => _duplicateCount;

        public CatalogDatabaseBuilder(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("输出路径不能为空。", nameof(destinationPath));
            _destinationPath = Path.GetFullPath(destinationPath);
            var directory = Path.GetDirectoryName(_destinationPath)!;
            Directory.CreateDirectory(directory);
            _temporaryPath = _destinationPath + "." + Guid.NewGuid().ToString("N") + ".building";

            var builder = new SqliteConnectionStringBuilder { DataSource = _temporaryPath, Pooling = false };
            _connection = new SqliteConnection(builder.ToString());
            _connection.Open();
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = "PRAGMA application_id=1112364081; PRAGMA user_version=1; PRAGMA page_size=4096; PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; PRAGMA cache_size=-8192;" +
                    "CREATE TABLE CatalogInfo (Id TEXT NOT NULL PRIMARY KEY, Version TEXT NOT NULL, SourceUri TEXT NOT NULL, License TEXT NOT NULL, SnapshotDate TEXT NOT NULL, RecordCount INTEGER NOT NULL, SourceSha256 TEXT NOT NULL, Coverage TEXT NOT NULL, FileFormatVersion INTEGER NOT NULL);" +
                    "CREATE TABLE Records (Ordinal INTEGER NOT NULL PRIMARY KEY, Key TEXT NOT NULL COLLATE BINARY UNIQUE, Kind TEXT NOT NULL, CountryCode TEXT NULL, Name TEXT NOT NULL, PayloadJson TEXT NOT NULL);";
                command.ExecuteNonQuery();
            }
            _transaction = _connection.BeginTransaction();
            _insertRecord = _connection.CreateCommand();
            _insertRecord.Transaction = _transaction;
            _insertRecord.CommandText = "INSERT OR IGNORE INTO Records (Ordinal, Key, Kind, CountryCode, Name, PayloadJson) VALUES ($ordinal, $key, $kind, $country, $name, $payload);";
            _insertRecord.Parameters.Add("$ordinal", SqliteType.Integer);
            _insertRecord.Parameters.Add("$key", SqliteType.Text);
            _insertRecord.Parameters.Add("$kind", SqliteType.Text);
            _insertRecord.Parameters.Add("$country", SqliteType.Text);
            _insertRecord.Parameters.Add("$name", SqliteType.Text);
            _insertRecord.Parameters.Add("$payload", SqliteType.Text);
        }

        public bool Add(string key, string kind, string? countryCode, string name, string payloadJson)
        {
            return AddCore(key, kind, countryCode, name, payloadJson, false);
        }

        public bool AddStrict(string key, string kind, string? countryCode, string name, string payloadJson)
        {
            return AddCore(key, kind, countryCode, name, payloadJson, true);
        }

        private bool AddCore(string key, string kind, string? countryCode, string name, string payloadJson, bool verifyDuplicate)
        {
            if (_disposed || _completed) throw new ObjectDisposedException(nameof(CatalogDatabaseBuilder));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("稳定键不能为空。", nameof(key));
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("记录类型不能为空。", nameof(kind));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (payloadJson == null) throw new ArgumentNullException(nameof(payloadJson));
            _insertRecord.Parameters["$ordinal"].Value = _count + 1;
            _insertRecord.Parameters["$key"].Value = key;
            _insertRecord.Parameters["$kind"].Value = kind;
            _insertRecord.Parameters["$country"].Value = (object?)countryCode ?? DBNull.Value;
            _insertRecord.Parameters["$name"].Value = name;
            _insertRecord.Parameters["$payload"].Value = payloadJson;
            if (_insertRecord.ExecuteNonQuery() == 0)
            {
                if (verifyDuplicate) VerifyIdenticalDuplicate(key, kind, countryCode, name, payloadJson);
                _duplicateCount++;
                return false;
            }
            _count++;
            return true;
        }

        private void VerifyIdenticalDuplicate(string key, string kind, string? countryCode, string name, string payloadJson)
        {
            using (var command = _connection.CreateCommand())
            {
                command.Transaction = _transaction;
                command.CommandText = "SELECT Kind, CountryCode, Name, PayloadJson FROM Records WHERE Key=$key;";
                command.Parameters.AddWithValue("$key", key);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidDataException("记录插入被忽略，但稳定键不存在：" + key);
                    var existingCountryCode = reader.IsDBNull(1) ? null : reader.GetString(1);
                    if (!string.Equals(reader.GetString(0), kind, StringComparison.Ordinal)
                        || !string.Equals(existingCountryCode, countryCode, StringComparison.Ordinal)
                        || !string.Equals(reader.GetString(2), name, StringComparison.Ordinal)
                        || !string.Equals(reader.GetString(3), payloadJson, StringComparison.Ordinal))
                        throw new InvalidDataException("同一稳定键映射到不同目录内容：" + key);
                }
            }
        }

        public void Complete(DataSetInfo info) => Complete((_, _) => info);

        public DataSetInfo Complete(Func<long, long, DataSetInfo> createInfo)
        {
            if (createInfo == null) throw new ArgumentNullException(nameof(createInfo));
            if (_disposed || _completed) throw new ObjectDisposedException(nameof(CatalogDatabaseBuilder));
            var info = createInfo(_count, _duplicateCount) ?? throw new InvalidOperationException("目录元数据工厂返回了 null。");
            if (info.RecordCount != _count) throw new InvalidDataException($"数据集声明 {info.RecordCount} 条记录，实际写入 {_count} 条。");
            _insertRecord.Dispose();
            using (var command = _connection.CreateCommand())
            {
                command.Transaction = _transaction;
                command.CommandText = "INSERT INTO CatalogInfo (Id, Version, SourceUri, License, SnapshotDate, RecordCount, SourceSha256, Coverage, FileFormatVersion) VALUES ($id, $version, $source, $license, $date, $count, $sha, $coverage, $format);";
                command.Parameters.AddWithValue("$id", info.Id);
                command.Parameters.AddWithValue("$version", info.Version);
                command.Parameters.AddWithValue("$source", info.SourceUri);
                command.Parameters.AddWithValue("$license", info.License);
                command.Parameters.AddWithValue("$date", info.SnapshotDate);
                command.Parameters.AddWithValue("$count", info.RecordCount);
                command.Parameters.AddWithValue("$sha", info.SourceSha256);
                command.Parameters.AddWithValue("$coverage", info.Coverage);
                command.Parameters.AddWithValue("$format", SqliteMockCatalog.FileFormatVersion);
                command.ExecuteNonQuery();
            }
            using (var command = _connection.CreateCommand())
            {
                command.Transaction = _transaction;
                command.CommandText = "CREATE INDEX IX_Records_KindCountryKey ON Records (Kind, CountryCode, Key COLLATE BINARY);" +
                    "CREATE INDEX IX_Records_NameKey ON Records (Name COLLATE BINARY, Key COLLATE BINARY);";
                command.ExecuteNonQuery();
            }
            _transaction.Commit();
            _transaction.Dispose();
            _connection.Dispose();
            if (new FileInfo(_temporaryPath).Length > 512L * 1024 * 1024)
                throw new InvalidDataException("生成的目录文件超过 512 MiB 上限。");
            if (File.Exists(_destinationPath)) File.Replace(_temporaryPath, _destinationPath, null);
            else File.Move(_temporaryPath, _destinationPath);
            _completed = true;
            return info;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _insertRecord.Dispose();
            _transaction.Dispose();
            _connection.Dispose();
            if (!_completed && File.Exists(_temporaryPath)) File.Delete(_temporaryPath);
        }
    }
}
