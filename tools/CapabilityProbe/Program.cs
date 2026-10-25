using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Bing.MockData;

int count = args.Length > 0 ? int.Parse(args[0]) : 10000;
int concurrency = args.Length > 1 ? int.Parse(args[1]) : 1;
using var mock = new MockContext(new MockOptions { Seed = 31817, ReferenceTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) });
var recipe = mock.Generator(c => new ProbeRow(c.RecordIndex, c.ForField("Name").Text.Random(12, 12), c.ForField("Value").Numbers.Int()));
foreach (var ignored in recipe.Enumerate(Math.Min(1000, count))) { }
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
long initialMemory = GC.GetTotalMemory(false), allocatedBefore = GC.GetTotalAllocatedBytes(true);
var metrics = new GenerationMetrics(); using var sink = new CountingWriter();
var timer = Stopwatch.StartNew();
var rows = concurrency == 1 ? recipe.Enumerate(count) : recipe.EnumerateParallel(count, concurrency, metrics: metrics);
MockExport.Write(rows, sink, MockExportFormat.JsonLines);
timer.Stop();
if (sink.Lines != count) throw new InvalidOperationException("输出数量不匹配。");
if (metrics.MaxActualParallelism > concurrency || metrics.MaxQueuedRecords > 256) throw new InvalidOperationException("并发或队列越界。");
Console.WriteLine(JsonSerializer.Serialize(new {
    expected = "EXPECTED_SUCCESS", records = sink.Lines, requestedConcurrency = concurrency,
    maxActualParallelism = concurrency == 1 ? 1 : metrics.MaxActualParallelism,
    maxQueuedRecords = metrics.MaxQueuedRecords, elapsedMilliseconds = timer.Elapsed.TotalMilliseconds,
    allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocatedBefore, initialManagedBytes = initialMemory,
    sampledPeakManagedBytes = sink.PeakManaged, peakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64,
    outputCharacters = sink.Characters, library = typeof(MockContext).Assembly.Location,
    runtime = Environment.Version.ToString(), seed = mock.Seed
}));

sealed record ProbeRow(long Id, string Name, int Value);
sealed class CountingWriter : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;
    public long Lines, Characters, PeakManaged;
    public override void Write(string value)
    {
        if (value == null) return; Characters += value.Length;
        foreach (char c in value) if (c == '\n') Lines++;
        if (Lines % 10000 == 0) PeakManaged = Math.Max(PeakManaged, GC.GetTotalMemory(false));
    }
    public override void Write(char value) { Characters++; if (value == '\n') Lines++; }
}
