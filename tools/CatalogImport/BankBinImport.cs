using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Bing.MockData.Catalogs;
using Bing.MockData.Catalogs.Internal;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class BankBinImport
{
    internal const string Commit = "fcc907c81cf02b12e396b0c12a8c16472b47619a";
    internal const string SnapshotDate = "2026-09-03";
    internal const int ExpectedRows = 458051;
    internal const int ExpectedChinaRows = 18389;
    internal const long MaximumDatabaseBytes = 512L * 1024 * 1024;
    internal const string DataSetId = "bank-bins-techbuddie";
    internal const string DataSetVersion = Commit + "-import-v1";

    internal static readonly string[] InputFiles =
    {
        "bins.csv", "README.md", "LICENSE", "conflicts.csv", "summary.json", "merge.py"
    };

    internal static readonly string[] Columns =
    {
        "bin", "brand", "type", "category", "issuer", "issuer_phone", "issuer_url",
        "alpha_2", "alpha_3", "country", "source", "seen"
    };

    private static readonly IReadOnlyDictionary<string, string> ExpectedInputHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bins.csv"] = "50e9d57ae6c7b04c17a18c74aa3c9b0b07962ec4ba2fbe8d1ff3b6db00bd7615",
            ["conflicts.csv"] = "e936861e24aad6668bba6c49ed45b87863e62bd7ed37774d8b76d7e78ebbf355",
            ["LICENSE"] = "2a82ac9bbc3e3ee066908381e8d373896db5a6025d083fbd59692fe9ccfb9111",
            ["merge.py"] = "4f2602ab32b30ea18d3252ee53370c661b2e4d2f51c3a8449ace077233ad0292",
            ["README.md"] = "09ad86c4aeba39588967b68b801c64845a44530f0c776ef00b1db1c4629dbbae",
            ["summary.json"] = "05b47e9edb9908794fb4bfc15e57afbcbe37a69bcd77941b2b651e850274185c"
        };

    private const string SourceUri = "https://github.com/Techbuddie-Solutions/binlist-data/tree/" + Commit;
    private const string License = "CC-BY-4.0";
    private const string Coverage = "Community-compiled issuer/BIN metadata; this snapshot contains 7 five-digit, 454496 six-digit, and 3548 eight-digit prefixes. The upstream README describes six-digit prefixes with a small number of five-digit legacy rows and states that eight-digit BINs are not covered by open sources, which conflicts with the included rows. All source rows are preserved; the dataset is not official or exhaustive, PAN length is not provided, and empty source cells are unknown and stored as null.";

    internal static BankBinImportResult Import(string sourceFolder, string outputRoot, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder)) throw new ArgumentException("原始数据目录不能为空。", nameof(sourceFolder));
        if (string.IsNullOrWhiteSpace(outputRoot)) throw new ArgumentException("输出目录不能为空。", nameof(outputRoot));
        var folder = Path.GetFullPath(sourceFolder);
        ValidateSource(folder);
        token.ThrowIfCancellationRequested();

        var summary = JObject.Parse(File.ReadAllText(Path.Combine(folder, "summary.json"), Encoding.UTF8));
        var declaredRows = (int?)summary["total"];
        if (declaredRows != ExpectedRows || (string?)summary["generated"] != SnapshotDate)
            throw new InvalidDataException("固定 BIN 来源 summary.json 的版本或行数不匹配。");

        var targetPath = Path.Combine(Path.GetFullPath(outputRoot), "bank-bins.sqlite3");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var inputInfos = InputFiles.Select(name => new BankBinInputInfo(name,
            Sha256(Path.Combine(folder, name)), new FileInfo(Path.Combine(folder, name)).Length)).ToArray();
        var sourceSha = AggregateChecksum(folder, InputFiles);
        IEnumerable<BankBinSourceRow> Rows() => ReadCsvFile(Path.Combine(folder, "bins.csv"), token);

        var result = ImportRows(Rows, targetPath, sourceSha, SnapshotDate,
            ExpectedRows, ExpectedChinaRows, token, ExpectedRows, 0,
            () =>
            {
                if (!string.Equals(AggregateChecksum(folder, InputFiles), sourceSha, StringComparison.Ordinal))
                    throw new InvalidDataException("固定 BIN 来源文件在导入过程中发生变化。");
            });

        var outputSha = Sha256(targetPath);
        var outputBytes = new FileInfo(targetPath).Length;
        if (outputBytes > MaximumDatabaseBytes)
            throw new InvalidDataException("生成的 BIN 目录超过 512 MiB 上限。");

        var evidence = new JObject
        {
            ["schemaVersion"] = 1,
            ["status"] = "PASS",
            ["source"] = new JObject
            {
                ["repository"] = "https://github.com/Techbuddie-Solutions/binlist-data",
                ["commit"] = Commit,
                ["commitDate"] = "2026-09-03T16:14:29Z",
                ["sourceUri"] = SourceUri,
                ["license"] = License,
                ["attribution"] = "Techbuddie Solutions; upstream README also credits Ian Nuttall and Venelin Kochev, each CC BY 4.0",
                ["sha256"] = sourceSha,
                ["inputFiles"] = JArray.FromObject(inputInfos),
                ["upstreamSummary"] = summary.DeepClone()
            },
            ["transform"] = new JObject
            {
                ["stableKey"] = "bin|<exact bin field>",
                ["kind"] = "bank-bin",
                ["payloadFields"] = JArray.FromObject(Columns),
                ["emptyText"] = "null",
                ["name"] = "issuer, or empty string when unknown",
                ["countryCode"] = "alpha_2, or null when unknown",
                ["duplicatePolicy"] = "Exact duplicate rows are merged and counted; duplicate keys with any differing field fail the import.",
                ["sqliteFieldVerification"] = "Every source row was streamed again and compared to the generated SQLite row by stable key and all 12 payload fields.",
                ["sourceBoundary"] = "This imported snapshot contains 7 five-digit, 454496 six-digit, and 3548 eight-digit prefixes. Its README describes six-digit prefixes with a small number of five-digit legacy rows and states that open sources do not cover eight-digit BINs; the actual snapshot contradicts that statement. All records are preserved, without a completeness or issuer-verification claim; PAN length is not provided."
            },
            ["counts"] = new JObject
            {
                ["sourceRows"] = result.SourceRows,
                ["catalogRecords"] = result.CatalogRows,
                ["duplicateRows"] = result.DuplicateCount,
                ["countryCnRows"] = result.ChinaRows,
                ["rowsClosed"] = result.SourceRows == result.CatalogRows + result.DuplicateCount,
                ["missingFieldRows"] = JObject.FromObject(result.MissingFieldRows),
                ["binDigitLengthDistribution"] = JObject.FromObject(result.BinLengthDistribution)
            },
            ["catalog"] = new JObject
            {
                ["id"] = result.Info.Id,
                ["version"] = result.Info.Version,
                ["recordCount"] = result.Info.RecordCount,
                ["sourceSha256"] = result.Info.SourceSha256,
                ["sqliteSha256"] = outputSha,
                ["sqliteBytes"] = outputBytes,
                ["sizeLimitBytes"] = MaximumDatabaseBytes,
                ["withinSizeLimit"] = outputBytes <= MaximumDatabaseBytes
            },
            ["verification"] = new JObject
            {
                ["csvToSqliteAllFields"] = "PASS",
                ["stableKeysUnique"] = true,
                ["chinaRows"] = result.ChinaRows == ExpectedChinaRows,
                ["sourceRows"] = result.SourceRows == ExpectedRows
            }
        };

        return result with
        {
            OutputPath = targetPath,
            OutputSha256 = outputSha,
            OutputBytes = outputBytes,
            Inputs = inputInfos,
            Evidence = evidence
        };
    }

    internal static BankBinImportResult ImportRows(Func<IEnumerable<BankBinSourceRow>> rowsFactory,
        string targetPath, string sourceSha, string snapshotDate, long? expectedRows,
        long? expectedChinaRows, CancellationToken token, long? expectedCatalogRows = null,
        long? expectedDuplicates = null, Action? validateBeforePublish = null)
    {
        if (rowsFactory == null) throw new ArgumentNullException(nameof(rowsFactory));
        if (targetPath == null) throw new ArgumentNullException(nameof(targetPath));
        var target = Path.GetFullPath(targetPath);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var candidate = target + ".candidate." + Guid.NewGuid().ToString("N");
        var sourceRows = 0L;
        var chinaRows = 0L;
        var missing = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var lengths = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var kindCounts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        DataSetInfo? info = null;
        var duplicateCount = 0L;
        var catalogRows = 0L;

        try
        {
            using (var writer = new CatalogDatabaseBuilder(candidate))
            {
                foreach (var row in rowsFactory())
                {
                    token.ThrowIfCancellationRequested();
                    sourceRows++;
                    if (row.CountryCode == "CN") chinaRows++;
                    var binLengthKey = row.Bin.Length.ToString(CultureInfo.InvariantCulture);
                    lengths.TryGetValue(binLengthKey, out var binLengthCount);
                    lengths[binLengthKey] = binLengthCount + 1;
                    for (var i = 0; i < Columns.Length; i++)
                    {
                        if (row.Values[i].Length != 0) continue;
                        missing.TryGetValue(Columns[i], out var missingCount);
                        missing[Columns[i]] = missingCount + 1;
                    }

                    if (writer.AddStrict(row.Key, "bank-bin", row.CountryCode, row.Name, row.PayloadJson))
                    {
                        catalogRows++;
                        kindCounts.TryGetValue("bank-bin", out var count);
                        kindCounts["bank-bin"] = count + 1;
                    }
                    else duplicateCount++;
                }

                if (expectedRows.HasValue && sourceRows != expectedRows.Value)
                    throw new InvalidDataException($"BIN CSV source row count mismatch: expected {expectedRows.Value}, found {sourceRows}.");
                if (expectedChinaRows.HasValue && chinaRows != expectedChinaRows.Value)
                    throw new InvalidDataException($"BIN CSV CN row count mismatch: expected {expectedChinaRows.Value}, found {chinaRows}.");
                if (expectedCatalogRows.HasValue && writer.RecordCount != expectedCatalogRows.Value)
                    throw new InvalidDataException($"BIN catalog row count mismatch: expected {expectedCatalogRows.Value}, found {writer.RecordCount}.");
                if (expectedDuplicates.HasValue && writer.DuplicateCount != expectedDuplicates.Value)
                    throw new InvalidDataException($"BIN duplicate row count mismatch: expected {expectedDuplicates.Value}, found {writer.DuplicateCount}.");

                info = writer.Complete((count, duplicates) => new DataSetInfo(DataSetId, DataSetVersion,
                    SourceUri, License, snapshotDate, count, sourceSha, Coverage));
                duplicateCount = writer.DuplicateCount;
                catalogRows = writer.RecordCount;
            }

            token.ThrowIfCancellationRequested();
            VerifyDatabase(rowsFactory, candidate, info!, catalogRows, sourceRows, token);
            token.ThrowIfCancellationRequested();
            validateBeforePublish?.Invoke();
            token.ThrowIfCancellationRequested();
            if (File.Exists(target)) File.Replace(candidate, target, null);
            else File.Move(candidate, target);

            return new BankBinImportResult(info!, target, Sha256(target), new FileInfo(target).Length,
                Array.Empty<BankBinInputInfo>(), kindCounts, duplicateCount, sourceRows, catalogRows,
                chinaRows, missing, lengths, new JObject());
        }
        finally
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }
    }

    internal static IEnumerable<BankBinSourceRow> ReadCsvFile(string path, CancellationToken token)
    {
        using (var reader = new StreamReader(path, new UTF8Encoding(false, true), true, 64 * 1024))
        {
            foreach (var row in ReadCsv(reader, token)) yield return row;
        }
    }

    internal static IEnumerable<BankBinSourceRow> ReadCsv(TextReader reader, CancellationToken token)
    {
        var header = ReadCsvRow(reader);
        if (header == null || !header.SequenceEqual(Columns, StringComparer.Ordinal))
            throw new InvalidDataException("BIN CSV header does not match the pinned 12-field schema.");

        List<string>? values;
        while ((values = ReadCsvRow(reader)) != null)
        {
            token.ThrowIfCancellationRequested();
            if (values.Count == 1 && values[0].Length == 0) continue;
            if (values.Count > Columns.Length)
                throw new InvalidDataException("BIN CSV row contains more fields than its header.");
            var fullValues = new string[Columns.Length];
            for (var i = 0; i < fullValues.Length; i++)
                fullValues[i] = i < values.Count ? values[i] : string.Empty;
            yield return new BankBinSourceRow(fullValues);
        }
    }

    private static List<string>? ReadCsvRow(TextReader reader)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var insideQuotes = false;
        var afterQuote = false;
        var sawAny = false;

        while (true)
        {
            var value = reader.Read();
            if (value < 0)
            {
                if (insideQuotes) throw new InvalidDataException("BIN CSV ends inside a quoted field.");
                if (!sawAny && fields.Count == 0 && field.Length == 0) return null;
                fields.Add(field.ToString());
                return fields;
            }

            sawAny = true;
            var c = (char)value;
            if (insideQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                    else { insideQuotes = false; afterQuote = true; }
                }
                else field.Append(c);
                continue;
            }

            if (afterQuote && c != ',' && c != '\r' && c != '\n')
                throw new InvalidDataException("BIN CSV contains text after a closing quote.");
            if (c == '"')
            {
                if (field.Length != 0 || afterQuote)
                    throw new InvalidDataException("BIN CSV contains a quote inside an unquoted field.");
                insideQuotes = true;
                continue;
            }
            if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                afterQuote = false;
                continue;
            }
            if (c == '\n' || c == '\r')
            {
                if (c == '\r' && reader.Peek() == '\n') reader.Read();
                fields.Add(field.ToString());
                return fields;
            }
            field.Append(c);
        }
    }

    private static void ValidateSource(string folder)
    {
        foreach (var name in InputFiles)
        {
            var path = Path.Combine(folder, name);
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new FileNotFoundException("Pinned bank BIN source input is missing or empty.", path);
        }

        var readme = File.ReadAllText(Path.Combine(folder, "README.md"), Encoding.UTF8);
        if (!readme.Contains("CC BY 4.0", StringComparison.Ordinal)
            || !readme.Contains("not exhaustive", StringComparison.Ordinal)
            || !readme.Contains("eight-digit BINs", StringComparison.Ordinal)
            || !readme.Contains("iannuttall/binlist-data", StringComparison.Ordinal)
            || !readme.Contains("venelinkochev/bin-list-data", StringComparison.Ordinal))
            throw new InvalidDataException("Pinned BIN README is missing license, attribution, or coverage statements.");
        var license = File.ReadAllText(Path.Combine(folder, "LICENSE"), Encoding.UTF8);
        if (!license.Contains("Attribution 4.0 International", StringComparison.Ordinal))
            throw new InvalidDataException("Pinned BIN LICENSE is not the expected CC BY 4.0 legal text.");
        VerifyInputHashes(folder, ExpectedInputHashes);
    }

    private static void VerifyInputHashes(string folder, IReadOnlyDictionary<string, string> expectedHashes)
    {
        foreach (var name in InputFiles)
        {
            if (!expectedHashes.TryGetValue(name, out var expectedHash))
                throw new InvalidDataException("Pinned BIN source lock is missing an input hash: " + name);
            var actualHash = Sha256(Path.Combine(folder, name));
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException("Pinned BIN input hash mismatch: " + name);
        }
    }

    private static void VerifyDatabase(Func<IEnumerable<BankBinSourceRow>> rowsFactory, string path,
        DataSetInfo expectedInfo, long expectedCount, long expectedSourceRows, CancellationToken token)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            using (var infoCommand = connection.CreateCommand())
            {
                infoCommand.CommandText = "SELECT Id, Version, SourceUri, License, SnapshotDate, RecordCount, SourceSha256, Coverage FROM CatalogInfo;";
                using (var reader = infoCommand.ExecuteReader())
                {
                    if (!reader.Read()
                        || reader.GetString(0) != expectedInfo.Id
                        || reader.GetString(1) != expectedInfo.Version
                        || reader.GetString(2) != expectedInfo.SourceUri
                        || reader.GetString(3) != expectedInfo.License
                        || reader.GetString(4) != expectedInfo.SnapshotDate
                        || reader.GetInt64(5) != expectedInfo.RecordCount
                        || reader.GetString(6) != expectedInfo.SourceSha256
                        || reader.GetString(7) != expectedInfo.Coverage
                        || reader.Read())
                        throw new InvalidDataException("Generated BIN CatalogInfo does not match the pinned import contract.");
                }
            }

            using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = "SELECT COUNT(*) FROM Records;";
                if (Convert.ToInt64(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture) != expectedCount)
                    throw new InvalidDataException("Generated BIN SQLite record count does not match the import count.");
            }

            using (var query = connection.CreateCommand())
            {
                query.CommandText = "SELECT Kind, CountryCode, Name, PayloadJson FROM Records WHERE Key=$key;";
                query.Parameters.Add("$key", SqliteType.Text);
                using (var sourceRows = rowsFactory().GetEnumerator())
                {
                    var verified = 0L;
                    while (sourceRows.MoveNext())
                    {
                        token.ThrowIfCancellationRequested();
                        var source = sourceRows.Current;
                        query.Parameters["$key"].Value = source.Key;
                        using (var record = query.ExecuteReader())
                        {
                            if (!record.Read()) throw new InvalidDataException("Generated BIN SQLite is missing source key " + source.Key);
                            if (record.GetString(0) != "bank-bin"
                                || !string.Equals(record.IsDBNull(1) ? null : record.GetString(1), source.CountryCode, StringComparison.Ordinal)
                                || !string.Equals(record.GetString(2), source.Name, StringComparison.Ordinal))
                                throw new InvalidDataException("Generated BIN SQLite index fields differ from source for " + source.Key);
                            var payload = JObject.Parse(record.GetString(3));
                            if (payload.Count != Columns.Length)
                                throw new InvalidDataException("Generated BIN SQLite payload has an unexpected field count for " + source.Key);
                            for (var i = 0; i < Columns.Length; i++)
                            {
                                var tokenValue = payload[Columns[i]];
                                var expected = source.Values[i];
                                if (expected.Length == 0)
                                {
                                    if (tokenValue == null || tokenValue.Type != JTokenType.Null)
                                        throw new InvalidDataException("Generated BIN SQLite did not preserve unknown as null: " + Columns[i]);
                                }
                                else if (tokenValue?.Type != JTokenType.String || (string?)tokenValue != expected)
                                    throw new InvalidDataException("Generated BIN SQLite field differs from CSV: " + Columns[i] + " at " + source.Key);
                            }
                        }
                        verified++;
                    }
                    if (verified != expectedSourceRows)
                        throw new InvalidDataException($"CSV-to-SQLite verification row count mismatch: expected {expectedSourceRows}, found {verified}.");
                }
            }
        }
    }

    private static string AggregateChecksum(string folder, IEnumerable<string> names)
    {
        var builder = new StringBuilder();
        foreach (var name in names.OrderBy(item => item, StringComparer.Ordinal))
            builder.Append(name).Append('\n').Append(Sha256(Path.Combine(folder, name))).Append('\n');
        return Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string Sha256(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var hash = SHA256.Create())
            return ToHex(hash.ComputeHash(stream));
    }

    private static string Sha256(byte[] bytes)
    {
        using (var hash = SHA256.Create()) return ToHex(hash.ComputeHash(bytes));
    }

    private static string ToHex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

    internal static void WriteEvidence(BankBinImportResult result, string path)
    {
        var output = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, result.Evidence.ToString(Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
            if (File.Exists(output)) File.Replace(temporary, output, null);
            else File.Move(temporary, output);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static void WriteNotice(BankBinImportResult result, string outputRoot)
    {
        var noticeDirectory = Path.Combine(Path.GetFullPath(outputRoot), "notices");
        Directory.CreateDirectory(noticeDirectory);
        var path = Path.Combine(noticeDirectory, "bank-bins-NOTICE.txt");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var content = string.Join(Environment.NewLine, new[]
        {
            "Dataset: " + result.Info.Id + " " + result.Info.Version,
            "Source: https://github.com/Techbuddie-Solutions/binlist-data/tree/" + Commit,
            "License: CC BY 4.0. The upstream LICENSE and README are included with this data package.",
            "Attribution: Techbuddie Solutions; upstream README credits Ian Nuttall and Venelin Kochev.",
            "Source SHA-256: " + result.Info.SourceSha256,
            "Records: " + result.CatalogRows.ToString(CultureInfo.InvariantCulture) + "; duplicate source rows: " + result.DuplicateCount.ToString(CultureInfo.InvariantCulture) + ".",
            "Observed BIN field lengths: 5 digits=7, 6 digits=454496, 8 digits=3548; all source rows are retained.",
            "Coverage note: the upstream README describes six-digit prefixes with a small number of five-digit legacy rows and states that eight-digit BINs are not covered by open sources. The pinned CSV nevertheless contains 3548 eight-digit prefix rows. This snapshot is community-compiled, non-official, and not exhaustive.",
            "Transformation: stable key is bin|<exact bin>; kind is bank-bin; all 12 source columns are retained as strings, empty cells become JSON null, Name projects issuer or an empty string, and CountryCode projects alpha_2 or null.",
            "The seen field is the upstream observation date and is not confirmation by the bank. Issuer and card data may be stale or reassigned; no PAN length or payment decision is derived from this dataset.",
            "The upstream conflicts.csv is included to preserve merge disagreement provenance."
        }) + Environment.NewLine;
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static int RunFixtures()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "Bing.MockData.BankBinFixtures." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            var header = string.Join(",", Columns);
            const string quotedRow = "000001,VISA,DEBIT,PREPAID,\"Issuer, \"\"A\"\"\r\nBranch\",,https://issuer.example,US,USA,UNITED STATES,test,2026-09-03";
            const string shortRow = "01234,MASTERCARD,CREDIT,,Issuer Two,,,CN,CHN,CHINA,merged,2025-02-11";
            const string eightDigitRow = "12345678,VISA,CREDIT,,Issuer Three,123-456-7890,https://three.example,GB,GBR,UNITED KINGDOM,correction,2026-08-01";
            var successfulCsv = header + "\r\n" + quotedRow + "\r\n" + quotedRow + "\r\n" + shortRow + "\r\n" + eightDigitRow + "\r\n";
            Func<IEnumerable<BankBinSourceRow>> successfulRows = () => ReadCsv(new StringReader(successfulCsv), CancellationToken.None);
            var successPath = Path.Combine(temporaryRoot, "fixture.sqlite3");
            var sourceHash = new string('a', 64);
            var success = ImportRows(successfulRows, successPath, sourceHash, SnapshotDate, 4, 1, CancellationToken.None);
            Assert(success.SourceRows == 4 && success.CatalogRows == 3 && success.DuplicateCount == 1,
                "Identical duplicate rows were not merged and counted.");
            Assert(success.BinLengthDistribution.ContainsKey("5") && success.BinLengthDistribution["5"] == 1,
                "Five digit leading-zero BIN was not preserved.");
            Assert(success.BinLengthDistribution.ContainsKey("8") && success.BinLengthDistribution["8"] == 1,
                "Eight digit BIN distribution was not preserved.");
            Assert(success.MissingFieldRows["issuer_phone"] == 3,
                "Empty source cells were not counted as unknown.");
            Assert(ReadPayloadField(successPath, "bin|000001", "issuer") == "Issuer, \"A\"\r\nBranch",
                "Quoted comma, quote escape, or embedded newline was not preserved.");
            Assert(ReadPayloadField(successPath, "bin|000001", "issuer_phone") == null,
                "Empty CSV text did not project to JSON null.");
            Assert(ReadPayloadField(successPath, "bin|12345678", "bin") == "12345678",
                "Eight digit BIN prefix was not preserved in the SQLite payload.");

            var conflictPath = Path.Combine(temporaryRoot, "conflict.sqlite3");
            var oldConflictBytes = Encoding.UTF8.GetBytes("old-catalog-conflict");
            File.WriteAllBytes(conflictPath, oldConflictBytes);
            var conflictCsv = header + "\n" + shortRow + "\n" + shortRow.Replace("Issuer Two", "Changed Issuer", StringComparison.Ordinal) + "\n";
            AssertThrows<InvalidDataException>(() => ImportRows(
                () => ReadCsv(new StringReader(conflictCsv), CancellationToken.None),
                conflictPath, sourceHash, SnapshotDate, null, null, CancellationToken.None));
            Assert(File.ReadAllBytes(conflictPath).SequenceEqual(oldConflictBytes),
                "Conflicting duplicate replaced the previous catalog.");
            AssertNoCandidateFiles(temporaryRoot);

            var cancelPath = Path.Combine(temporaryRoot, "cancel.sqlite3");
            var oldCancelBytes = Encoding.UTF8.GetBytes("old-catalog-cancel");
            File.WriteAllBytes(cancelPath, oldCancelBytes);
            using (var cancellation = new CancellationTokenSource())
            {
                IEnumerable<BankBinSourceRow> CancelDuringEnumeration()
                {
                    yield return new BankBinSourceRow(shortRow.Split(','));
                    cancellation.Cancel();
                    yield return new BankBinSourceRow(shortRow.Replace("01234", "01235", StringComparison.Ordinal).Split(','));
                }

                AssertThrows<OperationCanceledException>(() => ImportRows(
                    CancelDuringEnumeration, cancelPath, sourceHash, SnapshotDate, null, null, cancellation.Token));
            }
            Assert(File.ReadAllBytes(cancelPath).SequenceEqual(oldCancelBytes),
                "Canceled import replaced the previous catalog.");
            AssertNoCandidateFiles(temporaryRoot);

            AssertThrows<InvalidDataException>(() => ReadCsv(new StringReader(header + "\n" +
                "01234,VISA,DEBIT,,,,,,,,,\"unterminated"), CancellationToken.None).ToList());
            AssertThrows<InvalidDataException>(() => ReadCsv(new StringReader(header.Replace("bin,", "prefix,", StringComparison.Ordinal) + "\n"),
                CancellationToken.None).ToList());

            var hashRoot = Path.Combine(temporaryRoot, "source-lock");
            Directory.CreateDirectory(hashRoot);
            var fixtureHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in InputFiles)
            {
                var file = Path.Combine(hashRoot, name);
                File.WriteAllText(file, "pinned:" + name, new UTF8Encoding(false));
                fixtureHashes[name] = Sha256(file);
            }
            VerifyInputHashes(hashRoot, fixtureHashes);
            File.AppendAllText(Path.Combine(hashRoot, "bins.csv"), "changed", new UTF8Encoding(false));
            AssertThrows<InvalidDataException>(() => VerifyInputHashes(hashRoot, fixtureHashes));

            var cancelAtPublishPath = Path.Combine(temporaryRoot, "cancel-at-publish.sqlite3");
            var oldPublishBytes = Encoding.UTF8.GetBytes("old-catalog-publish");
            File.WriteAllBytes(cancelAtPublishPath, oldPublishBytes);
            using (var cancellation = new CancellationTokenSource())
            {
                AssertThrows<OperationCanceledException>(() => ImportRows(
                    successfulRows, cancelAtPublishPath, sourceHash, SnapshotDate, 4, 1,
                    cancellation.Token, validateBeforePublish: cancellation.Cancel));
            }
            Assert(File.ReadAllBytes(cancelAtPublishPath).SequenceEqual(oldPublishBytes),
                "Cancellation after the final validation replaced the previous catalog.");
            AssertNoCandidateFiles(temporaryRoot);

            Console.WriteLine("bank-bins fixtures: PASS (CSV fidelity, 5/6/8 digit prefixes, nulls, hash lock, duplicates, conflict, cancellation, target preservation)");
            return 0;
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static string? ReadPayloadField(string path, string key, string field)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString();
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT PayloadJson FROM Records WHERE Key=$key;";
                command.Parameters.AddWithValue("$key", key);
                var json = (string?)command.ExecuteScalar();
                return json == null ? null : (string?)JObject.Parse(json)[field];
            }
        }
    }

    private static void AssertNoCandidateFiles(string directory)
    {
        if (Directory.GetFiles(directory, "*.candidate.*").Length != 0
            || Directory.GetFiles(directory, "*.candidate.*.building").Length != 0)
            throw new InvalidOperationException("A failed BIN import left a candidate or building database behind.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException("Expected " + typeof(TException).Name + " was not thrown.");
    }

}

