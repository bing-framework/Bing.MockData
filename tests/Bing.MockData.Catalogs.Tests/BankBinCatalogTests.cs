#pragma warning disable xUnit1051
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Bing.MockData.Catalogs.Internal;
using Bing.MockData.Tests.Infrastructure;
using Newtonsoft.Json;
using Xunit;

namespace Bing.MockData.Catalogs.Tests;

public sealed class BankBinCatalogTests
{
    private static MockOptions Options() => new MockOptions{Seed=39,ReferenceTime=new DateTimeOffset(2026,10,27,0,0,0,TimeSpan.Zero)};

    [Fact]
    public void Exact_lookup_projects_source_fields_and_never_truncates_pan()
    {
        string path=Fixture();
        try
        {
            using var c=new MockContext(Options().UseCatalogFile("bank-bins",path));var bins=c.BankBins();
            Assert.Equal(4,bins.Count()); Assert.Equal("fixture-v1",bins.Info.Version);
            Assert.True(bins.TryGet("000123",out var row));Assert.Equal("000123",row.Bin);Assert.Equal("Bank A",row.Issuer);Assert.Equal("fixture-v1",row.DataVersion);
            Assert.True(bins.TryGet("4",out var unknown));Assert.Null(unknown.Issuer);Assert.Null(unknown.CountryCode);Assert.Null(unknown.Type);
            Assert.False(bins.TryGet("00012300",out _));Assert.False(bins.TryGet("9",out _));
            foreach(string invalid in new[]{"", "１２３", "123456789", "6220001234567890", " 123", "abc"})
                Assert.Throws<ArgumentException>(()=>bins.TryGet(invalid,out _));
            Assert.Throws<ArgumentException>(()=>bins.Count(new CatalogQuery{Kind="city"}));
            Assert.Throws<ArgumentOutOfRangeException>(()=>bins.Query(new CatalogQuery{PageSize=0}));
            Assert.Equal(2,bins.Count(new CatalogQuery{CountryCode="CN"}));
            Assert.Equal("bin|622000",bins.Query(new CatalogQuery{NamePrefix="Bank B",KeyPrefix="bin|6220",PageSize=1}).Single().Key);
            Assert.Throws<CatalogCoverageException>(()=>bins.Sample(new CatalogQuery{CountryCode="ZZ"}));
        }
        finally{File.Delete(path);}
    }

    [Fact]
    public void Query_copies_filters_and_all_three_cancellation_sources_remain_effective()
    {
        string path=Fixture();
        try
        {
            for(int cancelSource=0;cancelSource<3;cancelSource++)
            {
                using var rootToken=new CancellationTokenSource();using var queryToken=new CancellationTokenSource();using var argumentToken=new CancellationTokenSource();
                using var root=new MockContext(Options().UseCatalogFile("bank-bins",path));
                var c=root.ForRecord(0,rootToken.Token);var bins=c.BankBins();
                var q=new CatalogQuery{PageSize=1,CountryCode="CN",CancellationToken=queryToken.Token};
                var sequence=bins.Query(q,argumentToken.Token);q.CountryCode="ZZ";
                using var iterator=sequence.GetEnumerator();Assert.True(iterator.MoveNext());Assert.Equal("CN",iterator.Current.CountryCode);
                new[]{rootToken,queryToken,argumentToken}[cancelSource].Cancel();
                Assert.ThrowsAny<OperationCanceledException>(()=>iterator.MoveNext());
            }
            using var c2=new MockContext(Options().UseCatalogFile("bank-bins",path));
            var sequence2=c2.BankBins().Query(new CatalogQuery{PageSize=1});
            Assert.Equal(sequence2.Select(r=>r.Key),sequence2.Select(r=>r.Key));
            using var canceled=new CancellationTokenSource();canceled.Cancel();
            var q2=new CatalogQuery{CancellationToken=canceled.Token};
            Assert.ThrowsAny<OperationCanceledException>(()=>c2.BankBins().Count(q2));
            Assert.ThrowsAny<OperationCanceledException>(()=>c2.BankBins().Sample(q2));
        }
        finally{File.Delete(path);}
    }

