using Microsoft.UI.Dispatching;
using SystemTimer = System.Timers.Timer;

namespace MediaPlayerScrobblerBase;

public sealed class TimerTickSource : ITickSource
{
    private readonly SystemTimer _timer;
    private readonly SynchronizationContext _synchronizationContext;
    private readonly object _gate = new();
    private bool _isRunning;
    private bool _isTickScheduled;
    private bool _isDisposed;
    private long _generation;

    public event EventHandler? Tick;

    public TimerTickSource(int intervalMs)
        : this(intervalMs, GetCurrentDispatcherContext())
    {
    }

    internal TimerTickSource(int intervalMs, SynchronizationContext synchronizationContext)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalMs);
        ArgumentNullException.ThrowIfNull(synchronizationContext);

        _synchronizationContext = synchronizationContext;
        _timer = new SystemTimer(intervalMs);
        _timer.Elapsed += OnElapsed;
    }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_isRunning)
                return;

            _generation++;
            _isRunning = true;
            _timer.Start();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (_isDisposed || !_isRunning)
                return;

            _isRunning = false;
            _generation++;
            _timer.Stop();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
                return;

            _isRunning = false;
            _isDisposed = true;
            _generation++;
            _timer.Elapsed -= OnElapsed;
            _timer.Dispose();
            Tick = null;
        }
    }

    private void OnElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        long generation;

        lock (_gate)
        {
            if (_isDisposed || !_isRunning || _isTickScheduled)
                return;

            _isTickScheduled = true;
            generation = _generation;
        }

        try
        {
            _synchronizationContext.Post(_ => DeliverTick(generation), null);
        }
        catch
        {
            lock (_gate)
            {
                _isTickScheduled = false;
            }

            throw;
        }
    }

    private void DeliverTick(long generation)
    {
        lock (_gate)
        {
            try
            {
                if (_isDisposed || !_isRunning || generation != _generation)
                    return;

                Tick?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _isTickScheduled = false;
            }
        }
    }

    private static SynchronizationContext GetCurrentDispatcherContext()
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("TimerTickSource must be created on a thread with a dispatcher queue.");

        return new DispatcherQueueSynchronizationContext(dispatcherQueue);
    }

    private sealed class DispatcherQueueSynchronizationContext(DispatcherQueue dispatcherQueue) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            if (!dispatcherQueue.TryEnqueue(() => d(state)))
                throw new InvalidOperationException("The timer tick could not be queued on the dispatcher thread.");
        }
    }
}
