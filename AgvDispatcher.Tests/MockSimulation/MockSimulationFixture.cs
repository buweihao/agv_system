using AgvDispatcher.Application.Dispatching;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Mock.Planning;
using AgvDispatcher.Infrastructure.Mock.Reservations;
using AgvDispatcher.Infrastructure.Mock.Simulation;
using AgvDispatcher.Infrastructure.Mock.Traffic;

namespace AgvDispatcher.Tests.MockSimulation;

internal sealed record MockSimulationFixture(
    MockFleetSimulationEngine Engine,
    IDispatchOrchestrationService Dispatch,
    MockSimulationFixture.FakeTaskService Tasks,
    MockSimulationFixture.FakeVehicleStateStore VehicleStateStore,
    MockTrafficControlService Traffic,
    IRouteReservationService Reservations)
{
    public static MockSimulationFixture Create(MapSnapshotDto map)
    {
        return Create(new FakeMapService(() => map));
    }

    public static MockSimulationFixture Create(MutableMockMap map)
    {
        return Create(new FakeMapService(() => map.Snapshot));
    }

    private static MockSimulationFixture Create(FakeMapService mapService)
    {
        var tasks = new FakeTaskService();
        var store = new FakeVehicleStateStore();
        var vehicles = new FakeVehicleService(store);
        var traffic = new MockTrafficControlService();
        IRouteReservationService reservations = new MockRouteReservationService(traffic);
        IDispatchOrchestrationService dispatch = new DispatchOrchestrationService(
            tasks,
            vehicles,
            new FakeDispatchScoringService(),
            mapService,
            new DijkstraPathPlanner(),
            traffic,
            reservations,
            new FakeVehicleAdapterManager());
        var engine = new MockFleetSimulationEngine(dispatch, tasks, store, reservations, traffic, mapService);
        return new MockSimulationFixture(engine, dispatch, tasks, store, traffic, reservations);
    }

    internal sealed class FakeTaskService : ITaskService
    {
        private readonly Dictionary<string, TaskOrder> _tasks = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<TaskOrder> GetTasks() => _tasks.Values.ToArray();
        public TaskOrder? GetTask(string taskId) => _tasks.GetValueOrDefault(taskId);

        public TaskOrder CreateTask(TaskCreateRequest request)
        {
            var taskId = request.Attributes.TryGetValue("MockSimulationTaskId", out var configuredTaskId)
                ? configuredTaskId
                : $"TASK-{_tasks.Count + 1:000}";
            var task = new TaskOrder
            {
                TaskId = taskId,
                TaskNo = taskId,
                TaskType = request.TaskType,
                TemplateId = request.TemplateId,
                Priority = request.Priority,
                State = TaskState.Pending,
                SourceNodeId = request.SourceNodeId,
                TargetNodeId = request.TargetNodeId,
                CargoCode = request.CargoCode,
                CargoName = request.CargoName,
                CargoWeight = request.CargoWeight,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.Now,
                Attributes = new Dictionary<string, string>(request.Attributes)
            };
            _tasks[task.TaskId] = task;
            return task;
        }

        public void AssignVehicle(string taskId, string vehicleId) => _tasks[taskId].AssignedVehicleId = vehicleId;

        public void UpdateTaskProgress(string taskId, int progressPercent, string? currentNodeId = null)
        {
            var task = _tasks[taskId];
            task.ProgressPercent = progressPercent;
            if (!string.IsNullOrWhiteSpace(currentNodeId))
            {
                task.CurrentNodeId = currentNodeId;
            }
        }

        public void UpdateTaskState(string taskId, TaskState state, string? reason = null) => _tasks[taskId].State = state;
        public void CancelTask(string taskId, string? reason = null) => _tasks[taskId].State = TaskState.Cancelled;
        public void RequeueInterruptedTask(string taskId, string? reason = null) { }
        public void CompleteInterruptedTaskManually(string taskId, string? reason = null) { }
        public void FailInterruptedTask(string taskId, string? reason = null) => _tasks[taskId].State = TaskState.Failed;
    }

    internal sealed class FakeVehicleStateStore : IVehicleStateStore
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _vehicles = new(StringComparer.OrdinalIgnoreCase);

        public bool CreateVehicle(VehicleStatusSnapshot snapshot)
        {
            if (_vehicles.ContainsKey(snapshot.VehicleId))
            {
                return false;
            }

            UpsertStatus(snapshot);
            return true;
        }

        public void UpsertStatus(VehicleStatusSnapshot snapshot) => _vehicles[snapshot.VehicleId] = snapshot;
        public bool RemoveVehicle(string vehicleId) => _vehicles.Remove(vehicleId);
        public VehicleStatusSnapshot? GetVehicle(string vehicleId) => _vehicles.GetValueOrDefault(vehicleId);
        public IReadOnlyCollection<VehicleStatusSnapshot> GetAllVehicles() => _vehicles.Values.ToArray();
    }

    private sealed class FakeVehicleService : IVehicleService
    {
        private readonly FakeVehicleStateStore _store;

        public FakeVehicleService(FakeVehicleStateStore store)
        {
            _store = store;
        }

        public IReadOnlyList<Vehicle> GetVehicles() =>
            _store.GetAllVehicles().Select(snapshot => new Vehicle
            {
                VehicleId = snapshot.VehicleId,
                Brand = snapshot.Brand,
                IsEnabled = true,
                AdapterType = "Mock",
                SupportedCommandFlags = VehicleCommandCapability.AssignTask | VehicleCommandCapability.CancelTask
            }).ToArray();

        public Vehicle? GetVehicle(string vehicleId) =>
            GetVehicles().FirstOrDefault(vehicle =>
                string.Equals(vehicle.VehicleId, vehicleId, StringComparison.OrdinalIgnoreCase));

        public VehicleStatus? GetVehicleStatus(string vehicleId)
        {
            var snapshot = _store.GetVehicle(vehicleId);
            return snapshot is null
                ? null
                : new VehicleStatus
                {
                    VehicleId = snapshot.VehicleId,
                    State = snapshot.State,
                    BatteryLevel = snapshot.BatteryLevel,
                    LocationText = snapshot.Location,
                    CurrentTaskId = snapshot.CurrentTaskId,
                    IsOnline = snapshot.IsOnline,
                    HasAlarm = snapshot.HasAlarm,
                    ReportedAt = snapshot.ReportedAt
                };
        }

        public IReadOnlyList<VehicleStatus> GetVehicleStatuses() =>
            _store.GetAllVehicles()
                .Select(snapshot => GetVehicleStatus(snapshot.VehicleId)!)
                .ToArray();

        public void UpdateVehicleStatus(VehicleStatus status)
        {
        }

        public bool IsVehicleAvailable(string vehicleId) =>
            GetVehicleStatus(vehicleId) is { State: RobotState.Idle, IsOnline: true, HasAlarm: false };
    }

    private sealed class FakeDispatchScoringService : IDispatchScoringService
    {
        public DispatchScoringResult ScoreAndSelectVehicle(
            TaskOrder task,
            IEnumerable<(Vehicle Vehicle, VehicleStatus Status)> availableVehicles)
        {
            var vehicle = availableVehicles.FirstOrDefault().Vehicle;
            return new DispatchScoringResult
            {
                SelectedVehicleId = vehicle?.VehicleId,
                Reason = vehicle is null ? "No vehicle" : "Selected for simulation test"
            };
        }
    }

    private sealed class FakeVehicleAdapterManager : IVehicleAdapterManager
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<DispatchResult> SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(DispatchResult.Success("Accepted", command.TaskId, command.VehicleId, command.CommandId));
    }

    private sealed class FakeMapService : IMapService
    {
        private readonly Func<MapSnapshotDto> _mapProvider;

        public FakeMapService(Func<MapSnapshotDto> mapProvider)
        {
            _mapProvider = mapProvider;
        }

        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request) => AgvResult<MapSnapshotDto>.Ok(_mapProvider());
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request) => AgvResult<IReadOnlyList<MapNodeDto>>.Ok(_mapProvider().Nodes);
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request) => AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_mapProvider().Edges);
        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request) => AgvResult<MapNodeDto>.Ok(_mapProvider().Nodes.First(item => item.NodeId == request.NodeId));
        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request) => AgvResult<MapEdgeDto>.Ok(_mapProvider().Edges.First(item => item.EdgeId == request.EdgeId));
        public AgvResult<bool> NodeExists(GetMapNodeRequest request) => AgvResult<bool>.Ok(_mapProvider().Nodes.Any(item => item.NodeId == request.NodeId));
        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request) => AgvResult<bool>.Ok(_mapProvider().Edges.Any(item => item.EdgeId == request.EdgeId));
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request) =>
            AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_mapProvider().Edges.Where(item => item.FromNodeId == request.NodeId).ToArray());
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request) =>
            AgvResult<IReadOnlyList<MapNodeDto>>.Ok(_mapProvider().Nodes.Where(item => item.NodeType == request.NodeType).ToArray());
        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request) =>
            AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Not configured");
        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request) =>
            AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Not configured");
    }
}

