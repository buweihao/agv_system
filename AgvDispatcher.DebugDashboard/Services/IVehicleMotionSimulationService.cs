using AgvDispatcher.Core.Models;

namespace AgvDispatcher.DebugDashboard.Services;

public interface IVehicleMotionSimulationService : IDisposable
{
    double DefaultSpeed { get; set; }

    bool IsMoving(string vehicleId);

    Task<VehicleMotionResult> MoveToAsync(
        string vehicleId,
        MapPosition target,
        double? speed = null,
        CancellationToken cancellationToken = default);

    bool StopVehicle(string vehicleId, string reason = "Stopped");

    void StopAll(string reason = "Stopped");
}

public sealed class VehicleMotionResult
{
    public bool ReachedTarget { get; init; }

    public string Message { get; init; } = string.Empty;

    public static VehicleMotionResult Arrived() => new()
    {
        ReachedTarget = true,
        Message = "Target reached."
    };

    public static VehicleMotionResult Stopped(string message) => new()
    {
        ReachedTarget = false,
        Message = message
    };
}

public interface IVehicleMotionScheduler : IDisposable
{
    event EventHandler<TimeSpan>? Tick;

    void Start();

    void Stop();
}
