using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Xml.Linq;
using Bing.MockData.Catalogs;
using Bing.MockData.Catalogs.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class Program
{
    private const string MimeVersion = "1.54.0";
    private const string CldrVersion = "49.0.0";
    private const string CldrImportVersion = "49.0.0-import-v2";
    private const string PciCommit = "fe7fceb2e1153d495e1deefacf59c5d61c36ef8e";
    private const string AirportsCommit = "711cf6754ef05a1ccedc2482cc1e838032249d3c";
    private const string PhoneMetadataCommit = "6d349ac62b76a86eccba0ac1d2fc83ea2b00a841";
    private static readonly HttpClient Http = CreateHttpClient();

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintUsage();
            return 0;
        }

        var output = Option(args, "--output") ?? Path.GetFullPath("src/Bing.MockData.Catalogs/Data");
        var raw = Option(args, "--raw") ?? Path.GetFullPath("tools/CatalogImport/raw");
        var refresh = args.Contains("--refresh", StringComparer.Ordinal);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

        try
        {
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(raw);
            var names = args[0] switch
            {
                "all-basic" => new[] { "mime-db", "cldr", "pci", "ourairports", "phone-metadata", "geonames-city", "geonames-postal" },
                "geonames" => new[] { "geonames-city", "geonames-postal" },
                "mime-db" or "cldr" or "pci" or "ourairports" or "phone-metadata" or "geonames-city" or "geonames-postal" => new[] { args[0] },
                _ => throw new ArgumentException($"未知数据源：{args[0]}")
            };

            var outputs = new List<CatalogOutput>();
            foreach (var name in names)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                Console.WriteLine($"Importing {name}...");
                if (name == "geonames-postal")
                {
                    outputs.AddRange(await ImportGeoNamesPostalShards(raw, output, refresh, cancellation.Token));
                    continue;
                }
                outputs.Add(name switch
                {
                    "mime-db" => await ImportMimeDb(raw, output, refresh, cancellation.Token),
                    "cldr" => await ImportCldr(raw, output, refresh, cancellation.Token),
                    "pci" => await ImportPci(raw, output, refresh, cancellation.Token),
                    "ourairports" => await ImportOurAirports(raw, output, refresh, cancellation.Token),
                    "phone-metadata" => await ImportPhoneMetadata(raw, output, refresh, cancellation.Token),
                    "geonames-city" => await ImportGeoNames(raw, output, refresh, cancellation.Token, postal: false),
                    _ => throw new InvalidOperationException(name)
                });
            }

            WriteManifest(output, outputs);
            foreach (var item in outputs)
                Console.WriteLine($"{item.Info.Id}: {item.Info.RecordCount:N0} records, source SHA-256 {item.Info.SourceSha256}");
            Console.WriteLine($"Manifest: {Path.Combine(output, "catalogs.manifest.json")}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Import canceled.");
            return 130;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static async Task<CatalogOutput> ImportMimeDb(string rawRoot, string outputRoot, bool refresh, CancellationToken token)
    {
        var folder = Path.Combine(rawRoot, "mime-db", MimeVersion);
        Directory.CreateDirectory(folder);
        var metadataPath = Path.Combine(folder, "npm-package-metadata.json");
        var packumentPath = Path.Combine(folder, "npm-package-packument.json");
        var metadata = await DownloadJson("https://registry.npmjs.org/mime-db/" + MimeVersion, metadataPath, refresh, token);
        if ((string?)metadata["version"] != MimeVersion || (string?)metadata["license"] != "MIT")
            throw new InvalidDataException("mime-db package metadata version or license changed.");
        var tarballUrl = (string?)metadata["dist"]?["tarball"] ?? throw new InvalidDataException("npm metadata has no tarball URL.");
        var tarballPath = Path.Combine(folder, "mime-db-" + MimeVersion + ".tgz");
        var packument = await DownloadJson("https://registry.npmjs.org/mime-db", packumentPath, refresh, token);
        if (packument["versions"]?[MimeVersion] == null) throw new InvalidDataException("npm packument does not contain the requested mime-db release.");
        var published = (string?)packument["time"]?[MimeVersion] ?? throw new InvalidDataException("npm packument has no release timestamp.");
        var publishedAt = DateTimeOffset.Parse(published, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
        await Download(tarballUrl, tarballPath, refresh, token);
        var dbPath = Path.Combine(folder, "db.json");
        var licensePath = Path.Combine(folder, "LICENSE");
        ExtractTarEntry(tarballPath, "package/db.json", dbPath);
        ExtractTarEntry(tarballPath, "package/LICENSE", licensePath);
        CopyNotice(licensePath, outputRoot, "mime-db-MIT-LICENSE.txt");
        var data = JObject.Parse(await File.ReadAllTextAsync(dbPath, Encoding.UTF8, token));
        var inputs = new[] { "npm-package-metadata.json", "npm-package-packument.json", "mime-db-" + MimeVersion + ".tgz" };
        return WriteCatalog(outputRoot, "mime-db", MimeVersion, "https://registry.npmjs.org/mime-db/" + MimeVersion,
            "MIT", "npm package tarball; aliases retained", folder, inputs, EnumerateMime(data),
            snapshotDate: publishedAt.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), token: token);
    }

    private static async Task<CatalogOutput> ImportCldr(string rawRoot, string outputRoot, bool refresh, CancellationToken token)
    {
        var folder = Path.Combine(rawRoot, "cldr", CldrVersion);
        Directory.CreateDirectory(folder);
        var root = "https://raw.githubusercontent.com/unicode-org/cldr-json/" + CldrVersion + "/cldr-json/";
        var sourceFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["currencyData.json"] = "cldr-core/supplemental/currencyData.json",
            ["territories-en.json"] = "cldr-localenames-full/main/en/territories.json",
            ["territories-zh.json"] = "cldr-localenames-full/main/zh/territories.json",
            ["currencies-en.json"] = "cldr-numbers-full/main/en/currencies.json",
            ["currencies-zh.json"] = "cldr-numbers-full/main/zh/currencies.json"
        };
        foreach (var source in sourceFiles)
            await Download(root + source.Value, Path.Combine(folder, source.Key), refresh, token);
        var licensePath = Path.Combine(folder, "unicode-license.txt");
        await Download("https://www.unicode.org/license.txt", licensePath, refresh, token);
        CopyNotice(licensePath, outputRoot, "cldr-Unicode-License.txt");
        var commitName = "commit.json";
        var commit = await DownloadJson("https://api.github.com/repos/unicode-org/cldr-json/commits/" + CldrVersion,
            Path.Combine(folder, commitName), refresh, token);
        var commitDate = (DateTimeOffset?)commit["commit"]?["committer"]?["date"] ?? throw new InvalidDataException("CLDR commit date missing.");

        var territoryEn = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "territories-en.json"), Encoding.UTF8, token));
        var territoryZh = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "territories-zh.json"), Encoding.UTF8, token));
        var currencyEn = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "currencies-en.json"), Encoding.UTF8, token));
        var currencyZh = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "currencies-zh.json"), Encoding.UTF8, token));
        var currencyData = JObject.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "currencyData.json"), Encoding.UTF8, token));
        var namesEn = territoryEn["main"]?["en"]?["localeDisplayNames"]?["territories"] as JObject ?? throw new InvalidDataException("CLDR English territory names are missing.");
        var namesZh = territoryZh["main"]?["zh"]?["localeDisplayNames"]?["territories"] as JObject ?? throw new InvalidDataException("CLDR Chinese territory names are missing.");
        var currEn = currencyEn["main"]?["en"]?["numbers"]?["currencies"] as JObject ?? throw new InvalidDataException("CLDR English currency names are missing.");
        var currZh = currencyZh["main"]?["zh"]?["numbers"]?["currencies"] as JObject ?? throw new InvalidDataException("CLDR Chinese currency names are missing.");
        var fractions = currencyData["supplemental"]?["currencyData"]?["fractions"] as JObject ?? throw new InvalidDataException("CLDR currency fractions are missing.");
        var regionCurrencies = currencyData["supplemental"]?["currencyData"]?["region"] as JObject ?? throw new InvalidDataException("CLDR currency regions are missing.");
        var records = new List<ImportedRecord>();

        var regionCodes = new SortedSet<string>(namesEn.Properties().Select(p => p.Name), StringComparer.Ordinal);
        regionCodes.UnionWith(regionCurrencies.Properties().Select(p => p.Name));
        foreach (var regionCode in regionCodes)
        {
            var nameEn = NullIfEmpty((string?)namesEn[regionCode]);
            var nameZh = NullIfEmpty((string?)namesZh[regionCode]);
            var payload = new JObject
            {
                ["code"] = regionCode,
                ["nameEn"] = nameEn == null ? JValue.CreateNull() : new JValue(nameEn),
                ["nameZh"] = nameZh == null ? JValue.CreateNull() : new JValue(nameZh),
                ["nameEnMissing"] = nameEn == null,
                ["nameZhMissing"] = nameZh == null
            };
            records.Add(new ImportedRecord("region|" + regionCode, "region", regionCode, nameEn ?? regionCode, payload));
        }

        var currencyCodes = new SortedSet<string>(currEn.Properties().Select(p => p.Name), StringComparer.Ordinal);
        currencyCodes.UnionWith(currZh.Properties().Select(p => p.Name));
        var activeCurrencies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in regionCurrencies.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (region.Value is not JArray entries) continue;
            var historyIndex = 0;
            foreach (var item in entries.OfType<JObject>())
            {
                foreach (var currency in item.Properties())
                {
                    var code = currency.Name;
                    currencyCodes.Add(code);
                    var payload = (JObject)currency.Value.DeepClone();
                    payload["region"] = region.Name;
                    payload["currencyCode"] = code;
                    payload["historyIndex"] = historyIndex;
                    records.Add(new ImportedRecord("region-currency|" + region.Name + "|" + code + "|" + historyIndex,
                        "region-currency", region.Name, code, payload));
                    if ((string?)payload["_to"] == null) activeCurrencies.Add(code);
                }
                historyIndex++;
            }
        }

        foreach (var code in currencyCodes)
        {
            var fraction = fractions[code] as JObject ?? fractions["DEFAULT"] as JObject;
            var nameEn = NullIfEmpty((string?)currEn[code]?["displayName"]);
            var nameZh = NullIfEmpty((string?)currZh[code]?["displayName"]);
            var payload = new JObject
            {
                ["code"] = code,
                ["nameEn"] = nameEn == null ? JValue.CreateNull() : new JValue(nameEn),
                ["symbolEn"] = currEn[code]?["symbol"]?.DeepClone(),
                ["nameZh"] = nameZh == null ? JValue.CreateNull() : new JValue(nameZh),
                ["symbolZh"] = currZh[code]?["symbol"]?.DeepClone(),
                ["digits"] = fraction?["_digits"]?.DeepClone() ?? new JValue("2"),
                ["rounding"] = fraction?["_rounding"]?.DeepClone() ?? new JValue("0"),
                ["isCurrent"] = activeCurrencies.Contains(code),
                ["nameEnMissing"] = nameEn == null,
                ["nameZhMissing"] = nameZh == null
            };
            records.Add(new ImportedRecord("currency|" + code, "currency", null, nameEn ?? code, payload));
        }

        var inputs = sourceFiles.Keys.Concat(new[] { "unicode-license.txt", commitName }).ToArray();
        return WriteCatalog(outputRoot, "cldr", CldrImportVersion, "https://github.com/unicode-org/cldr-json/tree/" + CldrVersion,
            "Unicode-3.0", "CLDR territories/currencies; active and historic currency metadata; region-currency references; missing localized names flagged", folder, inputs, records,
            snapshotDate: commitDate.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), token: token);
    }

    private static async Task<CatalogOutput> ImportPci(string rawRoot, string outputRoot, bool refresh, CancellationToken token)
    {
        var folder = Path.Combine(rawRoot, "pciids", PciCommit);
        Directory.CreateDirectory(folder);
        var root = "https://raw.githubusercontent.com/pciutils/pciids/" + PciCommit + "/";
        var idsPath = Path.Combine(folder, "pci.ids");
        var readmePath = Path.Combine(folder, "README.md");
        await Download(root + "pci.ids", idsPath, refresh, token);
        await Download(root + "README.md", readmePath, refresh, token);
        CopyNotice(readmePath, outputRoot, "pciids-README.md");
        var records = EnumeratePci(idsPath);
        var dateLine = File.ReadLines(idsPath, Encoding.UTF8)
            .Select(line => line.TrimStart('#').TrimStart(' ', '\t'))
            .FirstOrDefault(line => line.StartsWith("Date:", StringComparison.Ordinal));
        var dateText = dateLine == null ? null : dateLine.Substring("Date:".Length).Trim();
        if (!DateTimeOffset.TryParse(dateText, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var sourceDate))
            throw new InvalidDataException("PCI database has no parseable source date.");
        var sha = Sha256(idsPath);
        return WriteCatalog(outputRoot, "pci-ids", PciCommit, "https://github.com/pciutils/pciids/tree/" + PciCommit,
            "BSD-3-Clause OR GPL-2.0-or-later (BSD-3-Clause selected)", "PCI vendors, devices, subsystems and classes from upstream database",
            folder, new[] { "pci.ids", "README.md" }, records, sha,
            sourceDate.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), token);
    }

    private static async Task<CatalogOutput> ImportOurAirports(string rawRoot, string outputRoot, bool refresh, CancellationToken token)
    {
        var folder = Path.Combine(rawRoot, "ourairports", AirportsCommit);
        Directory.CreateDirectory(folder);
        var root = "https://raw.githubusercontent.com/davidmegginson/ourairports-data/" + AirportsCommit + "/";
        var files = new[] { "airports.csv", "airport-frequencies.csv", "runways.csv", "navaids.csv", "countries.csv", "regions.csv" };
        foreach (var file in files) await Download(root + file, Path.Combine(folder, file), refresh, token);
        var termsPath = Path.Combine(folder, "open-data.html");
        await Download("https://ourairports.com/data/", termsPath, refresh, token);
        CopyNotice(termsPath, outputRoot, "ourairports-public-domain.html");
        var airportCountries = new Dictionary<string, string>(StringComparer.Ordinal);
        var airportCountriesByIdent = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in ReadCsv(Path.Combine(folder, "airports.csv")))
        {
            var id = Required(row, "id");
            var country = NullIfEmpty(Get(row, "iso_country"));
            if (airportCountries.ContainsKey(id)) throw new InvalidDataException($"OurAirports airport id is duplicated: {id}");
            airportCountries.Add(id, country ?? string.Empty);
            var ident = NullIfEmpty(Get(row, "ident"));
            if (ident != null)
            {
                if (airportCountriesByIdent.TryGetValue(ident, out var existing) && existing != (country ?? string.Empty))
                    throw new InvalidDataException($"OurAirports airport identifier maps to multiple countries: {ident}");
                airportCountriesByIdent[ident] = country ?? string.Empty;
            }
        }
        var commit = await DownloadJson("https://api.github.com/repos/davidmegginson/ourairports-data/commits/" + AirportsCommit,
            Path.Combine(folder, "commit.json"), refresh, token);
        var commitDate = (DateTimeOffset?)commit["commit"]?["committer"]?["date"] ?? throw new InvalidDataException("OurAirports commit date missing.");
        var sha = AggregateChecksum(folder, files.Append("open-data.html"));
        return WriteCatalog(outputRoot, "ourairports", "git:" + AirportsCommit, "https://github.com/davidmegginson/ourairports-data/tree/" + AirportsCommit,
            "Public Domain", "Global airports, runways, frequencies, navaids, countries and regions; airlines/routes excluded", folder,
            files.Append("open-data.html").Append("commit.json").ToArray(), EnumerateOurAirports(folder, airportCountries, airportCountriesByIdent), sha,
            commitDate.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), token);
    }

    private static async Task<CatalogOutput> ImportPhoneMetadata(string rawRoot, string outputRoot, bool refresh, CancellationToken token)
    {
        var folder = Path.Combine(rawRoot, "libphonenumber", PhoneMetadataCommit);
        Directory.CreateDirectory(folder);
        var root = "https://raw.githubusercontent.com/google/libphonenumber/" + PhoneMetadataCommit + "/";
        var metadataName = "PhoneNumberMetadata.xml";
        var licenseName = "LICENSE";
        var commitName = "commit.json";
        await Download(root + "resources/" + metadataName, Path.Combine(folder, metadataName), refresh, token);
        await Download(root + licenseName, Path.Combine(folder, licenseName), refresh, token);
        CopyNotice(Path.Combine(folder, licenseName), outputRoot, "libphonenumber-Apache-2.0-LICENSE.txt");
        var commit = await DownloadJson("https://api.github.com/repos/google/libphonenumber/commits/" + PhoneMetadataCommit,
            Path.Combine(folder, commitName), refresh, token);
        var commitDate = (DateTimeOffset?)commit["commit"]?["committer"]?["date"] ?? throw new InvalidDataException("libphonenumber commit date missing.");
        var license = await File.ReadAllTextAsync(Path.Combine(folder, licenseName), Encoding.UTF8, token);
        if (!license.Contains("Apache License", StringComparison.Ordinal) || !license.Contains("Version 2.0", StringComparison.Ordinal))
            throw new InvalidDataException("libphonenumber license is not the expected Apache-2.0 text.");

        var document = XDocument.Load(Path.Combine(folder, metadataName), LoadOptions.None);
        var records = EnumeratePhoneMetadata(document);
        var inputs = new[] { metadataName, licenseName, commitName };
        return WriteCatalog(outputRoot, "libphonenumber", "git:" + PhoneMetadataCommit,
            "https://github.com/google/libphonenumber/tree/" + PhoneMetadataCommit,
            "Apache-2.0", "Phone numbering patterns and calling codes by region; format metadata only, not allocation evidence",
            folder, inputs, records, snapshotDate: commitDate.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), token: token);
    }

    private static async Task<CatalogOutput> ImportGeoNames(string rawRoot, string outputRoot, bool refresh, CancellationToken token, bool postal)
    {
        var dataset = postal ? "postal" : "cities15000";
        var directoryName = postal ? "zip" : "dump";
        var sourceRoot = "https://download.geonames.org/export/" + directoryName + "/";
        var folder = Path.Combine(rawRoot, "geonames", dataset);
        Directory.CreateDirectory(folder);
        var archiveName = postal ? "allCountries.zip" : "cities15000.zip";
        var textName = postal ? "allCountries.txt" : "cities15000.txt";
        var archivePath = Path.Combine(folder, archiveName);
        var modified = await Download(sourceRoot + archiveName, archivePath, refresh, token);
        var readmeName = "readme.txt";
        var readmePath = Path.Combine(folder, readmeName);
        var readmeUrl = "https://download.geonames.org/export/" + directoryName + "/readme.txt";
        await Download(readmeUrl, readmePath, refresh, token);
        var readme = await File.ReadAllTextAsync(readmePath, Encoding.UTF8, token);
        if (!readme.Contains("Creative Commons Attribution 4.0 License", StringComparison.Ordinal) ||
            !readme.Contains("https://creativecommons.org/licenses/by/4.0/", StringComparison.Ordinal))
            throw new InvalidDataException("GeoNames readme no longer confirms the expected CC BY 4.0 license.");
        CopyNotice(readmePath, outputRoot, postal ? "geonames-postal-readme.txt" : "geonames-cities-readme.txt");
        if (modified == null) throw new InvalidDataException("GeoNames source date is missing; re-run this import with --refresh.");
        var snapshotDate = modified.Value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var inputNames = new[] { archiveName, readmeName };
        var sourceUri = sourceRoot + archiveName;
        return WriteCatalog(outputRoot, postal ? "geonames-postal" : "geonames-cities", "modified:" + snapshotDate,
            sourceUri, "CC-BY-4.0", postal
                ? "All GeoNames postal-code rows; postal granularity and source accuracy retained"
                : "Global cities15000 extract; places with source feature class and population threshold, not all settlements",
            folder, inputNames, EnumerateGeoNames(archivePath, textName, postal),
            snapshotDate: snapshotDate, token: token);
    }

    private static async Task<IReadOnlyList<CatalogOutput>> ImportGeoNamesPostalShards(string rawRoot, string outputRoot, bool refresh, CancellationToken token)
    {
        const int shardCount = 2;
        const string directoryName = "zip";
        const string archiveName = "allCountries.zip";
        const string textName = "allCountries.txt";
        const string readmeName = "readme.txt";
        var sourceRoot = "https://download.geonames.org/export/" + directoryName + "/";
        var folder = Path.Combine(rawRoot, "geonames", "postal");
        Directory.CreateDirectory(folder);
        var archivePath = Path.Combine(folder, archiveName);
        var modified = await Download(sourceRoot + archiveName, archivePath, refresh, token);
        var readmePath = Path.Combine(folder, readmeName);
        await Download(sourceRoot + readmeName, readmePath, refresh, token);
        var readme = await File.ReadAllTextAsync(readmePath, Encoding.UTF8, token);
        if (!readme.Contains("Creative Commons Attribution 4.0 License", StringComparison.Ordinal) ||
            !readme.Contains("https://creativecommons.org/licenses/by/4.0/", StringComparison.Ordinal))
            throw new InvalidDataException("GeoNames readme no longer confirms the expected CC BY 4.0 license.");
        CopyNotice(readmePath, outputRoot, "geonames-postal-readme.txt");
        if (modified == null) throw new InvalidDataException("GeoNames source date is missing; re-run this import with --refresh.");
        var snapshotDate = modified.Value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var inputNames = new[] { archiveName, readmeName };
        var sourceUri = sourceRoot + archiveName;
        var outputs = new List<CatalogOutput>(shardCount);
        for (var shard = 0; shard < shardCount; shard++)
        {
            token.ThrowIfCancellationRequested();
            var shardIndex = shard;
            outputs.Add(WriteCatalog(outputRoot, "geonames-postal-" + shard, "modified:" + snapshotDate + ":shard-" + shard + "-of-" + shardCount,
                sourceUri, "CC-BY-4.0", $"All GeoNames postal-code rows, source row modulo {shardCount} equals {shard}; postal granularity and accuracy retained",
                folder, inputNames, EnumerateGeoNames(archivePath, textName, postal: true, shardIndex, shardCount),
                snapshotDate: snapshotDate, token: token));
        }
        return outputs;
    }

    private static IEnumerable<ImportedRecord> EnumerateGeoNames(string archivePath, string entryName, bool postal, int? shard = null, int shardCount = 1)
    {
        long sourceRow = 0;
        foreach (var line in ReadZipLines(archivePath, entryName))
        {
            sourceRow++;
            if (shard.HasValue && sourceRow % shardCount != shard.Value) continue;
            var columns = line.Split('\t');
            if (postal)
            {
                if (columns.Length < 12) throw new InvalidDataException($"GeoNames postal row {sourceRow} has {columns.Length} fields; expected 12.");
                var country = RequiredColumn(columns, 0, "countryCode", sourceRow);
                var code = RequiredColumn(columns, 1, "postalCode", sourceRow);
                var placeName = string.IsNullOrWhiteSpace(columns[2]) ? code : columns[2];
                var latitude = ParseCoordinate(columns[9], -90m, 90m, "latitude", sourceRow);
                var longitude = ParseCoordinate(columns[10], -180m, 180m, "longitude", sourceRow);
                int? accuracy = null;
                if (!string.IsNullOrWhiteSpace(columns[11]))
                {
                    if (!int.TryParse(columns[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAccuracy))
                        throw new InvalidDataException($"GeoNames postal row {sourceRow} has invalid accuracy.");
                    accuracy = parsedAccuracy;
                }
                var payload = new JObject
                {
                    ["countryCode"] = country,
                    ["postalCode"] = code,
                    ["latitude"] = latitude.HasValue ? new JValue(latitude.Value) : JValue.CreateNull(),
                    ["longitude"] = longitude.HasValue ? new JValue(longitude.Value) : JValue.CreateNull(),
                    ["accuracy"] = accuracy.HasValue ? new JValue(accuracy.Value) : JValue.CreateNull(),
                    ["placeName"] = placeName,
                    ["adminName1"] = NullIfEmpty(columns[3]), ["adminCode1"] = NullIfEmpty(columns[4]),
                    ["adminName2"] = NullIfEmpty(columns[5]), ["adminCode2"] = NullIfEmpty(columns[6]),
                    ["adminName3"] = NullIfEmpty(columns[7]), ["adminCode3"] = NullIfEmpty(columns[8]),
                    ["sourceRow"] = sourceRow
                };
                var key = "postal|" + country + "|" + code + "|" + sourceRow.ToString("D10", CultureInfo.InvariantCulture);
                yield return new ImportedRecord(key, "postal", country, placeName, payload);
            }
            else
            {
                if (columns.Length < 19) throw new InvalidDataException($"GeoNames city row {sourceRow} has {columns.Length} fields; expected 19.");
                var id = RequiredColumn(columns, 0, "geonameId", sourceRow);
                var country = RequiredColumn(columns, 8, "countryCode", sourceRow);
                var placeName = string.IsNullOrWhiteSpace(columns[1]) ? id : columns[1];
                var latitude = ParseCoordinate(columns[4], -90m, 90m, "latitude", sourceRow);
                var longitude = ParseCoordinate(columns[5], -180m, 180m, "longitude", sourceRow);
                var payload = new JObject
                {
                    ["countryCode"] = country,
                    ["postalCode"] = JValue.CreateNull(),
                    ["latitude"] = latitude.HasValue ? new JValue(latitude.Value) : JValue.CreateNull(),
                    ["longitude"] = longitude.HasValue ? new JValue(longitude.Value) : JValue.CreateNull(),
                    ["accuracy"] = JValue.CreateNull(),
                    ["placeName"] = placeName,
                    ["asciiName"] = NullIfEmpty(columns[2]), ["alternateNames"] = NullIfEmpty(columns[3]),
                    ["featureClass"] = columns[6], ["featureCode"] = columns[7],
                    ["countryCode2"] = NullIfEmpty(columns[9]), ["admin1Code"] = NullIfEmpty(columns[10]),
                    ["admin2Code"] = NullIfEmpty(columns[11]), ["admin3Code"] = NullIfEmpty(columns[12]),
                    ["admin4Code"] = NullIfEmpty(columns[13]), ["population"] = ParseOptionalLong(columns[14], sourceRow),
                    ["elevation"] = NullIfEmpty(columns[15]), ["dem"] = NullIfEmpty(columns[16]),
                    ["timeZone"] = NullIfEmpty(columns[17]), ["modificationDate"] = NullIfEmpty(columns[18])
                };
                yield return new ImportedRecord("city|" + id, "city", country, placeName, payload);
            }
        }
    }

    private static IEnumerable<string> ReadZipLines(string archivePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.GetEntry(entryName) ?? throw new InvalidDataException($"GeoNames archive is missing {entryName}.");
        if (entry.Length > 1024L * 1024 * 1024) throw new InvalidDataException("GeoNames uncompressed entry exceeds 1 GiB.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 64 * 1024);
        string? line;
        while ((line = reader.ReadLine()) != null) yield return line;
    }

    private static string RequiredColumn(string[] columns, int index, string name, long row)
    {
        var value = index < columns.Length ? columns[index] : string.Empty;
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"GeoNames row {row} is missing {name}.") : value;
    }

    private static decimal? ParseCoordinate(string value, decimal min, decimal max, string name, long row)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || result < min || result > max)
            throw new InvalidDataException($"GeoNames row {row} has invalid {name}.");
        return result;
    }

    private static long? ParseOptionalLong(string value, long row)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new InvalidDataException($"GeoNames city row {row} has invalid population.");
        return result;
    }

    private static CatalogOutput WriteCatalog(string outputRoot, string id, string version, string sourceUri, string license,
        string coverage, string rawFolder, IEnumerable<string> inputNames, IEnumerable<ImportedRecord> records,
        string? sourceSha = null, string? snapshotDate = null, CancellationToken token = default)
    {
        var names = inputNames.ToArray();
        var inputs = names.Select(name => Path.Combine(rawFolder, name)).ToArray();
        sourceSha ??= AggregateChecksum(rawFolder, names);
        var outputPath = Path.Combine(outputRoot, id + ".sqlite3");
        DataSetInfo info;
        long duplicateCount;
        var kindCounts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        using (var writer = new CatalogDatabaseBuilder(outputPath))
        {
            foreach (var record in records)
            {
                token.ThrowIfCancellationRequested();
                if (writer.Add(record.Key, record.Kind, record.CountryCode, record.Name, record.Payload.ToString(Formatting.None)))
                {
                    kindCounts.TryGetValue(record.Kind, out var kindCount);
                    kindCounts[record.Kind] = kindCount + 1;
                }
            }
            info = writer.Complete((count, duplicates) => new DataSetInfo(id, version, sourceUri, license,
                snapshotDate ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                count, sourceSha, coverage + $"; excluded duplicate keys: {duplicates}"));
            duplicateCount = writer.DuplicateCount;
        }
        return new CatalogOutput(info, outputPath, Sha256(outputPath), new FileInfo(outputPath).Length,
            inputs.Select(path => new InputInfo(Path.GetFileName(path), Sha256(path), new FileInfo(path).Length)).ToArray(),
            kindCounts, duplicateCount);
    }

    private static void WriteManifest(string outputRoot, IEnumerable<CatalogOutput> catalogs)
    {
        var manifestPath = Path.Combine(outputRoot, "catalogs.manifest.json");
        var byId = new SortedDictionary<string, JObject>(StringComparer.Ordinal);
        if (File.Exists(manifestPath))
        {
            var previous = JObject.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
            if (previous["catalogs"] is JArray existing)
            {
                foreach (var item in existing.OfType<JObject>())
                    if (item.Value<string>("Id") is string id) byId[id] = (JObject)item.DeepClone();
            }
        }
        foreach (var catalog in catalogs)
        {
            var item = JObject.FromObject(new
            {
                catalog.Info.Id,
                catalog.Info.Version,
                catalog.Info.SourceUri,
                catalog.Info.License,
                catalog.Info.SnapshotDate,
                catalog.Info.RecordCount,
                catalog.Info.SourceSha256,
                catalog.Info.Coverage,
                output = Path.GetFileName(catalog.OutputPath),
                outputSha256 = catalog.OutputSha256,
                outputBytes = catalog.OutputBytes,
                kindCounts = catalog.KindCounts,
                excludedDuplicateKeys = catalog.DuplicateCount,
                inputFiles = catalog.Inputs
            });
            byId[catalog.Info.Id] = item;
        }
        var noticesDirectory = Path.Combine(outputRoot, "notices");
        var notices = Directory.Exists(noticesDirectory)
            ? Directory.GetFiles(noticesDirectory).OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(path => new { file = Path.GetFileName(path), sha256 = Sha256(path), bytes = new FileInfo(path).Length }).ToArray()
            : Array.Empty<object>();
        var data = new JObject
        {
            ["formatVersion"] = SqliteMockCatalog.FileFormatVersion,
            ["generatedAtUtc"] = DateTimeOffset.UtcNow,
            ["catalogs"] = new JArray(byId.Values),
            ["notices"] = JArray.FromObject(notices)
        };
        File.WriteAllText(manifestPath, JsonConvert.SerializeObject(data, Formatting.Indented) + Environment.NewLine,
            new UTF8Encoding(false));
    }

    private static void CopyNotice(string sourcePath, string outputRoot, string destinationName)
    {
        var directory = Path.Combine(outputRoot, "notices");
        Directory.CreateDirectory(directory);
        File.Copy(sourcePath, Path.Combine(directory, destinationName), overwrite: true);
    }

    private static ImportedRecord CsvRecord(string key, string kind, string? country, Dictionary<string, string> row, string nameField, string fallback)
    {
        var payload = new JObject();
        foreach (var pair in row) payload[pair.Key] = pair.Value;
        return new ImportedRecord(key, kind, country, NullIfEmpty(Get(row, nameField)) ?? fallback, payload);
    }

    private static IEnumerable<ImportedRecord> EnumerateMime(JObject data)
    {
        foreach (var mime in data.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (mime.Value["extensions"] is not JArray extensions) continue;
            foreach (var token in extensions)
            {
                var extension = (string?)token;
                if (string.IsNullOrWhiteSpace(extension)) continue;
                extension = extension.Trim().ToLowerInvariant();
                var mimeType = mime.Name.ToLowerInvariant();
                var payload = new JObject
                {
                    ["extension"] = extension,
                    ["mimeType"] = mimeType,
                    ["source"] = mime.Value["source"]?.DeepClone(),
                    ["compressible"] = mime.Value["compressible"]?.DeepClone(),
                    ["charset"] = mime.Value["charset"]?.DeepClone()
                };
                yield return new ImportedRecord("file|" + extension + "|" + mimeType, "mime-type", null, mimeType, payload);
            }
        }
    }

    private static IEnumerable<ImportedRecord> EnumerateOurAirports(string folder, Dictionary<string, string> airportCountries,
        Dictionary<string, string> airportCountriesByIdent)
    {
        foreach (var row in ReadCsv(Path.Combine(folder, "airports.csv")))
        {
            var id = Required(row, "id");
            yield return CsvRecord("airport|" + id, "airport", NullIfEmpty(Get(row, "iso_country")), row, "name", id);
        }
        foreach (var row in ReadCsv(Path.Combine(folder, "airport-frequencies.csv")))
        {
            var id = Required(row, "id");
            var airportRef = Required(row, "airport_ref");
            if (!airportCountries.TryGetValue(airportRef, out var country))
                throw new InvalidDataException($"OurAirports frequency {id} references missing airport {airportRef}.");
            yield return CsvRecord("frequency|" + id, "frequency", NullIfEmpty(country), row, "description", id);
        }
        foreach (var row in ReadCsv(Path.Combine(folder, "runways.csv")))
        {
            var id = Required(row, "id");
            var airportRef = Required(row, "airport_ref");
            if (!airportCountries.TryGetValue(airportRef, out var country))
                throw new InvalidDataException($"OurAirports runway {id} references missing airport {airportRef}.");
            yield return CsvRecord("runway|" + id, "runway", NullIfEmpty(country), row, "surface", id);
        }
        foreach (var row in ReadCsv(Path.Combine(folder, "navaids.csv")))
        {
            var id = Required(row, "id");
            var airportRef = NullIfEmpty(Get(row, "associated_airport"));
            string? country = null;
            if (airportRef != null && !airportCountriesByIdent.TryGetValue(airportRef, out country))
                throw new InvalidDataException($"OurAirports navaid {id} references missing airport {airportRef}.");
            yield return CsvRecord("navaid|" + id, "navaid", NullIfEmpty(country), row, "name", id);
        }
        foreach (var row in ReadCsv(Path.Combine(folder, "countries.csv")))
        {
            var code = Required(row, "code");
            yield return CsvRecord("country|" + code, "country", code, row, "name", code);
        }
        var countryCodes = new HashSet<string>(ReadCsv(Path.Combine(folder, "countries.csv"))
            .Select(row => Required(row, "code")), StringComparer.Ordinal);
        foreach (var row in ReadCsv(Path.Combine(folder, "regions.csv")))
        {
            var code = Required(row, "code");
            var country = NullIfEmpty(Get(row, "iso_country"));
            if (country != null && !countryCodes.Contains(country))
                throw new InvalidDataException($"OurAirports region {code} references missing country {country}.");
            yield return CsvRecord("region|" + code, "region", country, row, "name", code);
        }
    }

    private static IEnumerable<ImportedRecord> EnumeratePci(string path)
    {
        string? vendorId = null;
        string? deviceId = null;
        string? classId = null;
        string? subclassId = null;
        var inClassSection = false;

        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            if (raw.Length == 0 || raw[0] == '#') continue;
            if (raw.StartsWith("C ", StringComparison.Ordinal))
            {
                var fields = SplitFields(raw.Substring(2));
                if (fields.Length < 2 || !IsHex(fields[0]) || fields[0].Length != 2)
                    throw new InvalidDataException("PCI class row has an invalid identifier.");
                inClassSection = true;
                vendorId = null;
                deviceId = null;
                classId = fields[0].ToLowerInvariant();
                subclassId = null;
                var name = JoinFields(fields, 1);
                yield return new ImportedRecord("class|" + classId, "class", null, name,
                    new JObject { ["classId"] = classId, ["name"] = name });
                continue;
            }

            var depth = 0;
            while (depth < raw.Length && raw[depth] == '\t') depth++;
            var fieldsAtDepth = SplitFields(raw.Substring(depth));
            if (fieldsAtDepth.Length == 0) continue;

            if (depth == 0)
            {
                if (fieldsAtDepth[0].Length != 4 || !IsHex(fieldsAtDepth[0])) continue;
                inClassSection = false;
                vendorId = fieldsAtDepth[0].ToLowerInvariant();
                deviceId = null;
                classId = subclassId = null;
                var name = JoinFields(fieldsAtDepth, 1);
                yield return new ImportedRecord("vendor|" + vendorId, "vendor", null, name,
                    new JObject { ["vendorId"] = vendorId, ["name"] = name });
                continue;
            }

            if (inClassSection)
            {
                if (depth == 1 && classId != null && fieldsAtDepth[0].Length == 2 && IsHex(fieldsAtDepth[0]))
                {
                    subclassId = fieldsAtDepth[0].ToLowerInvariant();
                    var name = JoinFields(fieldsAtDepth, 1);
                    yield return new ImportedRecord("subclass|" + classId + "|" + subclassId, "subclass", null, name,
                        new JObject { ["classId"] = classId, ["subclassId"] = subclassId, ["name"] = name });
                }
                else if (depth == 2 && classId != null && subclassId != null && fieldsAtDepth[0].Length == 2 && IsHex(fieldsAtDepth[0]))
                {
                    var interfaceId = fieldsAtDepth[0].ToLowerInvariant();
                    var name = JoinFields(fieldsAtDepth, 1);
                    yield return new ImportedRecord("interface|" + classId + "|" + subclassId + "|" + interfaceId, "interface", null, name,
                        new JObject { ["classId"] = classId, ["subclassId"] = subclassId, ["interfaceId"] = interfaceId, ["name"] = name });
                }
                continue;
            }

            if (vendorId == null) continue;
            if (depth == 1 && fieldsAtDepth[0].Length == 4 && IsHex(fieldsAtDepth[0]) && fieldsAtDepth.Length >= 2)
            {
                deviceId = fieldsAtDepth[0].ToLowerInvariant();
                var name = JoinFields(fieldsAtDepth, 1);
                yield return new ImportedRecord("device|" + vendorId + "|" + deviceId, "device", null, name,
                    new JObject { ["vendorId"] = vendorId, ["deviceId"] = deviceId, ["name"] = name });
            }
            else if (depth == 2 && deviceId != null && fieldsAtDepth.Length >= 3 && fieldsAtDepth[0].Length == 4 && IsHex(fieldsAtDepth[0]) &&
                     fieldsAtDepth[1].Length == 4 && IsHex(fieldsAtDepth[1]))
            {
                var subvendorId = fieldsAtDepth[0].ToLowerInvariant();
                var subdeviceId = fieldsAtDepth[1].ToLowerInvariant();
                var name = JoinFields(fieldsAtDepth, 2);
                yield return new ImportedRecord("subsystem|" + vendorId + "|" + deviceId + "|" + subvendorId + "|" + subdeviceId, "subsystem", null, name,
                    new JObject { ["vendorId"] = vendorId, ["deviceId"] = deviceId, ["subvendorId"] = subvendorId,
                        ["subdeviceId"] = subdeviceId, ["name"] = name });
            }
        }
    }

    private static IEnumerable<ImportedRecord> EnumeratePhoneMetadata(XDocument document)
    {
        var territories = document.Root?.Element("territories") ?? throw new InvalidDataException("Phone metadata has no territories element.");
        foreach (var territory in territories.Elements("territory"))
        {
            var countryCode = (string?)territory.Attribute("id");
            var callingCode = (string?)territory.Attribute("countryCode");
            if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(callingCode)) continue;
            foreach (var type in new[] { (Name: "mobile", Element: "mobile"), (Name: "fixedline", Element: "fixedLine") })
            {
                var numberType = territory.Element(type.Element);
                var sourcePattern = numberType?.Element("nationalNumberPattern")?.Value;
                var pattern = sourcePattern == null ? null : CompactPattern(sourcePattern);
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                var prefix = (string?)territory.Attribute("nationalPrefix") ?? string.Empty;
                var formats = new JArray();
                foreach (var format in territory.Element("availableFormats")?.Elements("numberFormat") ?? Enumerable.Empty<XElement>())
                {
                    var formatPattern = (string?)format.Attribute("pattern");
                    var replacement = format.Element("format")?.Value;
                    if (string.IsNullOrWhiteSpace(formatPattern) || string.IsNullOrWhiteSpace(replacement)) continue;
                    var leadingDigits = format.Elements("leadingDigits").LastOrDefault()?.Value ?? string.Empty;
                    formats.Add(new JObject
                    {
                        ["pattern"] = CompactPattern(formatPattern),
                        ["replacement"] = replacement,
                        ["leadingDigits"] = CompactPattern(leadingDigits),
                        ["nationalPrefixRule"] = (string?)format.Attribute("nationalPrefixFormattingRule") ??
                            (string?)territory.Attribute("nationalPrefixFormattingRule") ?? string.Empty
                    });
                }
                var payload = new JObject
                {
                    ["countryCode"] = countryCode,
                    ["callingCode"] = callingCode,
                    ["type"] = type.Name,
                    ["nationalPattern"] = pattern,
                    ["sourcePattern"] = sourcePattern,
                    ["nationalPrefix"] = prefix,
                    ["formats"] = formats
                };
                var key = countryCode == "001"
                    ? countryCode + ":" + type.Name + ":" + callingCode
                    : countryCode.ToUpperInvariant() + ":" + type.Name;
                yield return new ImportedRecord(key, "phone-pattern",
                    countryCode.ToUpperInvariant(), countryCode.ToUpperInvariant() + " " + type.Name, payload);
            }
        }
    }

    private static string[] SplitFields(string value) => value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    private static string CompactPattern(string value) => new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static string JoinFields(string[] fields, int start) => string.Join(" ", fields.Skip(start));

    private static async Task<JObject> DownloadJson(string url, string path, bool refresh, CancellationToken token)
    {
        await Download(url, path, refresh, token);
        return JObject.Parse(await File.ReadAllTextAsync(path, Encoding.UTF8, token));
    }

    private static async Task<DateTimeOffset?> Download(string url, string path, bool refresh, CancellationToken token)
    {
        var metadataPath = path + ".last-modified";
        if (!refresh && File.Exists(path) && new FileInfo(path).Length > 0)
        {
            if (File.Exists(metadataPath) && DateTimeOffset.TryParse(File.ReadAllText(metadataPath, Encoding.UTF8),
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var savedDate)) return savedDate;
            return null;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".download";
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var lastModified = response.Content.Headers.LastModified;
        await using (var source = await response.Content.ReadAsStreamAsync(token))
        await using (var destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            await source.CopyToAsync(destination, token);
        if (File.Exists(path)) File.Delete(path);
        File.Move(temporary, path);
        if (lastModified.HasValue) File.WriteAllText(metadataPath, lastModified.Value.ToString("O", CultureInfo.InvariantCulture), new UTF8Encoding(false));
        return lastModified;
    }

    private static void ExtractTarEntry(string archivePath, string relativePath, string outputPath)
    {
        using var file = File.OpenRead(archivePath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) != null)
        {
            if (!string.Equals(entry.Name.Replace('\\', '/'), relativePath, StringComparison.Ordinal) || entry.DataStream == null) continue;
            using var output = File.Create(outputPath);
            entry.DataStream.CopyTo(output);
            return;
        }
        throw new InvalidDataException($"npm archive is missing {relativePath}.");
    }

    private static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        var header = ReadCsvRow(reader);
        if (header == null) yield break;
        List<string>? row;
        while ((row = ReadCsvRow(reader)) != null)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < header.Count; i++) result[header[i]] = i < row.Count ? row[i] : string.Empty;
            yield return result;
        }
    }

    private static List<string>? ReadCsvRow(TextReader reader)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var sawAny = false;
        while (true)
        {
            var value = reader.Read();
            if (value < 0)
            {
                if (!sawAny && fields.Count == 0 && field.Length == 0) return null;
                fields.Add(field.ToString());
                return fields;
            }
            sawAny = true;
            var c = (char)value;
            if (quoted)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                    else quoted = false;
                }
                else field.Append(c);
                continue;
            }
            if (c == '"' && field.Length == 0) { quoted = true; continue; }
            if (c == ',') { fields.Add(field.ToString()); field.Clear(); continue; }
            if (c == '\n') { fields.Add(field.ToString()); return fields; }
            if (c == '\r')
            {
                if (reader.Peek() == '\n') reader.Read();
                fields.Add(field.ToString());
                return fields;
            }
            field.Append(c);
        }
    }

    private static string Required(Dictionary<string, string> row, string name)
    {
        var value = Get(row, name);
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"Source row is missing stable key field '{name}'.") : value;
    }

    private static string Get(Dictionary<string, string> row, string name) => row.TryGetValue(name, out var value) ? value : string.Empty;
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool IsHex(string value) => value.All(c => Uri.IsHexDigit(c));

    private static string AggregateChecksum(string folder, IEnumerable<string> names)
    {
        var builder = new StringBuilder();
        foreach (var name in names.OrderBy(x => x, StringComparer.Ordinal))
            builder.Append(name).Append('\n').Append(Sha256(Path.Combine(folder, name))).Append('\n');
        return Sha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var hash = SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string Sha256(byte[] value)
    {
        using var hash = SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(value)).ToLowerInvariant();
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        if (index < 0) return null;
        if (index + 1 >= args.Length) throw new ArgumentException($"Missing value for {name}.");
        return Path.GetFullPath(args[index + 1]);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Bing.MockData.CatalogImport", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project tools/CatalogImport -- <all-basic|geonames|mime-db|cldr|pci|ourairports|phone-metadata|geonames-city|geonames-postal> [--raw PATH] [--output PATH] [--refresh]");
        Console.WriteLine("Imports fixed, documented snapshots. Network access is used only by this tool; runtime catalogs are offline.");
    }

    private sealed record ImportedRecord(string Key, string Kind, string? CountryCode, string Name, JObject Payload);
    private sealed record InputInfo(string File, string Sha256, long Bytes);
    private sealed record CatalogOutput(DataSetInfo Info, string OutputPath, string OutputSha256, long OutputBytes, InputInfo[] Inputs,
        IReadOnlyDictionary<string, long> KindCounts, long DuplicateCount);
}
