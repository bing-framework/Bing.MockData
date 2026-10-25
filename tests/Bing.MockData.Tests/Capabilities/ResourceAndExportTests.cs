// 此文件同时验证省略可选取消参数的公开契约；取消用例显式传入独立令牌。
#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class ResourceAndExportTests
{
    private sealed class Resource : IDisposable { public int Calls; public void Dispose() => Calls++; }
    private sealed class TrackingStream : MemoryStream
    {
        public int DisposeCalls;
        public TrackingStream(string content) : base(Encoding.UTF8.GetBytes(content)) { }
        protected override void Dispose(bool disposing) { if (disposing) DisposeCalls++; base.Dispose(disposing); }
    }
    [Fact]
    public void Legacy_text_loops_observe_cancellation_and_mobile_rejects_oversized_configuration()
    {
        Assert.Throws<MockLimitException>(() => new Core.Randomizers.MobileRandomizer(new Core.Options.MobileFieldOptions { Length = 100001 }));
        using var first = new CancellationTokenSource();
        var numbers = new Internals.Generators.RandomGenerator(new CancellingRandom(first));
        Assert.Throws<OperationCanceledException>(() => numbers.GenerateNumber(1000, first.Token));
        using var second = new CancellationTokenSource();
        var text = new Internals.Generators.RandomStringGenerator(new CancellingRandom(second), true, false, false, false, false);
        Assert.Throws<OperationCanceledException>(() => text.Generate(1000, 1000, second.Token));
    }
    private sealed class CancellingRandom : Random
    {
        private readonly CancellationTokenSource _cancel;
        public CancellingRandom(CancellationTokenSource cancel) { _cancel = cancel; }
        public override int Next(int maxValue) { _cancel.Cancel(); return 0; }
    }
    [Fact]
    public void Empty_batches_still_check_disposal_and_bound_tokens_propagate()
    {
        var mock = new MockContext(); var generator = mock.Sequence(); mock.Dispose();
        Assert.Throws<ObjectDisposedException>(() => generator.GenerateMany(0));
        Assert.Throws<ObjectDisposedException>(() => generator.Enumerate(0).ToArray());
        using var active = new MockContext(); using var cancel = new CancellationTokenSource();
        var bound = active.ForRecord(0, cancel.Token).Generator(c => { cancel.Cancel(); return c.Numbers.Int(); });
        Assert.Throws<OperationCanceledException>(() => bound.Generate());
    }
    [Fact]
    public void Dictionary_byte_limit_supports_long_max_and_input_must_be_utf8()
    {
        using var mock = new MockContext(new MockOptions { Limits = new MockLimits { MaxDictionaryBytes = long.MaxValue } });
        using var valid = new TrackingStream("word"); mock.LoadTextDictionary("valid", valid);
        Assert.Equal("word", mock.Dictionary<string>("valid")[0]);
        using var utf16 = new MemoryStream(new byte[] { 0xff, 0xfe, 0x61, 0x00 });
        Assert.Throws<DecoderFallbackException>(() => mock.LoadTextDictionary("bad", utf16));
    }
    [Fact]
    public void Context_disposes_only_owned_resources_and_rejects_later_calls()
    {
        var owned = new Resource(); var borrowed = new Resource();
        var options = new MockOptions().UseResource(owned, true).UseResource(owned, true).UseResource(borrowed).Register("answer", _ => 42);
        var mock = new MockContext(options); var child = mock.ForRecord(0); var generator = mock.Registered<int>("answer");
        Assert.Equal(42, generator.Generate()); child.Dispose(); Assert.Equal(42, generator.Generate());
        mock.Dispose(); mock.Dispose(); Assert.Equal(1, owned.Calls); Assert.Equal(0, borrowed.Calls);
        Assert.Throws<ObjectDisposedException>(() => generator.Generate()); Assert.Throws<ObjectDisposedException>(() => child.Numbers.Int());
    }
    [Fact]
    public void Text_dictionary_loading_is_atomic_bounded_and_respects_ownership()
    {
        using var mock = new MockContext(new MockOptions { Limits = new MockLimits { MaxDictionaryEntries = 2, MaxDictionaryBytes = 20 } });
        var owned = new TrackingStream("one\ntwo"); mock.LoadTextDictionary("words", owned, false); Assert.Equal(1, owned.DisposeCalls);
        Assert.Equal(new[] { "one", "two" }, mock.Dictionary<string>("words"));
        var borrowed = new TrackingStream("new"); mock.LoadTextDictionary("words", borrowed); Assert.Equal(0, borrowed.DisposeCalls); borrowed.Dispose();
        var oversized = new TrackingStream(new string('x', 21));
        Assert.Throws<MockLimitException>(() => mock.LoadTextDictionary("words", oversized, false)); Assert.Equal(1, oversized.DisposeCalls);
        Assert.Equal(new[] { "new" }, mock.Dictionary<string>("words"));
        var tooMany = new TrackingStream("a\nb\nc"); Assert.Throws<MockLimitException>(() => mock.LoadTextDictionary("words", tooMany, false)); Assert.Equal(1, tooMany.DisposeCalls);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); var cancelled = new TrackingStream("a");
        Assert.Throws<OperationCanceledException>(() => mock.LoadTextDictionary("words", cancelled, false, cancel.Token)); Assert.Equal(1, cancelled.DisposeCalls);
    }
    [Fact]
    public void Running_enumeration_keeps_dictionary_snapshot_and_later_runs_observe_replacement()
    {
        using var mock = new MockContext(); mock.SetDictionary("words", new[] { "old" });
        var generator = mock.Generator(c => c.Collections.Pick(c.Dictionary<string>("words")));
        var sequence = generator.Enumerate(3);
        using (var iterator = sequence.GetEnumerator())
        {
            Assert.True(iterator.MoveNext()); Assert.Equal("old", iterator.Current);
            mock.SetDictionary("words", new[] { "new" });
            Assert.True(iterator.MoveNext()); Assert.Equal("old", iterator.Current);
        }
        Assert.All(sequence, value => Assert.Equal("new", value));
        Assert.True(mock.RemoveDictionary("words")); Assert.Throws<MockGenerationException>(() => generator.Generate());
    }
    [Fact]
    public void Json_dictionary_requires_complete_array_and_limited_depth()
    {
        using var mock = new MockContext(new MockOptions { Limits = new MockLimits { MaxDepth = 2 } });
        using var input = new TrackingStream("[1,2,3]"); mock.LoadJsonDictionary<int>("numbers", input);
        Assert.Equal(new[] { 1, 2, 3 }, mock.Dictionary<int>("numbers")); Assert.Equal(0, input.DisposeCalls);
        using var malformed = new TrackingStream("[4,"); Assert.ThrowsAny<Newtonsoft.Json.JsonException>(() => mock.LoadJsonDictionary<int>("numbers", malformed));
        Assert.Equal(new[] { 1, 2, 3 }, mock.Dictionary<int>("numbers"));
        using var deep = new TrackingStream("[[[1]]]"); Assert.ThrowsAny<Newtonsoft.Json.JsonException>(() => mock.LoadJsonDictionary<object>("deep", deep));
    }
    [Fact]
    public void Json_and_jsonlines_select_columns_and_csv_quotes_special_characters()
    {
        var rows = new[] { new Item { Name = "a,b", Value = "line\n\"quoted\"" }, new Item { Name = "plain", Value = "" } };
        var columns = new[] { new ExportColumn<Item>("name", x => x.Name), new ExportColumn<Item>("value", x => x.Value) };
        var json = new StringWriter(); MockExport.Write(rows, json, MockExportFormat.Json, columns);
        Assert.Equal(2, JArray.Parse(json.ToString()).Count); Assert.Equal("a,b", (string)JArray.Parse(json.ToString())[0]["name"]);
        var lines = new StringWriter(); MockExport.Write(rows, lines, MockExportFormat.JsonLines, columns);
        Assert.Equal(2, lines.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        var csv = new StringWriter(); MockExport.Write(rows, csv, MockExportFormat.Csv, columns);
        Assert.Equal("name,value\r\n\"a,b\",\"line\n\"\"quoted\"\"\"\r\nplain,\r\n", csv.ToString());
        csv.Write("still open");
        using var memory = new MemoryStream(); MockExport.Write(rows, memory, MockExportFormat.Json); Assert.True(memory.CanWrite);
        var closed = new StringWriter(); MockExport.Write(rows, closed, MockExportFormat.Json, leaveOpen: false); Assert.Throws<ObjectDisposedException>(() => closed.Write("x"));
    }
    [Theory]
    [InlineData(MockExportFormat.Json)]
    [InlineData(MockExportFormat.JsonLines)]
    [InlineData(MockExportFormat.Csv)]
    public void Cancellation_disposes_input_enumerator_and_owned_writer(MockExportFormat format)
    {
        using var cancellation = new CancellationTokenSource(); bool disposed = false;
        IEnumerable<int> Source()
        { try { yield return 1; cancellation.Cancel(); yield return 2; } finally { disposed = true; } }
        var writer = new StringWriter();
        Assert.Throws<OperationCanceledException>(() => MockExport.Write(Source(), writer, format, new[] { new ExportColumn<int>("number", x => x) }, leaveOpen: false, cancellationToken: cancellation.Token));
        Assert.True(disposed); Assert.Throws<ObjectDisposedException>(() => writer.Write("x")); Assert.Contains("1", writer.ToString());
    }
    [Fact]
    public void Disposed_parallel_enumerator_releases_unconsumed_window_objects()
    {
        var references = new WeakReference[256];
        var disposedEnumerator = CreateDisposedEnumerator(references);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.All(references.Skip(1), reference => Assert.False(reference.IsAlive));
        GC.KeepAlive(disposedEnumerator);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static object CreateDisposedEnumerator(WeakReference[] references)
    {
        using var mock = new MockContext();
        var recipe = mock.Generator(c => { var value = new object(); references[c.RecordIndex] = new WeakReference(value); return value; });
        var iterator = recipe.EnumerateParallel(256, 4).GetEnumerator();
        Assert.True(iterator.MoveNext()); iterator.Dispose(); return iterator;
    }
    [Fact]
    public void Failed_generation_and_resource_disposal_release_the_remaining_resources()
    {
        var owned = new Resource(); var throwing = new ThrowingResource();
        var mock = new MockContext(new MockOptions().UseResource(owned, true).UseResource(throwing, true));
        var recipe = mock.Generator<int>(_ => throw new InvalidOperationException("failure"));
        var output = new StringWriter();
        Assert.Throws<MockGenerationException>(() => MockExport.Write(recipe.Enumerate(1), output, MockExportFormat.Json, leaveOpen: false));
        Assert.Throws<ObjectDisposedException>(() => output.Write("x"));
        Assert.Throws<AggregateException>(() => mock.Dispose()); Assert.Equal(1, owned.Calls);
        mock.Dispose(); Assert.Equal(1, owned.Calls);
    }
    private sealed class ThrowingResource : IDisposable { public void Dispose() => throw new InvalidOperationException("dispose failure"); }
#if !MOCKDATA_NETSTANDARD
    private sealed class AsyncResource : IAsyncDisposable { public int Calls; public ValueTask DisposeAsync() { Calls++; return default; } }
    [Fact]
    public async Task Modern_async_export_and_async_only_resources_release_correctly()
    {
        var resource = new AsyncResource(); var mock = new MockContext(new MockOptions().UseResource(resource, true));
        var writer = new StringWriter(); await MockExport.WriteAsync(mock.Sequence().EnumerateAsync(3), writer, MockExportFormat.Json);
        Assert.Equal("[1,2,3]", writer.ToString()); Assert.Throws<InvalidOperationException>(() => mock.Dispose());
        await mock.DisposeAsync(); await mock.DisposeAsync(); Assert.Equal(1, resource.Calls);
        var cancel = new CancellationTokenSource(); cancel.Cancel();
        using var other = new MockContext();
        await Assert.ThrowsAsync<OperationCanceledException>(() => MockExport.WriteAsync(other.Sequence().EnumerateAsync(3), new StringWriter(), MockExportFormat.Json, cancellationToken: cancel.Token));
    }
#endif
    public sealed class Item { public string Name { get; set; } public string Value { get; set; } }
}
