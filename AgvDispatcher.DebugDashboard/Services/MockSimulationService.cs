using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Events;

namespace AgvDispatcher.DebugDashboard.Services;

public sealed class MockSimulationService : IMockSimulationService, IDisposable
{
    private readonly IVehicleStateStore _vehicleStateStore;
    private readonly SubscriptionToken _vehicleChangedSubscription;

    public MockSimulationService(IVehicleStateStore vehicleStateStore, IEventAggregator eventAggregator)
    {
        _vehicleStateStore = vehicleStateStore;
        _vehicleChangedSubscription = eventAggregator
            .GetEvent<VehicleStateChangedEvent>()
            .Subscribe(_ => VehiclesChanged?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? VehiclesChanged;

    public IReadOnlyList<VehicleStatusSnapshot> GetVehicles() =>
        _vehicleStateStore.GetAllVehicles()
            .Select(Clone)
            .OrderBy(vehicle => vehicle.VehicleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public VehicleStatusSnapshot? GetVehicle(string vehicleId)
    {
        var snapshot = _vehicleStateStore.GetVehicle(vehicleId);
        return snapshot is null ? null : Clone(snapshot);
    }

    public void UpsertVehicle(MockVehicleUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (string.IsNullOrWhiteSpace(update.VehicleId))
        {
            throw new ArgumentException("VehicleId cannot be empty.", nameof(update));
        }

        var snapshot = _vehicleStateStore.GetVehicle(update.VehicleId);
        snapshot = snapshot is null ? CreateDefaultSnapshot(update.VehicleId) : Clone(snapshot);

        if (update.Brand is not null)
        {
            snapshot.Brand = update.Brand;
        }

        if (update.State.HasValue)
        {
            snapshot.State = update.State.Value;
            snapshot.IsOnline = update.State.Value != RobotState.Offline;
            if (update.State.Value == RobotState.Fault)
            {
                snapshot.HasAlarm = true;
                snapshot.ActiveAlarmCode ??= "MOCK_FAULT";
                snapshot.ActiveAlarmMessage ??= "Mock fault";
            }
            else if (update.State.Value != RobotState.Fault && update.HasAlarm is not true)
            {
                snapshot.HasAlarm = false;
                snapshot.ActiveAlarmCode = null;
                snapshot.ActiveAlarmMessage = null;
            }
        }

        if (update.LoadState.HasValue)
        {
            snapshot.LoadState = update.LoadState.Value;
        }

        if (update.BatteryLevel.HasValue)
        {
            snapshot.BatteryLevel = Math.Clamp(update.BatteryLevel.Value, 0, 100);
        }

        if (update.Location is not null)
        {
            snapshot.Location = update.Location;
        }

        if (update.Position is not null)
        {
            snapshot.Position = update.Position.Clone();
        }

        if (update.CurrentTaskId is not null)
        {
            snapshot.CurrentTaskId = string.IsNullOrWhiteSpace(update.CurrentTaskId) ? null : update.CurrentTaskId;
        }

        if (update.IsOnline.HasValue)
        {
            snapshot.IsOnline = update.IsOnline.Value;
            if (!update.IsOnline.Value)
            {
                snapshot.State = RobotState.Offline;
            }
            else if (snapshot.State == RobotState.Offline)
            {
                snapshot.State = RobotState.Idle;
            }
        }

        if (update.IsCharging.HasValue)
        {
            snapshot.IsCharging = update.IsCharging.Value;
        }

        if (update.HasAlarm.HasValue)
        {
            snapshot.HasAlarm = update.HasAlarm.Value;
            snapshot.ActiveAlarmCode = update.HasAlarm.Value ? update.ActiveAlarmCode ?? snapshot.ActiveAlarmCode ?? "MOCK_ALARM" : null;
            snapshot.ActiveAlarmMessage = update.HasAlarm.Value ? update.ActiveAlarmMessage ?? snapshot.ActiveAlarmMessage ?? "Mock alarm" : null;
            if (!update.HasAlarm.Value && snapshot.State == RobotState.Fault)
            {
                snapshot.State = RobotState.Idle;
                snapshot.IsOnline = true;
            }
        }

        if (update.ActiveAlarmCode is not null)
        {
            snapshot.ActiveAlarmCode = string.IsNullOrWhiteSpace(update.ActiveAlarmCode) ? null : update.ActiveAlarmCode;
        }

        if (update.ActiveAlarmMessage is not null)
        {
            snapshot.ActiveAlarmMessage = string.IsNullOrWhiteSpace(update.ActiveAlarmMessage) ? null : update.ActiveAlarmMessage;
        }

        if (update.Telemetry is not null)
        {
            snapshot.Telemetry = new Dictionary<string, string>(update.Telemetry, StringComparer.OrdinalIgnoreCase);
        }

        snapshot.ReportedAt = DateTime.Now;
        _vehicleStateStore.UpsertStatus(snapshot);
    }

    public void Dispose()
    {
        _vehicleChangedSubscription.Dispose();
    }

    private static VehicleStatusSnapshot CreateDefaultSnapshot(string vehicleId) => new()
    {
        VehicleId = vehicleId,
        Brand = "Mock",
        State = RobotState.Idle,
        LoadState = VehicleLoadState.Unknown,
        BatteryLevel = 100,
        Location = "Unassigned",
        IsOnline = true,
        ReportedAt = DateTime.Now,
        Telemetry = new Dictionary<string, string>()
    };

    private static VehicleStatusSnapshot Clone(VehicleStatusSnapshot source) => source.Clone();
}
