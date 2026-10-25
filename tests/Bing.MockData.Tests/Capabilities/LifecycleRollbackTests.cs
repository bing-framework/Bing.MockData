using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Bing.MockData.Tests.Capabilities;

public sealed class LifecycleRollbackTests
{
    private sealed class Resource : IDisposable
    {
        public int DisposeCalls;
        public void Dispose() => Interlocked.Increment(ref DisposeCalls);
    }

    private sealed class ThrowingResource : IDisposable
    {
        public int DisposeCalls;
        public readonly InvalidOperationException Error = new InvalidOperationException("cleanup failure");
        public void Dispose()
        {
            Interlocked.Increment(ref DisposeCalls);
            throw Error;
        }
    }

#if !MOCKDATA_NETSTANDARD
    private sealed class YieldingAsyncResource : IAsyncDisposable
    {
        public int DisposeCalls;

        public async ValueTask DisposeAsync()
        {
            await System.Threading.Tasks.Task.Yield();
            Interlocked.Increment(ref DisposeCalls);
        }
    }

    private sealed class PostingSynchronizationContext : SynchronizationContext
    {
        public int Posts;

        public override void Post(SendOrPostCallback callback, object state)
        {
            Interlocked.Increment(ref Posts);
            System.Threading.ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }

    [Fact]
    public void Failed_context_construction_awaits_async_cleanup_without_capturing_caller_context()
    {
        var originalError = new InvalidOperationException("construction failure");
        var syncResource = new Resource();
        var asyncResource = new YieldingAsyncResource();
        var options = new MockOptions()
            .UseResource(syncResource, ownsResource: true)
            .UseResource(asyncResource, ownsResource: true)
            .UseExtensionFactory<Resource>("failure", () => throw originalError, "v1");
        var previousContext = SynchronizationContext.Current;
        var testContext = new PostingSynchronizationContext();

        try
        {
            SynchronizationContext.SetSynchronizationContext(testContext);
            var error = Assert.Throws<InvalidOperationException>(() => new MockContext(options));
            Assert.Same(originalError, error);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        Assert.Equal(1, syncResource.DisposeCalls);
        Assert.Equal(1, asyncResource.DisposeCalls);
        Assert.Equal(0, testContext.Posts);
    }
#endif

    [Fact]
    public void Failed_context_construction_aggregates_cleanup_errors_and_continues_releasing()
    {
        var originalError = new InvalidOperationException("construction failure");
        var survivor = new Resource();
        var throwing = new ThrowingResource();
        var options = new MockOptions()
            .UseResource(survivor, ownsResource: true)
            .UseResource(throwing, ownsResource: true)
            .UseExtensionFactory<Resource>("failure", () => throw originalError, "v1");

        var error = Assert.Throws<AggregateException>(() => new MockContext(options));

        Assert.Contains(originalError, error.InnerExceptions);
        Assert.Contains(throwing.Error, error.InnerExceptions);
        Assert.Equal(1, throwing.DisposeCalls);
        Assert.Equal(1, survivor.DisposeCalls);
    }
}
