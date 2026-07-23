using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Services;
using AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels;
using Prism.Events;
using Xunit;

namespace AgvDispatcher.Tests.Map;

public sealed class MapViewModelVehicleUpdateTests
{
    [Fact]
    public async Task VehicleFrame_UpdatesExistingItemWithoutRebuildingStaticLayers()
    {
        var events = new EventAggregator();
        var store = new FakeVehicleStateStore(new VehicleStatusSnapshot
        {
            VehicleId = "AGV-001",
            Location = "N1",
            State = RobotState.Running,
            BatteryLevel = 80,
            IsOnline = true
        });
        using var viewModel = new MapViewModel(
            events,
            new FakeMapService(),
            new DijkstraPathPlanningService(),
            store,
            new FakeTaskService(),
            new FakeAliasRepository());
        await viewModel.Initialization;

        var node = Assert.Single(viewModel.Nodes);
        var edge = Assert.Single(viewModel.Edges);
        var vehicle = Assert.Single(viewModel.Vehicles);
        var vehicleUpdated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vehicle.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(VehicleMapViewItem.X))
            {
                vehicleUpdated.TrySetResult();
            }
        };
        var updated = store.GetVehicle("AGV-001")!;
        updated.Position = new MapPosition { MapId = "MAIN", X = 50, Y = 25, Heading = 30 };
        store.UpsertStatus(updated);

        events.GetEvent<VehicleStateChangedEvent>().Publish(new VehicleStateChangedMessage
        {
            ChangeType = VehicleStateChangeType.Updated,
            Snapshot = updated
        });
        await vehicleUpdated.Task;

        Assert.Same(node, Assert.Single(viewModel.Nodes));
        Assert.Same(edge, Assert.Single(viewModel.Edges));
        Assert.Same(vehicle, Assert.Single(viewModel.Vehicles));
        Assert.Equal(50, vehicle.X);
        Assert.Equal(25, vehicle.Y);
        Assert.Equal(30, vehicle.Heading);
    }

    [Fact]
    public async Task RemovedVehicle_IsRemovedIncrementally()
    {
        var events = new EventAggregator();
        var store = new FakeVehicleStateStore(new VehicleStatusSnapshot
        {
            VehicleId = "AGV-001",
            Location = "N1",
            State = RobotState.Idle,
            IsOnline = true
        });
        using var viewModel = new MapViewModel(
            events,
            new FakeMapService(),
            new DijkstraPathPlanningService(),
            store,
            new FakeTaskService(),
            new FakeAliasRepository());
        await viewModel.Initialization;
        var vehicleRemoved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Vehicles.CollectionChanged += (_, args) =>
        {
            if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                vehicleRemoved.TrySetResult();
            }
        };

        events.GetEvent<VehicleStateChangedEvent>().Publish(new VehicleStateChangedMessage
        {
            ChangeType = VehicleStateChangeType.Removed,
            RemovedVehicleId = "AGV-001"
        });
        await vehicleRemoved.Task;

        Assert.Empty(viewModel.Vehicles);
        Assert.Single(viewModel.Nodes);
        Assert.Single(viewModel.Edges);
    }

    private sealed class FakeMapService : IMapService
    {
        private readonly MapSnapshotDto _map = new()
        {
            MapId = "MAIN",
            MapName = "Test",
            Version = "1",
            Nodes = new[]
            {
                new MapNodeDto { NodeId = "N1", NodeCode = "N1", NodeName = "N1", X = 10, Y = 20, Enabled = true }
            },
            Edges = new[]
            {
                new MapEdgeDto
                {
                    EdgeId = "E1",
                    FromNodeId = "N1",
                    ToNodeId = "N1",
                    Direction = MapEdgeDirection.OneWay,
                    Enabled = true,
                    Distance = 1
                }
            }
        };

        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request) => AgvResult<MapSnapshotDto>.Ok(_map);
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request) => AgvResult<IReadOnlyList<MapNodeDto>>.Ok(_map.Nodes);
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request) => AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_map.Edges);
        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request) => AgvResult<MapNodeDto>.Ok(_map.Nodes[0]);
        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request) => AgvResult<MapEdgeDto>.Ok(_map.Edges[0]);
        public AgvResult<bool> NodeExists(GetMapNodeRequest request) => AgvResult<bool>.Ok(true);
        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request) => AgvResult<bool>.Ok(true);
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request) => AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_map.Edges);
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request) => AgvResult<IReadOnlyList<MapNodeDto>>.Ok(_map.Nodes);
        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request) => AgvResult<string>.Ok(request.SystemNodeId);
        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request) => AgvResult<string>.Ok(request.VendorNodeCode);
    }

    private sealed class FakeVehicleStateStore(params VehicleStatusSnapshot[] vehicles) : IVehicleStateStore
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _vehicles =
            vehicles.ToDictionary(item => item.VehicleId, item => item.Clone(), StringComparer.OrdinalIgnoreCase);
        public bool CreateVehicle(VehicleStatusSnapshot snapshot) => _vehicles.TryAdd(snapshot.VehicleId, snapshot.Clone());
        public void UpsertStatus(VehicleStatusSnapshot snapshot) => _vehicles[snapshot.VehicleId] = snapshot.Clone();
        public bool RemoveVehicle(string vehicleId) => _vehicles.Remove(vehicleId);
        public VehicleStatusSnapshot? GetVehicle(string vehicleId) => _vehicles.GetValueOrDefault(vehicleId)?.Clone();
        public IReadOnlyCollection<VehicleStatusSnapshot> GetAllVehicles() => _vehicles.Values.Select(item => item.Clone()).ToArray();
    }

    private sealed class FakeAliasRepository : IMapLocationAliasRepository
    {
        public Task<IReadOnlyList<MapLocationAlias>> GetAllAsync() => Task.FromResult<IReadOnlyList<MapLocationAlias>>(Array.Empty<MapLocationAlias>());
        public Task<IReadOnlyList<MapLocationAlias>> GetAllAsync(string mapId, string mapVersion) => GetAllAsync();
        public Task SaveAsync(MapLocationAlias alias) => Task.CompletedTask;
        public Task DeleteAsync(string aliasId) => Task.CompletedTask;
    }

    private sealed class FakeTaskService : ITaskService
    {
        public IReadOnlyList<TaskOrder> GetTasks() => Array.Empty<TaskOrder>();
        public TaskOrder? GetTask(string taskId) => null;
        public TaskOrder CreateTask(TaskCreateRequest request) => throw new NotSupportedException();
        public void AssignVehicle(string taskId, string vehicleId) { }
        public void UpdateTaskProgress(string taskId, int progressPercent, string? currentNodeId = null) { }
        public void UpdateTaskState(string taskId, TaskState state, string? reason = null) { }
        public void CancelTask(string taskId, string? reason = null) { }
        public void RequeueInterruptedTask(string taskId, string? reason = null) { }
        public void CompleteInterruptedTaskManually(string taskId, string? reason = null) { }
        public void FailInterruptedTask(string taskId, string? reason = null) { }
    }
}
