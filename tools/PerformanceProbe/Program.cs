using System.Diagnostics;
using System.Text.Json;
using Bing.MockData.Core.Options;
using Bing.MockData.Core.Randomizers;

// 同一程序引用 before/after 程序集，固定工作量；不将随机内容当成比较基准。
var results = new List<object>();
foreach (var (length, count) in new[] { (8, 1000), (1024, 100), (8192, 10) })
{
    var warmup = new TextRandomizer(new TextFieldOptions { Min = length, Max = length });
    for (var i = 0; i < 3; i++) warmup.Generate();
    for (var repetition = 0; repetition < 3; repetition++)
    {
        var generator = new TextRandomizer(new TextFieldOptions { Min = length, Max = length });
        var startBytes = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        var characters = 0L;
        for (var i = 0; i < count; i++) characters += generator.Generate().Length;
        timer.Stop();
        results.Add(new { length, count, repetition, characters, milliseconds = timer.Elapsed.TotalMilliseconds,
            allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes });
    }
}
Console.WriteLine(JsonSerializer.Serialize(new { candidate = args.FirstOrDefault() ?? "unspecified",
    runtime = Environment.Version.ToString(), os = Environment.OSVersion.ToString(), results },
    new JsonSerializerOptions { WriteIndented = true }));
