using System.Diagnostics;
using System.Text.Json;
using Bing.MockData;
using Bing.MockData.Catalogs;

if(args.Length>2 && (args[2]=="bank-card" || args[2]=="bank-bin"))
{
    Environment.ExitCode=BankProbe.Run(args);
    return;
}

int count=args.Length>0?int.Parse(args[0]):10000;
string root=args.Length>1?Path.GetFullPath(args[1]):Path.GetFullPath("src/Bing.MockData.Catalogs/Data/mime-db.sqlite3");
string workload=args.Length>2?args[2]:"all";
if(workload!="all"&&workload!="catalog"&&workload!="timeseries"&&workload!="request-log")throw new ArgumentException("Unknown workload.");
using var catalog=SqliteMockCatalog.Open(root);
using var context=new MockContext(new MockOptions{Seed=9001,ReferenceTime=new DateTimeOffset(2026,10,25,0,0,0,TimeSpan.Zero)}.UseCatalog("probe",catalog));
var series=context.TimeSeries.Create(new TimeSeriesOptions{Trend=.01,Amplitude=3});
var logs=context.Templates.RequestLog().Build();
var sampling=context.Generator(c=>c.Catalog("probe").Sample(c.Numbers.ULong()).Key);
var results=new List<object>();
void Run<T>(string name,MockGenerator<T> generator,int concurrency)
{
    foreach(var _ in generator.Enumerate(100)){}
    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();long before=GC.GetTotalAllocatedBytes(true);var timer=Stopwatch.StartNew();var metrics=new GenerationMetrics();long actual=0;
    foreach(var _ in concurrency==1?generator.Enumerate(count):generator.EnumerateParallel(count,concurrency,metrics:metrics))actual++;
    timer.Stop();results.Add(new{name,expected="EXPECTED_SUCCESS",count,actual,requestedConcurrency=concurrency,maxActualParallelism=concurrency==1?1:metrics.MaxActualParallelism,maxQueuedRequests=concurrency==1?0:metrics.MaxQueuedRecords,milliseconds=timer.Elapsed.TotalMilliseconds,recordsPerSecond=actual/timer.Elapsed.TotalSeconds,allocatedBytes=GC.GetTotalAllocatedBytes(true)-before,peakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64});
    if(actual!=count)throw new Exception("Record count mismatch.");
}
foreach(int concurrency in new[]{1,4}){if(workload=="all"||workload=="catalog")Run("catalog",sampling,concurrency);if(workload=="all"||workload=="timeseries")Run("timeseries",series,concurrency);if(workload=="all"||workload=="request-log")Run("request-log",logs,concurrency);}
Console.WriteLine(JsonSerializer.Serialize(new{runtime=Environment.Version.ToString(),os=Environment.OSVersion.ToString(),assemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(MockContext).Assembly.Location))),catalogVersion=catalog.Info.Version,results},new JsonSerializerOptions{WriteIndented=true}));
