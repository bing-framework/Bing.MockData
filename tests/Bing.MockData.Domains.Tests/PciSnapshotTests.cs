using System;
using System.IO;
using Bing.MockData.Catalogs;
using Bing.MockData.Devices;
using Bing.MockData.Tests.Infrastructure;
using Xunit;

namespace Bing.MockData.Domains.Tests;

public class PciSnapshotTests
{
    [Theory]
    [InlineData("pci-ids",42549)]
    [InlineData("pci-ids-resolved",42503)]
    public void Explicit_snapshot_supports_device_templates_and_releases_owned_file(string id,int count)
    {
        string path=Path.Combine(Path.GetTempPath(),"mockdata-pci-"+Guid.NewGuid().ToString("N")+".sqlite3");
        File.Copy(CatalogData.FilePath(id+".sqlite3"),path);
        try
        {
            using(var catalog=SqliteMockCatalog.Open(path))
            { Assert.Equal(id,catalog.Info.Id);Assert.Equal(count,catalog.Info.RecordCount); }
            var c=new MockContext(new MockOptions{Seed=39}.UseCatalogFile("devices",path));
            var generator=c.Devices().Device().Build();
            using(var e=generator.Enumerate(20).GetEnumerator())
            {
                Assert.True(e.MoveNext());
                Assert.StartsWith("device|"+e.Current.Manufacturer.Key.Split('|')[1]+"|",e.Current.Model.Key);
            }
            c.Dispose();c.Dispose();
            Assert.Throws<ObjectDisposedException>(()=>generator.At(1));
            using(var exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Assert.True(exclusive.Length>0);
            File.Move(path,path+".moved");File.Delete(path+".moved");
        }
        finally { if(File.Exists(path))File.Delete(path);if(File.Exists(path+".moved"))File.Delete(path+".moved"); }
    }
}
