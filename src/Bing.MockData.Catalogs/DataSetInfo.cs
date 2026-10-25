using System;

namespace Bing.MockData.Catalogs
{
    /// <summary>Describes the source and exact snapshot represented by a catalog file.</summary>
    public sealed class DataSetInfo
    {
        public string Id { get; }
        public string Version { get; }
        public string SourceUri { get; }
        public string License { get; }
        public string SnapshotDate { get; }
        public long RecordCount { get; }
        public string SourceSha256 { get; }
        public string Coverage { get; }

        public DataSetInfo(string id, string version, string sourceUri, string license, string snapshotDate,
            long recordCount, string sourceSha256, string coverage)
        {
            Id = Require(id, nameof(id));
            Version = Require(version, nameof(version));
            SourceUri = Require(sourceUri, nameof(sourceUri));
            License = Require(license, nameof(license));
            SnapshotDate = Require(snapshotDate, nameof(snapshotDate));
            if (recordCount < 0) throw new ArgumentOutOfRangeException(nameof(recordCount));
            RecordCount = recordCount;
            SourceSha256 = Require(sourceSha256, nameof(sourceSha256));
            if (SourceSha256.Length != 64 || !IsHex(SourceSha256))
                throw new ArgumentException("来源快照 SHA-256 必须是 64 位十六进制值。", nameof(sourceSha256));
            Coverage = Require(coverage, nameof(coverage));
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("值不能为空。", parameterName);
            return value;
        }

        private static bool IsHex(string value)
        {
            foreach (var c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }
    }
}
