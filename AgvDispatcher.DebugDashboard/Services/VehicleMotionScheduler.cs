using System.Diagnostics;

namespace AgvDispatcher.DebugDashboard.Services;

public sealed class VehicleMotionScheduler : IVehicleMotionScheduler
{
    private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(40);
    private readonly Stopwatch _stopwatch = new();
    private Timer? _timer;
    private int _isTicking;

    public event EventHandler<TimeSpan>? Tick;

    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        _stopwatch.Restart();
        _timer = new Timer(OnTimer, null, _interval, _interval);
    }

    public void Stop()
    {
        Interlocked.Exchange(ref _timer, null)?.Dispose();
        _stopwatch.Stop();
    }

    public void Dispose() => Stop();

    private void OnTimer(object? state)
    {
        if (Interlocked.Exchange(ref _isTicking, 1) != 0)
        {
            return;
        }

        try
        {
            var elapsed = _stopwatch.Elapsed;
            _stopwatch.Restart();
            if (elapsed > TimeSpan.Zero)
            {
                Tick?.Invoke(this, elapsed);
            }
        }
        finally
        {
            Volatile.Write(ref _isTicking, 0);
        }
    }
}