internal static class MockSimulationMaps
{
    public static MapSnapshotDto CreateSameTargetMap() => new()
    {
        MapId = "MAP-SAME-TARGET",
        MapName = "Same target",
        Version = "1.0",
        Nodes = new[] { Node("P1"), Node("P2"), Node("X"), Node("D") },
        Edges = new[]
        {
            Edge("E-P1-X", "P1", "X"),
            Edge("E-P2-X", "P2", "X"),
            Edge("E-X-D", "X", "D")
        }
    };

    public static MapSnapshotDto CreateNarrowAisleMap() => new()
    {
        MapId = "MAP-NARROW",
        MapName = "Narrow aisle",
        Version = "1.0",
        Nodes = new[] { Node("L"), Node("A"), Node("B"), Node("R") },
        Edges = new[]
        {
            Edge("E-L-A", "L", "A"),
            Edge("E-NARROW", "A", "B"),
            Edge("E-B-R", "B", "R"),
            Edge("E-R-B", "R", "B"),
            Edge("E-NARROW", "B", "A"),
            Edge("E-A-L", "A", "L")
        }
    };

    public static MapSnapshotDto CreateIntersectionMap() => new()
    {
        MapId = "MAP-INTERSECTION",
        MapName = "Intersection",
        Version = "1.0",
        Nodes = new[] { Node("A1"), Node("B1"), Node("X"), Node("A2"), Node("B2") },
        Edges = new[]
        {
            Edge("E-A1-X", "A1", "X"),
            Edge("E-X-A2", "X", "A2"),
            Edge("E-B1-X", "B1", "X"),
            Edge("E-X-B2", "X", "B2")
        }
    };

