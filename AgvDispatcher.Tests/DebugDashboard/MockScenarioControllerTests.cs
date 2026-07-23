using AgvDispatcher.Application.Dispatching;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.DebugDashboard.Services;
using AgvDispatcher.Infrastructure.Mock.Planning;
using AgvDispatcher.Infrastructure.Mock.Reservations;
using AgvDispatcher.Infrastructure.Mock.Traffic;
using Xunit;

namespace AgvDispatcher.Tests.DebugDashboard;

public sealed class MockScenarioControllerTests
{
    private static readonly RequestContext Context = new() { SourceModule = nameof(MockScenarioControllerTests) };

    [Fact]
    public async Task SameTarget_ShouldPutSecondVehicleIntoWaitingForTraffic()
    {
        var fixture = CreateFixture();
        await fixture.Controller.InitializeScenarioAsync("same-target");

        Assert.True((await fixture.Controller.StartVehicleAsync(MockScenarioVehicleSlot.VehicleA)).Success);
        Assert.True((await fixture.Controller.StartVehicleAsync(MockScenarioVehicleSlot.VehicleB)).Success);

        var taskB = fixture.Controller.CurrentState.TaskBId!;
        var executionB = await GetExecutionAsync(fixture.Dispatch, taskB);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, executionB.State);
        Assert.Equal(TaskState.Pending, fixture.Tasks.GetTask(taskB)!.State);
    }

    [Fact]
    public async Task RetryWaitingTask_ShouldStartAfterFirstVehicleArrivesAndReleases()
    {
        var fixture = CreateFixture();
        await fixture.Controller.InitializeScenarioAsync("same-target");
        Assert.True((await fixture.Controller.StartVehicleAsync(MockScenarioVehicleSlot.VehicleA)).Success);
        Assert.True((await fixture.Controller.StartVehicleAsync(MockScenarioVehicleSlot.VehicleB)).Success);

        var firstArrival = await fixture.Controller.ArriveNextNodeAsync("AGV-002");
        Assert.True(firstArrival.Success, firstArrival.Message);
        var secondArrival = await fixture.Controller.ArriveNextNodeAsync("AGV-002");
        Assert.True(secondArrival.Success, secondArrival.Message);
        var retried = await fixture.Controller.RetryWaitingTaskAsync("AGV-003");

        Assert.True(retried.Success);
        var taskB = fixture.Controller.CurrentState.TaskBId!;
        var executionB = await GetExecutionAsync(fixture.Dispatch, taskB);
        Assert.Equal(DispatchExecutionState.Running, executionB.State);
        Assert.Equal(TaskState.Running, fixture.Tasks.GetTask(taskB)!.State);
    }

    [Fact]
    public async Task ManualBlock_ShouldTriggerWaitingOrReplan()
    {
        var fixture = CreateFixture();
        await fixture.Controller.InitializeScenarioAsync("manual-block");
        Assert.True((await fixture.Controller.BlockScenarioResourceAsync()).Success);

        var started = await fixture.Controller.StartVehicleAsync(MockScenarioVehicleSlot.VehicleB);
        var taskB = fixture.Controller.CurrentState.TaskBId!;
        var execution = await fixture.Dispatch.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = taskB
        });

        Assert.True(
            !started.Success ||
            execution.Data?.State is DispatchExecutionState.WaitingForTraffic or DispatchExecutionState.Replanning);
        Assert.Equal(TrafficResourceState.Blocked, await EdgeStateAsync(fixture.Traffic, "E-P2-X1"));
    }

    [Fact]
    public async Task FaultVehicleOccupancy_ShouldPreventOtherVehicleFromUsingSameResource()
    {
        var fixture = CreateFixture();
        await fixture.Controller.InitializeScenarioAsync("fault-occupancy");
        Assert.True((await fixture.Controller.StartVehicleAsync(MockScenarioVehicleSlot.VehicleA)).Success);
        Assert.True((await fixture.Controller.ArriveNextNodeAsync("AGV-002")).Success);
        fixture.Simulation.UpsertVehicle(new MockVehicleUpdate
        {
            VehicleId = "AGV-002",
            State = RobotState.Fault,
            IsOnline = true,
            HasAlarm = true,
            ActiveAlarmCode = "FAULT-DEMO",
            ActiveAlarmMessage = "Fault while occupying X1"
        });

        Assert.True((await fixture.Controller.StartVehicleAsync(MockScenarioVehicleSlot.VehicleB)).Success);

        var taskB = fixture.Controller.CurrentState.TaskBId!;
        var executionB = await GetExecutionAsync(fixture.Dispatch, taskB);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, executionB.State);
        var node = await fixture.Traffic.GetResourceStatusAsync(
            new TrafficResourceKey { ResourceType = TrafficResourceType.Node, ResourceId = "X1" },
            Context);
        Assert.Equal(TrafficResourceState.Occupied, node.Data!.State);
        Assert.Equal("AGV-002", node.Data.OccupiedByAgvId);
    }

    private static Fixture CreateFixture()
    {
        var tasks = new FakeTaskService();
        var vehicles = new FakeVehicleService();
        var traffic = new MockTrafficControlService();
        IRouteReservationService reservations = new MockRouteReservationService(traffic);
        var dispatch = new DispatchOrchestrationService(
            tasks,
            vehicles,
            new FakeDispatchScoringService(),
            new FakeMapService(CreateMap()),
            new DijkstraPathPlanner(),
            traffic,
            reservations,
            new FakeVehicleAdapterManager());
        var simulation = new FakeMockSimulationService();
        var controller = new MockScenarioController(
            tasks,
            traffic,
            reservations,
            dispatch,
            new FakeMapService(CreateMap()),
            simulation,
            new FakeVehicleMotionSimulationService());
        return new Fixture(controller, dispatch, tasks, traffic, simulation);
    }

    private static async Task<AgvDispatcher.Core.Contracts.Dispatching.Models.DispatchExecutionDto> GetExecutionAsync(
        DispatchOrchestrationService dispatch,
        string taskId)
    {
        var result = await dispatch.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = taskId
        });
        Assert.True(result.Success, result.Message);
        return result.Data!;
    }

    private static async Task<TrafficResourceState> EdgeStateAsync(MockTrafficControlService traffic, string edgeId)
    {
        var result = await traffic.GetResourceStatusAsync(
            new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = edgeId },
            Context);
        return result.Data!.State;
    }

    private static MapSnapshotDto CreateMap() => new()
    {
        MapId = "SCENARIO-TEST-MAP",
        MapName = "Debug scenario map",
        Version = "1.0",
        Nodes = new[] { Node("P1"), Node("P2"), Node("X1"), Node("D1") },
        Edges = new[]
        {
            Edge("E-P1-X1", "P1", "X1"),
            Edge("E-P2-X1", "P2", "X1"),
            Edge("E-X1-D1", "X1", "D1")
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

    private sealed record Fixture(
        MockScenarioController Controller,
        DispatchOrchestrationService Dispatch,
        FakeTaskService Tasks,
        MockTrafficControlService Traffic,
        FakeMockSimulationService Simulation);

    private sealed class FakeTaskService : ITaskService
    {
        private readonly Dictionary<string, TaskOrder> _tasks = new(StringComparer.OrdinalIgnoreCase);
        private int _next = 1;

        public IReadOnlyList<TaskOrder> GetTasks() => _tasks.Values.ToArray();
        public TaskOrder? GetTask(string taskId) => _tasks.GetValueOrDefault(taskId);

        public TaskOrder CreateTask(TaskCreateRequest request)
        {
            var task = new TaskOrder
            {
                TaskId = $"SCN-{_next++:000}",
                TaskNo = $"SCN-{_next:000}",
                TaskType = request.TaskType,
                Priority = request.Priority,
                State = TaskState.Pending,
                SourceNodeId = request.SourceNodeId,
                TargetNodeId = request.TargetNodeId,
                CreatedAt = DateTime.Now
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
        public void FailInterruptedTask(string taskId, string? reason = null) { }
    }

    private sealed class FakeVehicleService : IVehicleService
    {
        private readonly Vehicle[] _vehicles = { Vehicle("AGV-002"), Vehicle("AGV-003") };
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
            SupportedCommandFlags = VehicleCommandCapability.AssignTask | VehicleCommandCapability.CancelTask
        };

        private static VehicleStatus Status(string id) => new()
        {
            VehicleId = id,
            State = RobotState.Idle,
            IsOnline = true,
            BatteryLevel = 100,
            LocationText = id == "AGV-002" ? "P1" : "P2"
        };
    }

    private sealed class FakeDispatchScoringService : IDispatchScoringService
    {
        public DispatchScoringResult ScoreAndSelectVehicle(
            TaskOrder task,
            IEnumerable<(Vehicle Vehicle, VehicleStatus Status)> availableVehicles)
        {
            var selected = availableVehicles.FirstOrDefault().Vehicle;
            return new DispatchScoringResult
            {
                SelectedVehicleId = selected?.VehicleId,
                Reason = selected is null ? "No vehicle" : "Selected"
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
        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request) =>
            AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Not configured");
        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request) =>
            AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Not configured");
    }

    private sealed class FakeMockSimulationService : IMockSimulationService
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _vehicles = new(StringComparer.OrdinalIgnoreCase);
        public event EventHandler? VehiclesChanged;
        public IReadOnlyList<VehicleStatusSnapshot> GetVehicles() => _vehicles.Values.ToArray();
        public VehicleStatusSnapshot? GetVehicle(string vehicleId) => _vehicles.GetValueOrDefault(vehicleId);

        public void UpsertVehicle(MockVehicleUpdate update)
        {
            var snapshot = GetVehicle(update.VehicleId) ?? new VehicleStatusSnapshot
            {
                VehicleId = update.VehicleId,
                Brand = "Mock",
                BatteryLevel = 100,
                State = RobotState.Idle,
                IsOnline = true,
                ReportedAt = DateTime.Now,
                Telemetry = new Dictionary<string, string>()
            };

            if (update.State.HasValue) snapshot.State = update.State.Value;
            if (update.Location is not null) snapshot.Location = update.Location;
            if (update.Position is not null) snapshot.Position = update.Position.Clone();
            if (update.CurrentTaskId is not null) snapshot.CurrentTaskId = string.IsNullOrWhiteSpace(update.CurrentTaskId) ? null : update.CurrentTaskId;
            if (update.IsOnline.HasValue) snapshot.IsOnline = update.IsOnline.Value;
            if (update.HasAlarm.HasValue) snapshot.HasAlarm = update.HasAlarm.Value;
            if (update.ActiveAlarmCode is not null) snapshot.ActiveAlarmCode = update.ActiveAlarmCode;
            if (update.ActiveAlarmMessage is not null) snapshot.ActiveAlarmMessage = update.ActiveAlarmMessage;
            snapshot.ReportedAt = DateTime.Now;
            _vehicles[snapshot.VehicleId] = snapshot;
            VehiclesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeVehicleMotionSimulationService : IVehicleMotionSimulationService
    {
        public double DefaultSpeed { get; set; } = 120;
        public bool IsMoving(string vehicleId) => false;
        public Task<VehicleMotionResult> MoveToAsync(string vehicleId, MapPosition target, double? speed = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(VehicleMotionResult.Arrived());
        public bool StopVehicle(string vehicleId, string reason = "Stopped") => true;
        public void StopAll(string reason = "Stopped") { }
        public void Dispose() { }
    }
}
