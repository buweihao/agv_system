using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Planning.Interfaces;
using AgvDispatcher.Core.Contracts.Planning.Requests;
using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
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

public sealed class DispatchOrchestrationServiceSmokeTests
{
    private static readonly RequestContext Context = new() { SourceModule = nameof(DispatchOrchestrationServiceSmokeTests) };

    [Fact]
    public async Task StartTaskAsync_HappyPath_ShouldPlanReserveLockAndSendCommand()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.StartTaskAsync(StartRequest(rollingWindowSize: 2));

        Assert.True(result.Success);
        Assert.True(result.Data!.PathPlanned);
        Assert.True(result.Data.ReservationCreated);
        Assert.True(result.Data.FirstWindowLocked);
        Assert.True(result.Data.VehicleCommandSent);
        Assert.Equal(DispatchExecutionState.Running, result.Data.Execution.State);
        Assert.Equal(TaskState.Running, fixture.Tasks.Task.State);
        Assert.Single(fixture.Adapter.Commands);
        var command = fixture.Adapter.Commands[0];
        Assert.Equal(DispatchCommandType.AssignTask, command.CommandType);
        Assert.Equal("TASK-001", command.TaskId);
        Assert.Equal("AGV-001", command.VehicleId);
        Assert.Contains("PlanId", command.Parameters.Keys);
        Assert.Contains("ReservationId", command.Parameters.Keys);
        Assert.Contains("MapVersion", command.Parameters.Keys);
        Assert.Equal(TrafficResourceState.Locked, await GetEdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Locked, await GetEdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E3"));
    }

    [Fact]
    public async Task StartTaskAsync_UnreachablePath_ShouldFailAndNotSendCommand()
    {
        var fixture = CreateFixture(map: CreateUnreachableMap());

        var result = await fixture.Service.StartTaskAsync(StartRequest());

        Assert.False(result.Success);
        Assert.Equal(DispatchOrchestrationFailureCode.PathNotReachable.ToString(), result.Error!.Code);
        Assert.Empty(fixture.Adapter.Commands);
        Assert.Equal(TaskState.Pending, fixture.Tasks.Task.State);
        var reservations = await fixture.Reservations.GetReservationsByTaskAsync(
            new AgvDispatcher.Core.Contracts.Reservations.Requests.GetTaskRouteReservationsRequest
            {
                Context = Context,
                TaskId = "TASK-001"
            });
        Assert.Empty(reservations.Data!);
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E1"));
    }

