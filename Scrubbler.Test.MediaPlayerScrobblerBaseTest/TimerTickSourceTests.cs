using System.Collections.Concurrent;
using MediaPlayerScrobblerBase;

namespace Scrubbler.Test.MediaPlayerScrobblerBaseTest;

[TestFixture]
internal sealed class TimerTickSourceTests
{
    [Test]
    public void Tick_IsDeliveredOnCapturedSynchronizationContext()
    {
        var context = new QueuedSynchronizationContext();
        using var source = new TimerTickSource(10, context);
        var callbackThreadId = -1;

        source.Tick += (_, _) => callbackThreadId = Environment.CurrentManagedThreadId;
        source.Start();

        Assert.That(SpinWait.SpinUntil(() => context.PendingCount > 0, TimeSpan.FromSeconds(2)), Is.True);
        var deliveryThreadId = Environment.CurrentManagedThreadId;
        context.RunAll();

        Assert.That(callbackThreadId, Is.EqualTo(deliveryThreadId));
    }

    [Test]
    public void SlowConsumer_DoesNotAccumulateOrOverlapTicks()
    {
        var context = new QueuedSynchronizationContext();
        using var source = new TimerTickSource(10, context);
        var tickCount = 0;

        source.Tick += (_, _) => tickCount++;
        source.Start();

        Assert.That(SpinWait.SpinUntil(() => context.PendingCount > 0, TimeSpan.FromSeconds(2)), Is.True);
        Thread.Sleep(100);

        Assert.That(context.PendingCount, Is.EqualTo(1));
        context.RunAll();
        Assert.That(tickCount, Is.EqualTo(1));
    }

    [Test]
    public void Stop_DiscardsAnAlreadyQueuedTick()
    {
        var context = new QueuedSynchronizationContext();
        using var source = new TimerTickSource(10, context);
        var tickCount = 0;

        source.Tick += (_, _) => tickCount++;
        source.Start();

        Assert.That(SpinWait.SpinUntil(() => context.PendingCount > 0, TimeSpan.FromSeconds(2)), Is.True);
        source.Stop();
        context.RunAll();

        Assert.That(tickCount, Is.Zero);
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public int PendingCount => _callbacks.Count;

        public override void Post(SendOrPostCallback d, object? state)
        {
            _callbacks.Enqueue((d, state));
        }

        public void RunAll()
        {
            while (_callbacks.TryDequeue(out var callback))
                callback.Callback(callback.State);
        }
    }
}
