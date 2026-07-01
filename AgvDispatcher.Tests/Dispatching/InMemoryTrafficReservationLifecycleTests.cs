using AgvDispatcher.Application.Dispatching;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Planning.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Mock.Planning;
using AgvDispatcher.Infrastructure.Mock.Reservations;
using AgvDispatcher.Infrastructure.Mock.Traffic;
using AgvDispatcher.Infrastructure.Sqlite.Services;
using Xunit;

namespace AgvDispatcher.Tests.Dispatching;

public sealed class InMemoryTrafficReservationLifecycleTests
{
    private static readonly RequestContext Context = new() { SourceModule = nameof(InMemoryTrafficReservationLifecycleTests) };

    [Fact]
    public async Task MockTaskExecutionSimulator_ShouldAdvanceRollingWindowDuringExecution()
    {
        var fixture = CreateFixture();
        var started = await fixture.Service.StartTaskAsync(StartRequest("TASK-001", "AGV-001"));
        Assert.True(started.Success);
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E3"));

        fixture.Simulator.Start(fixture.Tasks.GetTask("TASK-001")!, "AGV-001");
        try
        {
            var execution = await WaitForExecutionAsync(fixture.Service, "TASK-001", item => item.CurrentSegmentSequence >= 1);
            Assert.Equal(1, execution.CurrentSegmentSequence);
            Assert.Equal(DispatchExecutionState.Running, execution.State);
            Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E1"));
            Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E2"));
            Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E3"));
        }
        finally
        {
            fixture.Simulator.Cancel("TASK-001");
            await fixture.Service.CancelTaskAsync(new CancelDispatchTaskRequest
            {
                Context = Context,
                TaskId = "TASK-001",
                Reason = "Test cleanup"
            });
        }
    }

