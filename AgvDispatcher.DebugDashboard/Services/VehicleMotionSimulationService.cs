using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.DebugDashboard.Services;

public sealed class VehicleMotionSimulationService : IVehicleMotionSimulationService
{
    private readonly IMockSimulationService _simulation;
    private readonly IVehicleMotionScheduler _scheduler;
    private readonly Dictionary<string, MotionState> _motions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private bool _disposed;

    public VehicleMotionSimulationService(
        IMockSimulationService simulation,
        IVehicleMotionScheduler scheduler)
    {
        _simulation = simulation;
        _scheduler = scheduler;
        _scheduler.Tick += OnTick;
    }

    public double DefaultSpeed { get; set; } = 120;

    public bool IsMoving(string vehicleId)
    {
        lock (_syncRoot)
        {
            return _motions.ContainsKey(vehicleId);
        }
    }

    public Task<VehicleMotionResult> MoveToAsync(
        string vehicleId,
        MapPosition target,
        double? speed = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(vehicleId);
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        if (!target.HasValidCoordinates())
        {
            throw new ArgumentException("Target must contain valid map coordinates.", nameof(target));
        }

        var snapshot = _simulation.GetVehicle(vehicleId)
            ?? throw new InvalidOperationException($"Vehicle '{vehicleId}' does not exist.");
        if (snapshot.Position?.HasValidCoordinates() != true)
        {
            throw new InvalidOperationException($"Vehicle '{vehicleId}' has no continuous start position.");
        }

        var actualSpeed = speed ?? DefaultSpeed;
        if (!double.IsFinite(actualSpeed) || actualSpeed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "Speed must be greater than zero.");
        }

        lock (_syncRoot)
        {
            if (_motions.TryGetValue(vehicleId, out var current) && SameTarget(current.Target, target))
            {
                return current.Completion.Task;
            }

            if (current is not null)
            {
                CompleteNoLock(current, VehicleMotionResult.Stopped("Target changed."));
                _motions.Remove(vehicleId);
            }

            var completion = new TaskCompletionSource<VehicleMotionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var motion = new MotionState(vehicleId, target.Clone(), actualSpeed, completion);
            if (cancellationToken.CanBeCanceled)
            {
                motion.CancellationRegistration = cancellationToken.Register(
                    () => StopVehicle(vehicleId, "Cancelled."));
            }

            _motions[vehicleId] = motion;
            _scheduler.Start();
            return completion.Task;
        }
    }

    public bool StopVehicle(string vehicleId, string reason = "Stopped")
    {
        lock (_syncRoot)
        {
            if (!_motions.Remove(vehicleId, out var motion))
            {
                return false;
            }

            CompleteNoLock(motion, VehicleMotionResult.Stopped(reason));
            StopSchedulerIfIdleNoLock();
            return true;
        }
    }

    public void StopAll(string reason = "Stopped")
    {
        lock (_syncRoot)
        {
            foreach (var motion in _motions.Values)
            {
                CompleteNoLock(motion, VehicleMotionResult.Stopped(reason));
            }

            _motions.Clear();
            _scheduler.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAll("Motion service disposed.");
        _scheduler.Tick -= OnTick;
        _scheduler.Dispose();
    }

    private void OnTick(object? sender, TimeSpan elapsed)
    {
        MotionState[] motions;
        lock (_syncRoot)
        {
            motions = _motions.Values.ToArray();
        }

        foreach (var motion in motions)
        {
            try
            {
                AdvanceMotion(motion, elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                StopVehicle(motion.VehicleId, $"Motion failed: {ex.Message}");
            }
        }
    }

    private void AdvanceMotion(MotionState motion, double deltaSeconds)
    {
        var snapshot = _simulation.GetVehicle(motion.VehicleId);
        if (snapshot is null)
        {
            StopVehicle(motion.VehicleId, "Vehicle was removed.");
            return;
        }

        if (!snapshot.IsOnline || snapshot.State is RobotState.Fault or RobotState.Offline)
        {
            StopVehicle(motion.VehicleId, $"Vehicle state is {snapshot.State}.");
            return;
        }

        var start = snapshot.Position;
        if (start?.HasValidCoordinates() != true)
        {
            StopVehicle(motion.VehicleId, "Continuous position became invalid.");
            return;
        }

        var distance = Math.Sqrt(
            Math.Pow(motion.Target.X - start.X, 2) +
            Math.Pow(motion.Target.Y - start.Y, 2));
        var travel = motion.Speed * Math.Max(0, deltaSeconds);
        var progress = distance <= 0 || travel >= distance ? 1 : travel / distance;
        var next = VehicleMotionMath.Interpolate(start, motion.Target, progress);

        _simulation.UpsertVehicle(new MockVehicleUpdate
        {
            VehicleId = motion.VehicleId,
            Position = next
        });

        if (progress >= 1)
        {
            CompleteArrival(motion);
        }
    }

    private void CompleteArrival(MotionState motion)
    {
        lock (_syncRoot)
        {
            if (!_motions.TryGetValue(motion.VehicleId, out var current) || !ReferenceEquals(current, motion))
            {
                return;
            }

            _motions.Remove(motion.VehicleId);
            CompleteNoLock(motion, VehicleMotionResult.Arrived());
            StopSchedulerIfIdleNoLock();
        }
    }

    private void StopSchedulerIfIdleNoLock()
    {
        if (_motions.Count == 0)
        {
            _scheduler.Stop();
        }
    }

    private static bool SameTarget(MapPosition left, MapPosition right) =>
        string.Equals(left.MapId, right.MapId, StringComparison.OrdinalIgnoreCase) &&
        Math.Abs(left.X - right.X) < 0.000001 &&
        Math.Abs(left.Y - right.Y) < 0.000001;

    private static void CompleteNoLock(MotionState motion, VehicleMotionResult result)
    {
        motion.CancellationRegistration.Dispose();
        motion.Completion.TrySetResult(result);
    }

    private sealed class MotionState(
        string vehicleId,
        MapPosition target,
        double speed,
        TaskCompletionSource<VehicleMotionResult> completion)
    {
        public string VehicleId { get; } = vehicleId;
        public MapPosition Target { get; } = target;
        public double Speed { get; } = speed;
        public TaskCompletionSource<VehicleMotionResult> Completion { get; } = completion;
        public CancellationTokenRegistration CancellationRegistration { get; set; }
    }
}
