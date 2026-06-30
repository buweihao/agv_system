using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.DebugDashboard.Services;

public interface IMockSimulationService
{
    event EventHandler? VehiclesChanged;

    IReadOnlyList<VehicleStatusSnapshot> GetVehicles();

    VehicleStatusSnapshot? GetVehicle(string vehicleId);

    void UpsertVehicle(MockVehicleUpdate update);
}

public sealed class MockVehicleUpdate
{
    public string VehicleId { get; init; } = string.Empty;

    public string? Brand { get; init; }

    public RobotState? State { get; init; }

    public VehicleLoadState? LoadState { get; init; }

    public double? BatteryLevel { get; init; }

    public string? Location { get; init; }

    public string? CurrentTaskId { get; init; }

    public bool? IsOnline { get; init; }

    public bool? IsCharging { get; init; }

    public bool? HasAlarm { get; init; }

    public string? ActiveAlarmCode { get; init; }

    public string? ActiveAlarmMessage { get; init; }

    public Dictionary<string, string>? Telemetry { get; init; }
}