    [Fact]
    public async Task AdvanceRouteAsync_ShouldKeepCurrentNodeOccupied_WhenReleasingPassedSegment()
    {
        var fixture = CreateFixture();
        Assert.True((await fixture.Service.StartTaskAsync(StartRequest("TASK-001", "AGV-001"))).Success);
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Locked, await NodeStateAsync(fixture.Traffic, "N2"));
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Locked, await NodeStateAsync(fixture.Traffic, "N3"));

        var firstAdvance = await fixture.Service.AdvanceRouteAsync(new AdvanceDispatchRouteRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            VehicleId = "AGV-001",
            CurrentNodeId = "N2",
            PassedSegmentSequence = 1,
            AcquireNextWindow = true
        });

        Assert.True(firstAdvance.Success);
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Occupied, await NodeStateAsync(fixture.Traffic, "N2"));
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Locked, await NodeStateAsync(fixture.Traffic, "N3"));
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E3"));
        Assert.Equal(TrafficResourceState.Locked, await NodeStateAsync(fixture.Traffic, "N4"));

        var secondAdvance = await fixture.Service.AdvanceRouteAsync(new AdvanceDispatchRouteRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            VehicleId = "AGV-001",
            CurrentNodeId = "N3",
            PassedSegmentSequence = 2,
            AcquireNextWindow = true
        });

        Assert.True(secondAdvance.Success);
        Assert.Equal(TrafficResourceState.Free, await NodeStateAsync(fixture.Traffic, "N2"));
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Occupied, await NodeStateAsync(fixture.Traffic, "N3"));
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E3"));
        Assert.Equal(TrafficResourceState.Locked, await NodeStateAsync(fixture.Traffic, "N4"));
    }

    [Fact]
    public async Task SecondVehicle_ShouldWait_WhenFirstVehicleOccupiesCurrentNode()
    {
        var fixture = CreateFixture();
        Assert.True((await fixture.Service.StartTaskAsync(StartRequest("TASK-001", "AGV-001"))).Success);
        Assert.True((await fixture.Service.AdvanceRouteAsync(new AdvanceDispatchRouteRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            VehicleId = "AGV-001",
            CurrentNodeId = "N2",
            PassedSegmentSequence = 1,
            AcquireNextWindow = true
        })).Success);
        Assert.Equal(TrafficResourceState.Occupied, await NodeStateAsync(fixture.Traffic, "N2"));

        fixture.Tasks.AddTask("TASK-002");
        var second = await fixture.Service.StartTaskAsync(StartRequest("TASK-002", "AGV-002"));

        Assert.True(second.Success);
        Assert.False(second.Data!.FirstWindowLocked);
        Assert.False(second.Data.VehicleCommandSent);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, second.Data.Execution.State);
        Assert.Equal(TaskState.Pending, fixture.Tasks.GetTask("TASK-002")!.State);
    }

    [Fact]
    public async Task SecondVehicle_ShouldWait_WhenFirstVehicleLocksSameResource()
    {
        var fixture = CreateFixture();
        fixture.Tasks.AddTask("TASK-002");
        Assert.True((await fixture.Service.StartTaskAsync(StartRequest("TASK-001", "AGV-001"))).Success);

        var second = await fixture.Service.StartTaskAsync(StartRequest("TASK-002", "AGV-002"));

        Assert.True(second.Success);
        Assert.False(second.Data!.FirstWindowLocked);
        Assert.False(second.Data.VehicleCommandSent);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, second.Data.Execution.State);
        Assert.Equal(TaskState.Pending, fixture.Tasks.GetTask("TASK-002")!.State);
    }

    [Fact]
    public async Task RetryWaitingTaskAsync_ShouldStart_WhenResourceReleased()
    {
        var fixture = CreateFixture();
        fixture.Tasks.AddTask("TASK-002");
        Assert.True((await fixture.Service.StartTaskAsync(StartRequest("TASK-001", "AGV-001"))).Success);
        var waiting = await fixture.Service.StartTaskAsync(StartRequest("TASK-002", "AGV-002"));
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, waiting.Data!.Execution.State);

        Assert.True((await fixture.Service.CompleteTaskAsync(new CompleteDispatchTaskRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            VehicleId = "AGV-001",
            CurrentNodeId = "N4"
        })).Success);
        var retried = await fixture.Service.RetryWaitingTaskAsync(new RetryWaitingDispatchRequest
        {
            Context = Context,
            TaskId = "TASK-002"
        });

        Assert.True(retried.Success);
        Assert.True(retried.Data!.FirstWindowLocked);
        Assert.True(retried.Data.VehicleCommandSent);
        Assert.Equal(DispatchExecutionState.Running, retried.Data.Execution.State);
        Assert.Equal(TaskState.Running, fixture.Tasks.GetTask("TASK-002")!.State);
    }

    [Fact]
    public async Task BlockedResource_ShouldRequireReplan()
    {
        var fixture = CreateFixture();
        await fixture.Traffic.BlockResourcesAsync(new TrafficBlockRequest
        {
            Context = Context,
            Resources = new[] { Resource(TrafficResourceType.Edge, "E1") },
            Reason = "Maintenance"
        });

        var result = await fixture.Service.StartTaskAsync(StartRequest("TASK-001", "AGV-001"));

        Assert.False(result.Success);
        Assert.Equal(DispatchOrchestrationFailureCode.ReplanRequired.ToString(), result.Error!.Code);
        Assert.Empty(fixture.Adapter.Commands);
    }

    [Fact]
    public async Task CancelTaskAsync_ShouldReleaseAllLockedResources()
    {
        var fixture = CreateFixture();
        Assert.True((await fixture.Service.StartTaskAsync(StartRequest("TASK-001", "AGV-001"))).Success);

        var canceled = await fixture.Service.CancelTaskAsync(new CancelDispatchTaskRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            Reason = "Operator canceled"
        });

        Assert.True(canceled.Success);
        Assert.Equal(TaskState.Cancelled, fixture.Tasks.GetTask("TASK-001")!.State);
        var execution = await GetExecutionAsync(fixture.Service, "TASK-001");
        Assert.Equal(DispatchExecutionState.Canceled, execution.State);
        await AssertAllEdgesFreeAsync(fixture.Traffic);
        Assert.Equal(DispatchCommandType.CancelTask, fixture.Adapter.Commands.Last().CommandType);
    }

    [Fact]
    public async Task CompleteTaskAsync_ShouldReleaseAllRemainingResources()
    {
        var fixture = CreateFixture();
        fixture.Tasks.AddTask("TASK-002");
        Assert.True((await fixture.Service.StartTaskAsync(StartRequest("TASK-001", "AGV-001"))).Success);
        Assert.True((await fixture.Service.AdvanceRouteAsync(new AdvanceDispatchRouteRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            VehicleId = "AGV-001",
            CurrentNodeId = "N2",
            PassedSegmentSequence = 1
        })).Success);

        var completed = await fixture.Service.CompleteTaskAsync(new CompleteDispatchTaskRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            VehicleId = "AGV-001",
            CurrentNodeId = "N4"
        });

        Assert.True(completed.Success);
        Assert.Equal(TaskState.Completed, fixture.Tasks.GetTask("TASK-001")!.State);
        Assert.Equal(DispatchExecutionState.Completed, (await GetExecutionAsync(fixture.Service, "TASK-001")).State);
        await AssertAllEdgesFreeAsync(fixture.Traffic);
        var next = await fixture.Service.StartTaskAsync(StartRequest("TASK-002", "AGV-001"));
        Assert.True(next.Success);
        Assert.True(next.Data!.FirstWindowLocked);
    }

    private static Fixture CreateFixture()
    {
        var tasks = new FakeTaskService();
        tasks.AddTask("TASK-001");
        var vehicles = new FakeVehicleService();
        var traffic = new MockTrafficControlService();
        IRouteReservationService reservations = new MockRouteReservationService(traffic);
        var adapter = new FakeVehicleAdapterManager();
        var service = new DispatchOrchestrationService(
            tasks,
            vehicles,
            new FakeDispatchScoringService(),
            new FakeMapService(CreateLinearMap()),
            new DijkstraPathPlanner(),
            traffic,
            reservations,
            adapter);
        var simulator = new MockTaskExecutionSimulator(
            tasks,
            adapter,
            new FakeAuditTrailService(),
            new FakeChargeStationRepository(),
            new FakeVehicleStateStore(),
            service,
            reservations);
        return new Fixture(service, simulator, tasks, traffic, adapter);
    }

    private static StartDispatchTaskRequest StartRequest(string taskId, string vehicleId) => new()
    {
        Context = Context,
        TaskId = taskId,
        PreferredVehicleId = vehicleId,
        RollingWindowSize = 2
    };

    private static async Task<AgvDispatcher.Core.Contracts.Dispatching.Models.DispatchExecutionDto> WaitForExecutionAsync(
        DispatchOrchestrationService service,
        string taskId,
        Func<AgvDispatcher.Core.Contracts.Dispatching.Models.DispatchExecutionDto, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        while (!timeout.IsCancellationRequested)
        {
            var execution = await GetExecutionAsync(service, taskId);
            if (predicate(execution)) return execution;
            await Task.Delay(50, timeout.Token);
        }

        throw new TimeoutException($"Execution for {taskId} did not reach the expected state.");
    }

    private static async Task<AgvDispatcher.Core.Contracts.Dispatching.Models.DispatchExecutionDto> GetExecutionAsync(
        DispatchOrchestrationService service,
        string taskId)
    {
        var result = await service.GetExecutionAsync(new GetDispatchExecutionRequest { Context = Context, TaskId = taskId });
        Assert.True(result.Success);
        return result.Data!;
    }

    private static async Task<TrafficResourceState> EdgeStateAsync(MockTrafficControlService traffic, string edgeId)
    {
        var result = await traffic.GetResourceStatusAsync(Resource(TrafficResourceType.Edge, edgeId), Context);
        return result.Data!.State;
    }

    private static async Task<TrafficResourceState> NodeStateAsync(MockTrafficControlService traffic, string nodeId)
    {
        var result = await traffic.GetResourceStatusAsync(Resource(TrafficResourceType.Node, nodeId), Context);
        return result.Data!.State;
    }

    private static async Task AssertAllEdgesFreeAsync(MockTrafficControlService traffic)
    {
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(traffic, "E1"));
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(traffic, "E2"));
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(traffic, "E3"));
    }

    private static TrafficResourceKey Resource(TrafficResourceType type, string id) =>
        new() { ResourceType = type, ResourceId = id };

    private static MapSnapshotDto CreateLinearMap() => new()
    {
        MapId = "INMEMORY-LIFECYCLE-MAP",
        MapName = "N1 to N4 lifecycle map",
        Version = "1.0",
        Nodes = new[] { Node("N1"), Node("N2"), Node("N3"), Node("N4") },
        Edges = new[] { Edge("E1", "N1", "N2"), Edge("E2", "N2", "N3"), Edge("E3", "N3", "N4") }
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

    private sealed record Fixture(
        DispatchOrchestrationService Service,
        MockTaskExecutionSimulator Simulator,
        FakeTaskService Tasks,
        MockTrafficControlService Traffic,
        FakeVehicleAdapterManager Adapter);

    private sealed class FakeTaskService : ITaskService
    {
        private readonly Dictionary<string, TaskOrder> _tasks = new(StringComparer.OrdinalIgnoreCase);

        internal TaskOrder AddTask(string taskId)
        {
            var task = new TaskOrder
            {
                TaskId = taskId,
                State = TaskState.Pending,
                SourceNodeId = "N1",
                TargetNodeId = "N4",
                Priority = TaskPriority.Normal
            };
            _tasks[taskId] = task;
            return task;
        }

        public IReadOnlyList<TaskOrder> GetTasks() => _tasks.Values.ToArray();
        public TaskOrder? GetTask(string taskId) => _tasks.GetValueOrDefault(taskId);
        public TaskOrder CreateTask(TaskCreateRequest request) => throw new NotSupportedException();
        public void AssignVehicle(string taskId, string vehicleId) => _tasks[taskId].AssignedVehicleId = vehicleId;
        public void UpdateTaskProgress(string taskId, int progressPercent, string? currentNodeId = null)
        {
            var task = _tasks[taskId];
            task.ProgressPercent = progressPercent;
            if (!string.IsNullOrWhiteSpace(currentNodeId)) task.CurrentNodeId = currentNodeId;
        }
        public void UpdateTaskState(string taskId, TaskState state, string? reason = null) => _tasks[taskId].State = state;
        public void CancelTask(string taskId, string? reason = null) => _tasks[taskId].State = TaskState.Cancelled;
        public void RequeueInterruptedTask(string taskId, string? reason = null) { }
        public void CompleteInterruptedTaskManually(string taskId, string? reason = null) { }
        public void FailInterruptedTask(string taskId, string? reason = null) { }
    }

    private sealed class FakeVehicleService : IVehicleService
    {
        private readonly Vehicle[] _vehicles = { Vehicle("AGV-001"), Vehicle("AGV-002") };
        public IReadOnlyList<Vehicle> GetVehicles() => _vehicles;
        public Vehicle? GetVehicle(string vehicleId) => _vehicles.FirstOrDefault(item => item.VehicleId == vehicleId);
        public VehicleStatus? GetVehicleStatus(string vehicleId) => GetVehicle(vehicleId) is null ? null : Status(vehicleId);
        public IReadOnlyList<VehicleStatus> GetVehicleStatuses() => _vehicles.Select(item => Status(item.VehicleId)).ToArray();
        public void UpdateVehicleStatus(VehicleStatus status) { }
        public bool IsVehicleAvailable(string vehicleId) => GetVehicle(vehicleId) is not null;
        private static Vehicle Vehicle(string id) => new()
        {
            VehicleId = id,
            IsEnabled = true,
            AdapterType = "Mock",
            SupportedCommandFlags = VehicleCommandCapability.AssignTask | VehicleCommandCapability.MoveToNode |
                VehicleCommandCapability.CancelTask
        };
        private static VehicleStatus Status(string id) => new()
        {
            VehicleId = id,
            State = RobotState.Idle,
            IsOnline = true,
            BatteryLevel = 100,
            LocationText = "N1"
        };
    }

    private sealed class FakeDispatchScoringService : IDispatchScoringService
    {
        public DispatchScoringResult ScoreAndSelectVehicle(
            TaskOrder task,
            IEnumerable<(Vehicle Vehicle, VehicleStatus Status)> availableVehicles)
        {
            var selected = availableVehicles.FirstOrDefault().Vehicle;
            return new DispatchScoringResult { SelectedVehicleId = selected?.VehicleId, Reason = "Lifecycle test selection" };
        }
    }

    private sealed class FakeVehicleAdapterManager : IVehicleAdapterManager
    {
        internal List<DispatchCommand> Commands { get; } = new();
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<DispatchResult> SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(DispatchResult.Success("Accepted", command.TaskId, command.VehicleId, command.CommandId));
        }
    }

    private sealed class FakeMapService(MapSnapshotDto map) : IMapService
    {
        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request) => AgvResult<MapSnapshotDto>.Ok(map);
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request) => AgvResult<IReadOnlyList<MapNodeDto>>.Ok(map.Nodes);
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request) => AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(map.Edges);
        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request) => AgvResult<MapNodeDto>.Ok(map.Nodes.First(item => item.NodeId == request.NodeId));
        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request) => AgvResult<MapEdgeDto>.Ok(map.Edges.First(item => item.EdgeId == request.EdgeId));
        public AgvResult<bool> NodeExists(GetMapNodeRequest request) => AgvResult<bool>.Ok(map.Nodes.Any(item => item.NodeId == request.NodeId));
        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request) => AgvResult<bool>.Ok(map.Edges.Any(item => item.EdgeId == request.EdgeId));
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request) =>
            AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(map.Edges.Where(item => item.FromNodeId == request.NodeId).ToArray());
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request) =>
            AgvResult<IReadOnlyList<MapNodeDto>>.Ok(map.Nodes.Where(item => item.NodeType == request.NodeType).ToArray());
        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request) => AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Not configured");
        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request) => AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Not configured");
    }

    private sealed class FakeAuditTrailService : IAuditTrailService
    {
        public void Record(OperationLog log) { }
        public void RecordVehicleStatusUpdated(VehicleStatusSnapshot snapshot) { }
        public void RecordDispatchResult(DispatchResult result, string action) { }
        public void RecordAlarmRaised(AlarmEvent alarm) { }
        public void RecordAlarmAcknowledged(AlarmEvent alarm, string acknowledgedBy) { }
        public void RecordAlarmCleared(AlarmEvent alarm, string? reason) { }
    }

    private sealed class FakeVehicleStateStore : IVehicleStateStore
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _items = new(StringComparer.OrdinalIgnoreCase);
        public bool CreateVehicle(VehicleStatusSnapshot snapshot) => _items.TryAdd(snapshot.VehicleId, snapshot);
        public void UpsertStatus(VehicleStatusSnapshot snapshot) => _items[snapshot.VehicleId] = snapshot;
        public bool RemoveVehicle(string vehicleId) => _items.Remove(vehicleId);
        public VehicleStatusSnapshot? GetVehicle(string vehicleId) => _items.GetValueOrDefault(vehicleId);
        public IReadOnlyCollection<VehicleStatusSnapshot> GetAllVehicles() => _items.Values.ToArray();
    }

    private sealed class FakeChargeStationRepository : IChargeStationRepository
    {
        public Task<IReadOnlyList<ChargeStation>> GetAllAsync() => Task.FromResult<IReadOnlyList<ChargeStation>>(Array.Empty<ChargeStation>());
        public Task<ChargeStation?> GetByIdAsync(string stationId) => Task.FromResult<ChargeStation?>(null);
        public Task SaveAsync(ChargeStation station) => Task.CompletedTask;
        public Task DeleteAsync(string stationId) => Task.CompletedTask;
        public Task<IReadOnlyList<ChargeSessionRecord>> GetSessionsAsync(string? vehicleId = null, string? stationId = null) =>
            Task.FromResult<IReadOnlyList<ChargeSessionRecord>>(Array.Empty<ChargeSessionRecord>());
        public Task AddSessionAsync(ChargeSessionRecord session) => Task.CompletedTask;
        public Task UpdateSessionAsync(ChargeSessionRecord session) => Task.CompletedTask;
    }
}
