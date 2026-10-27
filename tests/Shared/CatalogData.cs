using System;
using System.IO;

namespace Bing.MockData.Tests.Infrastructure;

internal static class CatalogData
{
    internal static string FilePath(string filename)
    {
        string configured = Environment.GetEnvironmentVariable("MOCKDATA_DATA_ROOT");
        if (configured != null)
        {
            if (string.IsNullOrWhiteSpace(configured)) throw new DirectoryNotFoundException("Explicit MOCKDATA_DATA_ROOT is empty.");
            return InDirectory(configured, filename);
        }
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var root = Path.Combine(directory.FullName, "src", "Bing.MockData.Catalogs", "Data");
            if (Directory.Exists(root)) return InDirectory(root, filename);
        }
        throw new DirectoryNotFoundException("Catalog data root was not found.");
    }

    internal static string InDirectory(string root, string filename)
    {
        var path = Path.GetFullPath(Path.Combine(root, filename));
        if (!File.Exists(path)) throw new FileNotFoundException("Required catalog missing from the selected data root.", path);
        return path;
    }
}