    [Fact]
    public async Task StartTaskAsync_FirstWindowConflict_ShouldWaitAndNotSendCommand()
    {
        var traffic = new MockTrafficControlService();
        var planner = new LockAfterPlanningPathPlanner(new DijkstraPathPlanner(), traffic);
        var fixture = CreateFixture(traffic: traffic, planner: planner);

        var result = await fixture.Service.StartTaskAsync(StartRequest());

        Assert.True(result.Success);
        Assert.False(result.Data!.FirstWindowLocked);
        Assert.False(result.Data.VehicleCommandSent);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, result.Data.Execution.State);
        Assert.Empty(fixture.Adapter.Commands);
        Assert.Equal(TaskState.Pending, fixture.Tasks.Task.State);
    }

    [Fact]
    public async Task StartTaskAsync_CommandSendFailed_ShouldReleaseReservationAndKeepTaskPending()
    {
        var fixture = CreateFixture();
        fixture.Adapter.ShouldFailSendCommand = true;

        var result = await fixture.Service.StartTaskAsync(StartRequest(rollingWindowSize: 2));

        Assert.False(result.Success);
        Assert.Equal(DispatchOrchestrationFailureCode.VehicleCommandFailed.ToString(), result.Error!.Code);
        Assert.Equal(TaskState.Pending, fixture.Tasks.Task.State);
        Assert.Null(fixture.Tasks.Task.AssignedVehicleId);
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E3"));
        var execution = await fixture.Service.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = "TASK-001"
        });
        Assert.True(execution.Success);
        Assert.Equal(DispatchExecutionState.Failed, execution.Data!.State);
        Assert.Equal(DispatchOrchestrationFailureCode.VehicleCommandFailed.ToString(), execution.Data.LastFailureCode);
    }

    [Fact]
    public async Task AdvanceRouteAsync_ShouldReleasePassedResourcesAndAcquireNextWindow()
    {
        var fixture = CreateFixture();
        var started = await fixture.Service.StartTaskAsync(StartRequest(rollingWindowSize: 2));
        Assert.True(started.Success);

        var result = await fixture.Service.AdvanceRouteAsync(new AdvanceDispatchRouteRequest
        {
            Context = Context,
            TaskId = fixture.Tasks.Task.TaskId,
            VehicleId = fixture.Vehicles.Vehicle.VehicleId,
            CurrentNodeId = "N2",
            PassedSegmentSequence = 1,
            AcquireNextWindow = true
        });

        Assert.True(result.Success);
        Assert.True(result.Data!.ReleasedPassedResources);
        Assert.True(result.Data.AcquiredNextWindow);
        Assert.Equal(DispatchExecutionState.Running, result.Data.Execution.State);
        Assert.Equal(1, result.Data.Execution.CurrentSegmentSequence);
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Locked, await GetEdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Locked, await GetEdgeStateAsync(fixture.Traffic, "E3"));
    }

    [Fact]
    public async Task CancelTaskAsync_ShouldReleaseReservationAndCancelTask()
    {
        var fixture = CreateFixture();
        var started = await fixture.Service.StartTaskAsync(StartRequest());
        Assert.True(started.Success);

        var result = await fixture.Service.CancelTaskAsync(new CancelDispatchTaskRequest
        {
            Context = Context,
            TaskId = fixture.Tasks.Task.TaskId,
            Reason = "Smoke test cancellation"
        });

        Assert.True(result.Success);
        Assert.Equal(TaskState.Cancelled, fixture.Tasks.Task.State);
        Assert.Equal(DispatchCommandType.CancelTask, fixture.Adapter.Commands.Last().CommandType);
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E3"));
        var execution = await fixture.Service.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = fixture.Tasks.Task.TaskId
        });
        Assert.Equal(DispatchExecutionState.Canceled, execution.Data!.State);
    }

    [Fact]
    public async Task GetExecutionAsync_ShouldReturnCurrentExecution()
    {
        var fixture = CreateFixture();
        var started = await fixture.Service.StartTaskAsync(StartRequest(rollingWindowSize: 2));
        Assert.True(started.Success);

        var result = await fixture.Service.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = "TASK-001"
        });

        Assert.True(result.Success);
        Assert.Equal("TASK-001", result.Data!.TaskId);
        Assert.Equal("AGV-001", result.Data.VehicleId);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.PlanId));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.ReservationId));
        Assert.Equal(DispatchExecutionState.Running, result.Data.State);
    }

    [Fact]
    public async Task StartTaskAsync_TaskNotFound_ShouldFail()
    {
        var fixture = CreateFixture(includeTask: false);

        var result = await fixture.Service.StartTaskAsync(StartRequest());

        Assert.False(result.Success);
        Assert.Equal(DispatchOrchestrationFailureCode.TaskNotFound.ToString(), result.Error!.Code);
        Assert.Empty(fixture.Adapter.Commands);
    }

    [Fact]
    public async Task StartTaskAsync_TaskNotPending_ShouldFail()
    {
        var fixture = CreateFixture(taskState: TaskState.Running);

        var result = await fixture.Service.StartTaskAsync(StartRequest());

        Assert.False(result.Success);
        Assert.Equal(DispatchOrchestrationFailureCode.TaskNotDispatchable.ToString(), result.Error!.Code);
        Assert.Empty(fixture.Adapter.Commands);
    }

    [Fact]
    public async Task StartTaskAsync_MapUnavailable_ShouldFail()
    {
        var fixture = CreateFixture(mapUnavailable: true);

        var result = await fixture.Service.StartTaskAsync(StartRequest());

        Assert.False(result.Success);
        Assert.Equal(DispatchOrchestrationFailureCode.MapUnavailable.ToString(), result.Error!.Code);
        Assert.Empty(fixture.Adapter.Commands);
    }

    [Fact]
    public async Task AdvanceRouteAsync_UnknownExecution_ShouldFail()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.AdvanceRouteAsync(new AdvanceDispatchRouteRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            VehicleId = "AGV-001",
            CurrentNodeId = "N1"
        });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CancelTaskAsync_WithoutExecution_ShouldCancelPendingTask()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.CancelTaskAsync(new CancelDispatchTaskRequest
        {
            Context = Context,
            TaskId = "TASK-001",
            Reason = "test cancel"
        });

        Assert.True(result.Success);
        Assert.Equal(TaskState.Cancelled, fixture.Tasks.Task.State);
        Assert.Empty(fixture.Adapter.Commands);
        Assert.Equal(TrafficResourceState.Free, await GetEdgeStateAsync(fixture.Traffic, "E1"));
        var execution = await fixture.Service.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = "TASK-001"
        });
        Assert.False(execution.Success);
    }

    [Fact]
    public async Task RetryWaitingTaskAsync_WhenResourceStillBusy_ShouldRemainWaitingAndNotSendCommand()
    {
        var (fixture, waiting) = await CreateWaitingFixtureAsync();

        var result = await fixture.Service.RetryWaitingTaskAsync(new RetryWaitingDispatchRequest
        {
            Context = Context,
            TaskId = "TASK-001"
        });

        Assert.True(result.Success);
        Assert.True(result.Data!.ShouldWait);
        Assert.False(result.Data.FirstWindowLocked);
        Assert.False(result.Data.VehicleCommandSent);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, result.Data.Execution.State);
        Assert.Equal(waiting.Data!.Execution.ExecutionId, result.Data.Execution.ExecutionId);
        Assert.Equal(waiting.Data.ReservationId, result.Data.ReservationId);
        Assert.Empty(fixture.Adapter.Commands);
    }

    [Fact]
    public async Task RetryWaitingTaskAsync_WhenResourceReleased_ShouldLockAndSendCommand()
    {
        var (fixture, waiting) = await CreateWaitingFixtureAsync();
        var release = await fixture.Traffic.ReleaseAsync(new TrafficReleaseRequest
        {
            Context = Context,
            AgvId = "AGV-OTHER",
            TaskId = "TASK-OTHER",
            Reason = "Allow waiting dispatch to retry"
        });
        Assert.True(release.Success);

        var result = await fixture.Service.RetryWaitingTaskAsync(new RetryWaitingDispatchRequest
        {
            Context = Context,
            TaskId = "TASK-001"
        });

        Assert.True(result.Success);
        Assert.True(result.Data!.FirstWindowLocked);
        Assert.True(result.Data.VehicleCommandSent);
        Assert.False(result.Data.ShouldWait);
        Assert.Equal(DispatchExecutionState.Running, result.Data.Execution.State);
        Assert.Equal(TaskState.Running, fixture.Tasks.Task.State);
        Assert.Equal(waiting.Data!.Execution.ExecutionId, result.Data.Execution.ExecutionId);
        Assert.Equal(waiting.Data.ReservationId, result.Data.ReservationId);
        Assert.Single(fixture.Adapter.Commands);
        Assert.Equal(DispatchCommandType.AssignTask, fixture.Adapter.Commands[0].CommandType);
    }

    [Fact]
    public async Task RetryWaitingTaskAsync_UnknownExecution_ShouldFail()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.RetryWaitingTaskAsync(new RetryWaitingDispatchRequest
        {
            Context = Context,
            TaskId = "TASK-001"
        });

        Assert.False(result.Success);
        Assert.Equal(DispatchOrchestrationFailureCode.TaskNotFound.ToString(), result.Error!.Code);
    }

    [Fact]
    public async Task RetryWaitingTaskAsync_NotWaitingState_ShouldFail()
    {
        var fixture = CreateFixture();
        var started = await fixture.Service.StartTaskAsync(StartRequest());
        Assert.True(started.Success);

        var result = await fixture.Service.RetryWaitingTaskAsync(new RetryWaitingDispatchRequest
        {
            Context = Context,
            TaskId = "TASK-001"
        });

        Assert.False(result.Success);
        Assert.Equal(DispatchOrchestrationFailureCode.TaskNotDispatchable.ToString(), result.Error!.Code);
        Assert.Single(fixture.Adapter.Commands);
    }

    private static StartDispatchTaskRequest StartRequest(int rollingWindowSize = 1) => new()
    {
        Context = Context,
        TaskId = "TASK-001",
        PreferredVehicleId = "AGV-001",
        RollingWindowSize = rollingWindowSize
    };

    private static MapSnapshotDto CreateLinearMap() => new()
    {
        MapId = "DISPATCH-TEST-MAP",
        MapName = "N1 to N4 linear map",
        Version = "1.0",
        Nodes = new[]
        {
            Node("N1"), Node("N2"), Node("N3"), Node("N4")
        },
        Edges = new[]
        {
            Edge("E1", "N1", "N2"),
            Edge("E2", "N2", "N3"),
            Edge("E3", "N3", "N4")
        }
    };

    private static MapSnapshotDto CreateUnreachableMap()
    {
        var map = CreateLinearMap();
        return new MapSnapshotDto
        {
            MapId = map.MapId,
            MapName = "Disconnected dispatch test map",
            Version = map.Version,
            Nodes = map.Nodes,
            Edges = new[] { Edge("E1", "N1", "N2") }
        };
    }

    private static MapNodeDto Node(string id) => new()
    {
        NodeId = id,
        NodeCode = id,
        NodeName = $"Node {id}",
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

    private static Fixture CreateFixture(
        ITrafficControlService? traffic = null,
        IPathPlanner? planner = null,
        MapSnapshotDto? map = null,
        TaskState taskState = TaskState.Pending,
        bool includeTask = true,
        bool mapUnavailable = false)
    {
        traffic ??= new MockTrafficControlService();
        planner ??= new DijkstraPathPlanner();
        var tasks = new FakeTaskService(taskState, includeTask);
        var vehicles = new FakeVehicleService();
        var adapter = new FakeVehicleAdapterManager();
        IRouteReservationService reservations = new MockRouteReservationService(traffic);
        var service = new DispatchOrchestrationService(
            tasks,
            vehicles,
            new FakeDispatchScoringService(),
            new FakeMapService(map ?? CreateLinearMap(), mapUnavailable),
            planner,
            traffic,
            reservations,
            adapter);
        return new Fixture(service, tasks, vehicles, traffic, reservations, adapter);
    }

    private static async Task<(Fixture Fixture, AgvResult<AgvDispatcher.Core.Contracts.Dispatching.Results.StartDispatchTaskResultDto> Waiting)>
        CreateWaitingFixtureAsync()
    {
        var traffic = new MockTrafficControlService();
        var fixture = CreateFixture(
            traffic: traffic,
            planner: new LockAfterPlanningPathPlanner(new DijkstraPathPlanner(), traffic));
        var waiting = await fixture.Service.StartTaskAsync(StartRequest());
        Assert.True(waiting.Success);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, waiting.Data!.Execution.State);
        return (fixture, waiting);
    }

    private static async Task<TrafficResourceState> GetEdgeStateAsync(
        ITrafficControlService traffic,
        string edgeId)
    {
        var result = await traffic.GetResourceStatusAsync(
            new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = edgeId },
            Context);
        return result.Data!.State;
    }

    private sealed record Fixture(
        DispatchOrchestrationService Service,
        FakeTaskService Tasks,
        FakeVehicleService Vehicles,
        ITrafficControlService Traffic,
        IRouteReservationService Reservations,
        FakeVehicleAdapterManager Adapter);

    private sealed class FakeTaskService : ITaskService
    {
        private readonly Dictionary<string, TaskOrder> _tasks = new(StringComparer.OrdinalIgnoreCase);

        internal TaskOrder Task { get; }

        internal FakeTaskService(TaskState state, bool includeTask)
        {
            Task = new TaskOrder
            {
                TaskId = "TASK-001",
                State = state,
                SourceNodeId = "N1",
                TargetNodeId = "N4",
                Priority = TaskPriority.Normal
            };
            if (includeTask)
            {
                _tasks[Task.TaskId] = Task;
            }
        }

        public IReadOnlyList<TaskOrder> GetTasks() => _tasks.Values.ToArray();
        public TaskOrder? GetTask(string taskId) => _tasks.GetValueOrDefault(taskId);
        public TaskOrder CreateTask(TaskCreateRequest request) => throw new NotSupportedException();
        public void AssignVehicle(string taskId, string vehicleId)
        {
            if (_tasks.TryGetValue(taskId, out var task)) task.AssignedVehicleId = vehicleId;
        }
        public void UpdateTaskProgress(string taskId, int progressPercent, string? currentNodeId = null) { }
        public void UpdateTaskState(string taskId, TaskState state, string? reason = null)
        {
            if (_tasks.TryGetValue(taskId, out var task)) task.State = state;
        }
        public void CancelTask(string taskId, string? reason = null)
        {
            if (_tasks.TryGetValue(taskId, out var task)) task.State = TaskState.Cancelled;
        }
        public void RequeueInterruptedTask(string taskId, string? reason = null) { }
        public void CompleteInterruptedTaskManually(string taskId, string? reason = null) { }
        public void FailInterruptedTask(string taskId, string? reason = null) { }
    }

    private sealed class FakeVehicleService : IVehicleService
    {
        internal Vehicle Vehicle { get; } = new()
        {
            VehicleId = "AGV-001",
            IsEnabled = true,
            SupportedCommandFlags = VehicleCommandCapability.AssignTask
        };

        private VehicleStatus Status => new()
        {
            VehicleId = Vehicle.VehicleId,
            State = RobotState.Idle,
            IsOnline = true,
            BatteryLevel = 100,
            LocationText = "N1"
        };

        public IReadOnlyList<Vehicle> GetVehicles() => new[] { Vehicle };
        public Vehicle? GetVehicle(string vehicleId) => vehicleId == Vehicle.VehicleId ? Vehicle : null;
        public VehicleStatus? GetVehicleStatus(string vehicleId) => vehicleId == Vehicle.VehicleId ? Status : null;
        public IReadOnlyList<VehicleStatus> GetVehicleStatuses() => new[] { Status };
        public void UpdateVehicleStatus(VehicleStatus status) { }
        public bool IsVehicleAvailable(string vehicleId) => vehicleId == Vehicle.VehicleId;
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
                Reason = vehicle is null ? "No vehicle" : "Selected for smoke test"
            };
        }
    }

    private sealed class FakeVehicleAdapterManager : IVehicleAdapterManager
    {
        internal List<DispatchCommand> Commands { get; } = new();
        internal bool ShouldFailSendCommand { get; set; }
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<DispatchResult> SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            if (ShouldFailSendCommand)
            {
                return Task.FromResult(DispatchResult.Failure(
                    "CommandRejected",
                    "Vehicle adapter rejected the command.",
                    command.TaskId,
                    command.VehicleId));
            }

            return Task.FromResult(DispatchResult.Success(
                "Accepted",
                command.TaskId,
                command.VehicleId,
                command.CommandId));
        }
    }

    private sealed class FakeMapService : IMapService
    {
        private readonly MapSnapshotDto _map;
        private readonly bool _unavailable;

        internal FakeMapService(MapSnapshotDto map, bool unavailable)
        {
            _map = map;
            _unavailable = unavailable;
        }

        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request) =>
            _unavailable
                ? AgvResult<MapSnapshotDto>.Fail(FailureCode.MapNotLoaded, "Map unavailable")
                : AgvResult<MapSnapshotDto>.Ok(_map);

        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request) =>
            AgvResult<IReadOnlyList<MapNodeDto>>.Ok(_map.Nodes);

        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request) =>
            AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_map.Edges);

        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request)
        {
            var node = _map.Nodes.FirstOrDefault(item => item.NodeId == request.NodeId);
            return node is null
                ? AgvResult<MapNodeDto>.Fail(FailureCode.MapNodeNotFound, "Node not found")
                : AgvResult<MapNodeDto>.Ok(node);
        }

        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request)
        {
            var edge = _map.Edges.FirstOrDefault(item => item.EdgeId == request.EdgeId);
            return edge is null
                ? AgvResult<MapEdgeDto>.Fail(FailureCode.MapEdgeNotFound, "Edge not found")
                : AgvResult<MapEdgeDto>.Ok(edge);
        }

        public AgvResult<bool> NodeExists(GetMapNodeRequest request) =>
            AgvResult<bool>.Ok(_map.Nodes.Any(item => item.NodeId == request.NodeId));

        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request) =>
            AgvResult<bool>.Ok(_map.Edges.Any(item => item.EdgeId == request.EdgeId));

        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request) =>
            AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_map.Edges.Where(edge =>
                edge.FromNodeId == request.NodeId ||
                edge.Direction == MapEdgeDirection.Bidirectional && edge.ToNodeId == request.NodeId).ToArray());

        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request) =>
            AgvResult<IReadOnlyList<MapNodeDto>>.Ok(
                _map.Nodes.Where(node => node.NodeType == request.NodeType).ToArray());

        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request) =>
            AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Mapping not configured");

        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request) =>
            AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Mapping not configured");
    }

    private sealed class LockAfterPlanningPathPlanner : IPathPlanner
    {
        private readonly IPathPlanner _inner;
        private readonly ITrafficControlService _traffic;

        internal LockAfterPlanningPathPlanner(IPathPlanner inner, ITrafficControlService traffic)
        {
            _inner = inner;
            _traffic = traffic;
        }

        public async Task<AgvResult<PathPlanResult>> PlanAsync(
            PathPlanRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = await _inner.PlanAsync(request, cancellationToken);
            await _traffic.TryAcquireAsync(new TrafficAcquireRequest
            {
                Context = request.Context,
                AgvId = "AGV-OTHER",
                TaskId = "TASK-OTHER",
                LockMode = TrafficLockMode.Lock,
                Resources = new[]
                {
                    new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = "E1" }
                }
            }, cancellationToken);
            return result;
        }

        public Task<AgvResult<IReadOnlyList<PathPlanResult>>> PlanAlternativesAsync(
            PathPlanRequest request,
            CancellationToken cancellationToken = default) => _inner.PlanAlternativesAsync(request, cancellationToken);
        public Task<AgvResult<PathReachabilityResult>> CheckReachabilityAsync(
            PathReachabilityRequest request,
            CancellationToken cancellationToken = default) => _inner.CheckReachabilityAsync(request, cancellationToken);
        public Task<AgvResult<NearestNodeResult>> FindNearestReachableNodeAsync(
            NearestNodeRequest request,
            CancellationToken cancellationToken = default) => _inner.FindNearestReachableNodeAsync(request, cancellationToken);
    }
}
