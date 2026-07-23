using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.DebugDashboard.Services;
using Prism.Events;
using Xunit;

namespace AgvDispatcher.Tests.DebugDashboard;

public sealed class MockSimulationServiceTests
{
    [Fact]
    public void GetVehicles_ReadsFromUnifiedVehicleStateStore()
    {
        var store = new FakeVehicleStateStore();
        store.UpsertStatus(new VehicleStatusSnapshot
        {
            VehicleId = "AGV-001",
            Brand = "Mock-A",
            State = RobotState.Idle,
            Location = "N1",
            BatteryLevel = 90,
            IsOnline = true
        });
        var service = new MockSimulationService(store, new EventAggregator());

        var vehicle = Assert.Single(service.GetVehicles());

        Assert.Equal("AGV-001", vehicle.VehicleId);
        Assert.Equal("N1", vehicle.Location);
        Assert.Same(store.GetVehicle("AGV-001"), store.LastUpserted);
    }

    [Fact]
    public void UpsertVehicle_UpdatesUnifiedVehicleStateStore()
    {
        var store = new FakeVehicleStateStore();
        var service = new MockSimulationService(store, new EventAggregator());

        service.UpsertVehicle(new MockVehicleUpdate
        {
            VehicleId = "AGV-002",
            State = RobotState.Running,
            Location = "N2",
            BatteryLevel = 55,
            CurrentTaskId = "TASK-1",
            IsOnline = true
        });

        var snapshot = store.GetVehicle("AGV-002");
        Assert.NotNull(snapshot);
        Assert.Equal(RobotState.Running, snapshot.State);
        Assert.Equal("N2", snapshot.Location);
        Assert.Equal(55, snapshot.BatteryLevel);
        Assert.Equal("TASK-1", snapshot.CurrentTaskId);
        Assert.True(snapshot.IsOnline);
    }

    [Fact]
    public void UpsertVehicle_OfflineMarksVehicleOfflineInSameStore()
    {
        var store = new FakeVehicleStateStore();
        store.UpsertStatus(new VehicleStatusSnapshot
        {
            VehicleId = "AGV-003",
            Brand = "Mock-B",
            State = RobotState.Running,
            Location = "N3",
            BatteryLevel = 70,
            IsOnline = true
        });
        var service = new MockSimulationService(store, new EventAggregator());

        service.UpsertVehicle(new MockVehicleUpdate { VehicleId = "AGV-003", IsOnline = false });

        var snapshot = store.GetVehicle("AGV-003");
        Assert.NotNull(snapshot);
        Assert.Equal(RobotState.Offline, snapshot.State);
        Assert.False(snapshot.IsOnline);
    }

    [Fact]
    public void UpsertVehicle_CopiesContinuousPositionAndRefreshesReportedAt()
    {
        var store = new FakeVehicleStateStore();
        var service = new MockSimulationService(store, new EventAggregator());
        var position = new MapPosition { MapId = "MAIN", X = 0, Y = 0, Heading = 90 };

        service.UpsertVehicle(new MockVehicleUpdate
        {
            VehicleId = "AGV-004",
            Position = position
        });

        var first = store.GetVehicle("AGV-004")!;
        Assert.NotSame(position, first.Position);
        Assert.True(first.Position.HasValidCoordinates());
        Assert.Equal(0, first.Position.X);
        Assert.NotEqual(default, first.ReportedAt);

        position.X = 99;
        Assert.Equal(0, first.Position.X);
    }

    private sealed class FakeVehicleStateStore : IVehicleStateStore
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _vehicles = new(StringComparer.OrdinalIgnoreCase);

        public VehicleStatusSnapshot? LastUpserted { get; private set; }

        public bool CreateVehicle(VehicleStatusSnapshot snapshot)
        {
            if (_vehicles.ContainsKey(snapshot.VehicleId))
            {
                return false;
            }

            UpsertStatus(snapshot);
            return true;
        }

        public void UpsertStatus(VehicleStatusSnapshot snapshot)
        {
            _vehicles[snapshot.VehicleId] = snapshot;
            LastUpserted = snapshot;
        }

        public bool RemoveVehicle(string vehicleId) => _vehicles.Remove(vehicleId);

        public VehicleStatusSnapshot? GetVehicle(string vehicleId) =>
            _vehicles.TryGetValue(vehicleId, out var snapshot) ? snapshot : null;

        public IReadOnlyCollection<VehicleStatusSnapshot> GetAllVehicles() => _vehicles.Values.ToArray();
    }
}
