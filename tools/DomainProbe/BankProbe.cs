using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Bing.MockData;
using Bing.MockData.Catalogs;

internal static class BankProbe
{
    internal static int Run(string[] args)
    {
        int count=int.Parse(args[0]);string path=Path.GetFullPath(args[1]);string workload=args[2];
        int seconds=count<=10000?120:600;
        var options=new MockOptions{Seed=39,ReferenceTime=new DateTimeOffset(2026,10,27,0,0,0,TimeSpan.Zero)};
        if(workload=="bank-bin")options.UseCatalogFile("bank-bins",path);
        using var context=new MockContext(options);
        var generator=context.Generator(c=>workload=="bank-card"?c.Finance.BankCard():c.BankBins().Sample().Key);
        var results=new List<object>();bool success=true;
        foreach(int concurrency in new[]{1,4})
        {
            foreach(var _ in generator.Enumerate(100)){}
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            long before=GC.GetTotalAllocatedBytes(true),actual=0;var metrics=new GenerationMetrics();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
            var timer=Stopwatch.StartNew();string status="PASS";
            try
            {
                foreach(var _ in concurrency==1?generator.Enumerate(count,timeout.Token):generator.EnumerateParallel(count,concurrency,cancellationToken:timeout.Token,metrics:metrics))actual++;
                if(actual!=count){status="COUNT_MISMATCH";success=false;}
            }
            catch(OperationCanceledException) when(timeout.IsCancellationRequested){status="VERIFIED_TIMEOUT_BOUNDARY";success=false;}
            timer.Stop();
            results.Add(new{workload,expected="EXPECTED_SUCCESS",status,count,actual,requestedConcurrency=concurrency,
                maxActualParallelism=concurrency==1?(actual>0?1:0):metrics.MaxActualParallelism,
                maxQueuedRequests=concurrency==1?0:metrics.MaxQueuedRecords,observationSeconds=seconds,
                milliseconds=timer.Elapsed.TotalMilliseconds,recordsPerSecond=actual/timer.Elapsed.TotalSeconds,
                allocatedBytes=GC.GetTotalAllocatedBytes(true)-before,peakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64});
        }
        Console.WriteLine(JsonSerializer.Serialize(new{runtime=Environment.Version.ToString(),os=Environment.OSVersion.ToString(),
            coreSha256=Hash(typeof(MockContext).Assembly.Location),catalogAssemblySha256=Hash(typeof(BankBinCatalog).Assembly.Location),
            context.Seed,context.ReferenceTime,contract=context.Replay.GenerationContractVersion,data=context.Replay.DataVersion,
            catalogVersion=workload=="bank-bin"?context.BankBins().Info.Version:null,results},new JsonSerializerOptions{WriteIndented=true}));
        return success?0:2;
    }
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