    [Fact]
    public void Adapter_obeys_owned_borrowed_and_disposed_context_lifetimes()
    {
        string path=Fixture();
        try
        {
            using(var external=SqliteMockCatalog.Open(path))
            {
                var c=new MockContext(Options().UseCatalog("bank-bins",external));var bins=c.BankBins();
                using(var e=bins.Query().GetEnumerator())Assert.True(e.MoveNext());
                c.Dispose();c.Dispose();Assert.True(external.TryGet("bin|4",out _));
                Assert.Throws<ObjectDisposedException>(()=>bins.Count());Assert.Throws<ObjectDisposedException>(()=>bins.Info);
                Assert.Throws<ObjectDisposedException>(()=>bins.TryGet("4",out _));Assert.Throws<ObjectDisposedException>(()=>bins.Sample());
            }
            var owned=SqliteMockCatalog.Open(path);
            using(var c=new MockContext(Options().UseCatalog("bank-bins",owned,ownsResource:true))) Assert.True(c.BankBins().TryGet("4",out _));
            Assert.Throws<ObjectDisposedException>(()=>owned.GetAt(0));
            using(var exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Assert.True(exclusive.Length>0);
            string moved=path+".moved";File.Move(path,moved);File.Move(moved,path);
        }
        finally{File.Delete(path);}
    }

    [Fact]
    public void Wrong_dataset_malformed_record_and_empty_dataset_fail_explicitly()
    {
        string wrong=Fixture(id:"other"),broken=Fixture(broken:true),empty=Fixture(empty:true);
        try
        {
            using(var c=new MockContext(Options().UseCatalogFile("bank-bins",wrong)))Assert.Throws<ArgumentException>(()=>c.BankBins());
            using(var c=new MockContext(Options().UseCatalogFile("bank-bins",broken)))
            {
                var bins=c.BankBins();Assert.Throws<InvalidDataException>(()=>bins.Query().ToArray());
                Assert.True(bins.TryGet("4",out _)); // 失败的投影没有留下读取器或锁。
            }
            using(var c=new MockContext(Options().UseCatalogFile("bank-bins",empty)))
            {Assert.Equal(0,c.BankBins().Count());Assert.Empty(c.BankBins().Query());Assert.Throws<CatalogCoverageException>(()=>c.BankBins().Sample());}
        }
        finally{File.Delete(wrong);File.Delete(broken);File.Delete(empty);}
    }

    [Fact]
    public void Sample_replays_serial_parallel_and_rejects_different_directory_version()
    {
        string path=Fixture(),changed=Fixture(version:"fixture-v2");
        try
        {
            using var c=new MockContext(Options().UseCatalogFile("bank-bins",path));
            var generator=c.Generator(x=>x.BankBins().Sample().Key);var expected=generator.Enumerate(100).ToArray();
            Assert.Equal(expected,generator.Enumerate(100));Assert.Equal(expected,generator.EnumerateParallel(100,4));Assert.Equal(expected[19],generator.At(19));
            var replay=JsonConvert.DeserializeObject<ReplayInfo>(JsonConvert.SerializeObject(c.Replay));
            using var restored=new MockContext(replay.ToOptions().UseCatalogFile("bank-bins",path));
            Assert.Equal(expected,restored.Generator(x=>x.BankBins().Sample().Key).Enumerate(100));
            Assert.Throws<InvalidOperationException>(()=>new MockContext(replay.ToOptions().UseCatalogFile("bank-bins",changed)));
            Assert.All(c.Generator(x=>x.BankBins().Sample(new CatalogQuery{CountryCode="CN"})).GenerateMany(20),r=>Assert.Equal("CN",r.CountryCode));
        }
        finally{File.Delete(path);File.Delete(changed);}
    }

    [Fact]
    public void Fixed_global_snapshot_has_expected_coverage_and_streams_every_record()
    {
        using var c=new MockContext(Options().UseCatalogFile("bank-bins",CatalogData.FilePath("bank-bins.sqlite3")));
        var bins=c.BankBins(); Assert.Equal(458051,bins.Count());Assert.Equal(18389,bins.Count(new CatalogQuery{CountryCode="CN"}));
        Assert.Equal("fcc907c81cf02b12e396b0c12a8c16472b47619a-import-v1",bins.Info.Version);
        long count=0,cn=0,missingIssuer=0,missingCountry=0;var lengths=new long[9];string previous=null;
        foreach(var row in bins.Query())
        {
            Assert.True(previous==null||StringComparer.Ordinal.Compare(previous,row.Key)<0);
            Assert.Equal("bin|"+row.Bin,row.Key);previous=row.Key;count++;lengths[row.Bin.Length]++;
            if(row.CountryCode=="CN")cn++;if(row.Issuer==null)missingIssuer++;if(row.CountryCode==null)missingCountry++;
        }
        Assert.Equal(458051,count);Assert.Equal(18389,cn);
        Assert.Equal(7,lengths[5]);Assert.Equal(454496,lengths[6]);Assert.Equal(3548,lengths[8]);
        Assert.Equal(237412,missingIssuer);Assert.Equal(126175,missingCountry);
    }

    private static string Fixture(string id="bank-bins-techbuddie",string version="fixture-v1",bool broken=false,bool empty=false)
    {
        string path=Path.Combine(Path.GetTempPath(),"mock-bank-bin-"+Guid.NewGuid().ToString("N")+".sqlite3");
        using var builder=new CatalogDatabaseBuilder(path);
        if(!empty)
        foreach(var item in new[]{(Bin:"000123",Country:"US",Issuer:"Bank A"),(Bin:"4",Country:(string)null,Issuer:(string)null),(Bin:"622000",Country:"CN",Issuer:"Bank B"),(Bin:"622001",Country:"CN",Issuer:"Bank C")})
        {
            string payload=JsonConvert.SerializeObject(new {bin=broken&&item.Bin=="000123"?"bad":item.Bin,alpha_2=item.Country,issuer=item.Issuer,source="fixture",seen="2026-10-27"});
            builder.Add("bin|"+item.Bin,"bank-bin",item.Country,item.Issuer??"",payload);
        }
        builder.Complete(new DataSetInfo(id,version,"https://example.com","CC0-1.0","2026-10-27",empty?0:4,new string('a',64),"test fixture"));
        return path;
    }
}