internal sealed class BankBinSourceRow
{
    internal string[] Values { get; }
    internal string Bin => Values[0];
    internal string? CountryCode => Values[7].Length == 0 ? null : Values[7];
    internal string Name => Values[4].Length == 0 ? string.Empty : Values[4];
    internal string Key => "bin|" + Bin;
    internal string PayloadJson
    {
        get
        {
            var payload = new JObject();
            for (var i = 0; i < BankBinImport.Columns.Length; i++)
                payload[BankBinImport.Columns[i]] = Values[i].Length == 0 ? JValue.CreateNull() : new JValue(Values[i]);
            return payload.ToString(Formatting.None);
        }
    }

    internal BankBinSourceRow(string[] values)
    {
        if (values == null || values.Length != BankBinImport.Columns.Length)
            throw new InvalidDataException("BIN source row must contain exactly twelve values.");
        Values = (string[])values.Clone();
        if (Values[0].Length < 1 || Values[0].Length > 8 || Values[0].Any(c => c < '0' || c > '9'))
            throw new InvalidDataException("BIN source key must contain 1-8 ASCII digits.");
    }
}

internal sealed record BankBinInputInfo(string File, string Sha256, long Bytes);

internal sealed record BankBinImportResult(DataSetInfo Info, string OutputPath, string OutputSha256,
    long OutputBytes, BankBinInputInfo[] Inputs, IReadOnlyDictionary<string, long> KindCounts,
    long DuplicateCount, long SourceRows, long CatalogRows, long ChinaRows,
    IReadOnlyDictionary<string, long> MissingFieldRows,
    IReadOnlyDictionary<string, long> BinLengthDistribution, JObject Evidence);
