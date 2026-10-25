// 此文件同时验证省略可选取消参数的公开契约；取消用例显式传入独立令牌。
#pragma warning disable xUnit1051
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public class BuilderAndBatchTests
{
    public sealed class Row { public int Id { get; set; } public string Name { get; set; } public int Derived { get; set; } public Row Child { get; set; } public List<int> Values { get; set; } }
    public sealed class Constructed { public int Id { get; } public Constructed(int id) { Id = id; } }
    [Fact]
    public void Unrelated_rules_do_not_change_existing_field_streams()
    {
        using var mock = new MockContext(new MockOptions { Seed = 81 });
        var first = mock.Builder<Row>().RuleFor(x => x.Id, c => c.Numbers.Int()).RuleFor(x => x.Name, c => c.Text.Random()).Build();
        var second = mock.Builder<Row>().RuleFor(x => x.Derived, c => c.Numbers.Int()).RuleFor(x => x.Name, c => c.Text.Random()).RuleFor(x => x.Id, c => c.Numbers.Int()).Build();
        for (int i = 0; i < 100; i++) { var a = first.At(i); var b = second.At(i); Assert.Equal(a.Id, b.Id); Assert.Equal(a.Name, b.Name); }
    }
    [Fact]
    public void Built_plans_are_snapshots_and_derived_rules_follow_declaration_order()
    {
        using var mock = new MockContext();
        var builder = mock.Builder<Row>().RuleFor(x => x.Id, _ => 9).RuleFor(x => x.Derived, (_, row) => row.Id * 2);
        var before = builder.Build(); builder.Override(x => x.Id, (_, _) => 12);
        Assert.Equal(18, before.Generate().Derived); Assert.Equal(24, builder.Build().Generate().Derived);
        Assert.Throws<ArgumentException>(() => builder.RuleFor(x => x.Id, _ => 1));
        Assert.Throws<InvalidOperationException>(() => mock.Builder<Row>().Strict().Build());
        Assert.Throws<InvalidOperationException>(() => mock.Builder<Constructed>().Build());
        Assert.Equal(42, mock.Builder<Constructed>().ConstructUsing(_ => new Constructed(42)).Build().Generate().Id);
    }
    [Fact]
    public void Nested_and_collection_rules_enforce_budgets_and_locate_failure()
    {
        using var mock = new MockContext(new MockOptions { Seed = 3, Limits = new MockLimits { MaxDepth = 2, MaxCollectionSize = 2 } });
        var recursive = mock.Builder<Row>(); recursive.RuleFor(x => x.Child, c => recursive.Generate(c));
        Assert.Throws<MockLimitException>(() => recursive.Build().Generate());
        Assert.Throws<MockLimitException>(() => mock.Builder<Row>().Collection(x => x.Values, 0, 3, c => c.Numbers.Int()));
        var recipe = mock.Builder<Row>().RuleFor<string>(x => x.Name, _ => throw new InvalidOperationException("deliberate")).Build();
        var error = Assert.Throws<MockGenerationException>(() => recipe.At(17));
        Assert.Equal("Name", error.FieldPath); Assert.Equal(17, error.RecordIndex); Assert.Equal(3, error.Replay.Seed);
        var inner = mock.Builder<Row>().RuleFor(x => x.Id, _ => 7);
        Assert.Equal(7, mock.Builder<Row>().Nested(x => x.Child, inner).Build().Generate().Child.Id);
    }
    public sealed class ReadOnlyRow { public IReadOnlyCollection<int> Items { get; set; } }
    private sealed class ReadOnlyValues : IReadOnlyCollection<int>
    {
        public int Count => 3;
        public IEnumerator<int> GetEnumerator() => Enumerable.Range(0, 3).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    [Fact]
    public void Readonly_collection_rules_obey_element_limits_and_convenience_templates_advance()
    {
        using var mock = new MockContext(new MockOptions { Seed = 42, Limits = new MockLimits { MaxCollectionSize = 2 } });
        Assert.Throws<MockLimitException>(() => mock.Builder<ReadOnlyRow>().RuleFor(x => x.Items, _ => new ReadOnlyValues()).Build().Generate());
        var first = mock.Person(); var second = mock.Person();
        Assert.NotEqual(first.Name + first.BirthDate + first.Address.FullAddress, second.Name + second.BirthDate + second.Address.FullAddress);
        using var disposed = new MockContext(); disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => disposed.Collections.Sample(new[] { 1 }, 0));
    }
    [Fact]
    public void Batch_reenumeration_and_parallel_generation_match_record_indices()
    {
        using var mock = new MockContext(new MockOptions { Seed = 921 });
        var generator = mock.Generator(c => c.Numbers.Long());
        var sequence = generator.Enumerate(1000); var first = sequence.ToArray();
        generator.Generate(); Assert.Equal(first, sequence.ToArray());
        var metrics = new GenerationMetrics(); Assert.Equal(first, generator.EnumerateParallel(1000, 4, metrics: metrics));
        Assert.Equal(1000, metrics.GeneratedRecords); Assert.InRange(metrics.MaxActualParallelism, 1, 4); Assert.InRange(metrics.MaxQueuedRecords, 1, 256);
        Assert.Equal(first[43], generator.At(43));
    }
    [Fact]
    public void Early_stop_is_lazy_and_cancelled_calls_do_not_generate()
    {
        using var mock = new MockContext(); int calls = 0;
        var generator = mock.Generator(_ => ++calls);
        Assert.Equal(3, generator.Enumerate(1000000).Take(3).Count()); Assert.Equal(3, calls);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => generator.Enumerate(1, cancelled.Token).ToArray()); Assert.Equal(3, calls);
        Assert.Throws<MockLimitException>(() => generator.GenerateMany(10001));
        Assert.Throws<MockLimitException>(() => generator.Enumerate(1000001));
    }
    [Fact]
    public void Parallel_early_stop_obeys_backpressure_and_retains_no_active_workers()
    {
        using var mock = new MockContext(); var metrics = new GenerationMetrics();
        var result = mock.Sequence().EnumerateParallel(1000000, 4, metrics: metrics).Take(1).Single();
        Assert.Equal(1, result); Assert.Equal(256, metrics.GeneratedRecords); Assert.Equal(256, metrics.MaxQueuedRecords);
        using var cancel = new CancellationTokenSource();
        var generator = mock.Generator(c => { if (c.RecordIndex == 1) cancel.Cancel(); c.CancellationToken.ThrowIfCancellationRequested(); return c.RecordIndex; });
        Assert.ThrowsAny<OperationCanceledException>(() => generator.EnumerateParallel(1000, 4, cancel.Token).ToArray());
    }
    [Fact]
    public void Unique_scopes_fail_on_exhaustion_and_sequences_need_no_history()
    {
        using var mock = new MockContext(new MockOptions { Limits = new MockLimits { MaxAttempts = 2, MaxUniqueValues = 2 } });
        using var unique = new UniqueScope<int>(mock.Generator(_ => 1)); Assert.Equal(1, unique.Next());
        Assert.Throws<MockLimitException>(() => unique.Next()); unique.Dispose(); Assert.Throws<ObjectDisposedException>(() => unique.Next());
        using var capacity = new UniqueScope<long>(mock.Sequence()); capacity.Next(); capacity.Next(); Assert.Throws<MockLimitException>(() => capacity.Next());
        Assert.Equal(1000000, mock.Sequence().At(999999)); Assert.Equal("ID-0005", mock.Identifier("ID-", 1, 2, "D4").At(2));
        Assert.Throws<MockGenerationException>(() => mock.Sequence(long.MaxValue).At(1));
    }
    [Fact]
    public void Templates_keep_id_birth_gender_region_and_order_totals_consistent()
    {
        using var mock = new MockContext(new MockOptions { Seed = 81, ReferenceTime = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero) });
        var people = mock.Templates.Person("110000").Build().GenerateMany(20);
        Assert.All(people, p => {
            Assert.Equal(p.Address.CountyCode, p.IdCard.Substring(0, 6));
            Assert.Equal(p.BirthDate.ToString("yyyyMMdd"), p.IdCard.Substring(6, 8));
            Assert.Equal((int)p.Gender, (p.IdCard[16] - '0') % 2);
            Assert.Equal("110000", p.Address.ProvinceCode); Assert.Null(p.Address.CityCode);
            int age = 2026 - p.BirthDate.Year; if (p.BirthDate.Date > new DateTime(2026, 10, 25).AddYears(-age)) age--;
            Assert.Equal(age, p.Age); Assert.Equal(p.ValidFrom.AddYears(10), p.ValidTo);
        });
        var users = mock.Templates.User().Build().GenerateMany(3); var products = mock.Templates.Product().Build().GenerateMany(5);
        foreach (var order in mock.Templates.Order(users, products).Build().GenerateMany(20))
        { Assert.Contains(order.User, users); Assert.All(order.Items, i => { Assert.Contains(i.Product, products); Assert.Equal(i.Product.Price, i.UnitPrice); }); Assert.Equal(order.Items.Sum(i => i.Quantity * i.UnitPrice), order.Total); }
        var taiwan = mock.Address("710000"); Assert.Null(taiwan.CountyCode); Assert.Equal("710000", taiwan.DivisionCode);
        using var english = new MockContext(new MockOptions { Locale = "en-US" }); Assert.Equal("US", english.Address().CountryCode);
    }
}