    public static MapSnapshotDto CreateAlternativeRouteMap() => new()
    {
        MapId = "MAP-ALTERNATIVE",
        MapName = "Alternative route",
        Version = "1.0",
        Nodes = new[] { Node("S"), Node("A"), Node("B"), Node("C"), Node("T") },
        Edges = new[]
        {
            Edge("E-S-A", "S", "A"),
            Edge("E-A-T", "A", "T"),
            Edge("E-S-B", "S", "B"),
            Edge("E-B-C", "B", "C"),
            Edge("E-C-T", "C", "T")
        }
    };

    public static MapSnapshotDto CreateSameTargetAlternativeMap() => new()
    {
        MapId = "MAP-SAME-TARGET-ALTERNATIVE",
        MapName = "Locked edge with alternative route",
        Version = "1.0",
        Nodes = new[] { Node("P1"), Node("P2"), Node("X"), Node("Z"), Node("Y"), Node("Q"), Node("D") },
        Edges = new[]
        {
            Edge("E-P1-X", "P1", "X"),
            Edge("E-P2-X", "P2", "X"),
            Edge("E-X-Z", "X", "Z"),
            Edge("E-Z-D", "Z", "D"),
            Edge("E-P2-Y", "P2", "Y"),
            Edge("E-Y-Q", "Y", "Q"),
            Edge("E-Q-D", "Q", "D")
        }
    };

    public static MutableMockMap CreateMutableMapWithDisableEdgeSupport() =>
        new(CreateAlternativeRouteMap());

    public static MapSnapshotDto CreateLinearMap() => new()
    {
        MapId = "MAP-LINEAR",
        MapName = "Linear",
        Version = "1.0",
        Nodes = new[] { Node("N1"), Node("N2"), Node("N3"), Node("N4") },
        Edges = new[]
        {
            Edge("E1", "N1", "N2"),
            Edge("E2", "N2", "N3"),
            Edge("E3", "N3", "N4")
        }
    };

    private static MapNodeDto Node(string id) => new()
    {
        NodeId = id,
        NodeCode = id,
        NodeName = id,
        NodeType = AgvDispatcher.Core.Contracts.Map.MapNodeType.Normal
    };

    private static MapEdgeDto Edge(string id, string from, string to) => new()
    {
        EdgeId = id,
        FromNodeId = from,
        ToNodeId = to,
        Distance = 1,
        Direction = MapEdgeDirection.OneWay
    };
}

internal sealed class MutableMockMap
{
    public MutableMockMap(MapSnapshotDto snapshot)
    {
        Snapshot = snapshot;
    }

    public MapSnapshotDto Snapshot { get; private set; }

    public void DisableEdge(string edgeId)
    {
        Snapshot = new MapSnapshotDto
        {
            MapId = Snapshot.MapId,
            MapName = Snapshot.MapName,
            Version = $"{Snapshot.Version}-disabled-{edgeId}",
            Nodes = Snapshot.Nodes,
            Areas = Snapshot.Areas,
            Edges = Snapshot.Edges
                .Select(edge => edge.EdgeId.Equals(edgeId, StringComparison.OrdinalIgnoreCase)
                    ? new MapEdgeDto
                    {
                        EdgeId = edge.EdgeId,
                        FromNodeId = edge.FromNodeId,
                        ToNodeId = edge.ToNodeId,
                        Distance = edge.Distance,
                        Direction = edge.Direction,
                        SpeedLimit = edge.SpeedLimit,
                        AreaId = edge.AreaId,
                        Enabled = false,
                        Properties = edge.Properties
                    }
                    : edge)
                .ToArray()
        };
    }
}
