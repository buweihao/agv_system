using AgvDispatcher.Application.Dispatching;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Mock.Planning;
using AgvDispatcher.Infrastructure.Mock.Reservations;
using AgvDispatcher.Infrastructure.Mock.Simulation;
using AgvDispatcher.Infrastructure.Mock.Traffic;
using Xunit;

namespace AgvDispatcher.Tests.MockSimulation;

public sealed class MockFleetSimulationEngineDispatchStartTests
{
    private static readonly RequestContext Context = new()
    {
        SourceModule = nameof(MockFleetSimulationEngineDispatchStartTests)
    };

    [Fact]
    public async Task StartSingleVehicle_ShouldCreateExecutionAndLockFirstWindow()
    {
        var fixture = CreateFixture();
        await fixture.Engine.InitializeAsync(CreateScenario(singleTask: true));

        var result = await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A");

        Assert.True(result.Succeeded, result.Message);
        Assert.Contains(result.Events, item => item.EventType == MockSimulationEventType.TaskDispatchStarted);

        var task = fixture.Tasks.GetTask("TASK-A");
        Assert.NotNull(task);
        Assert.Equal(TaskState.Running, task!.State);
        Assert.Equal("AGV-A", task.AssignedVehicleId);

        var vehicle = fixture.Engine.GetVehicleState("AGV-A");
        Assert.NotNull(vehicle);
        Assert.Equal(MockVehicleSimulationState.Running, vehicle!.State);
        Assert.Equal("TASK-A", vehicle.CurrentTaskId);
        Assert.False(string.IsNullOrWhiteSpace(vehicle.ReservationId));
        Assert.False(string.IsNullOrWhiteSpace(vehicle.PlanId));

        var execution = await fixture.Dispatch.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = "TASK-A"
        });
        Assert.True(execution.Success, execution.Message);
        Assert.Equal(DispatchExecutionState.Running, execution.Data!.State);

        var reservation = await fixture.Reservations.GetReservationAsync(new GetRouteReservationRequest
        {
            Context = Context,
            ReservationId = vehicle.ReservationId!
        });
        Assert.True(reservation.Success, reservation.Message);
        Assert.Contains(reservation.Data!.Segments, segment => segment.IsLocked);

        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E-P1-X1"));
        Assert.Equal(RobotState.Running, fixture.VehicleStateStore.GetVehicle("AGV-A")!.State);
    }

    [Fact]
    public async Task StartTwoVehiclesSameTarget_ShouldPutSecondIntoWaiting()
    {
        var fixture = CreateFixture();
        await fixture.Engine.InitializeAsync(CreateScenario(singleTask: false));

        var first = await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A");
        var second = await fixture.Engine.StartTaskAsync("AGV-B", "TASK-B");

        Assert.True(first.Succeeded, first.Message);
        Assert.True(second.Succeeded, second.Message);
        Assert.Contains(second.Events, item => item.EventType == MockSimulationEventType.WaitingForTraffic);

        var taskA = fixture.Tasks.GetTask("TASK-A");
        var taskB = fixture.Tasks.GetTask("TASK-B");
        Assert.Equal(TaskState.Running, taskA!.State);
        Assert.Equal(TaskState.Pending, taskB!.State);

        var vehicleA = fixture.Engine.GetVehicleState("AGV-A");
        var vehicleB = fixture.Engine.GetVehicleState("AGV-B");
        Assert.Equal(MockVehicleSimulationState.Running, vehicleA!.State);
        Assert.Equal(MockVehicleSimulationState.WaitingForTraffic, vehicleB!.State);
        Assert.NotNull(vehicleB.WaitingSince);
        Assert.False(string.IsNullOrWhiteSpace(vehicleB.ReservationId));

        var executionB = await fixture.Dispatch.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = "TASK-B"
        });
        Assert.True(executionB.Success, executionB.Message);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, executionB.Data!.State);
    }

    [Fact]
    public async Task StepSingleVehicle_ShouldMoveToNextNode()
    {
        var fixture = CreateLinearFixture();
        await fixture.Engine.InitializeAsync(CreateLinearScenario());
        Assert.True((await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A")).Succeeded);

        var step = await fixture.Engine.StepAsync();

        Assert.True(step.Succeeded, step.Message);
        Assert.Contains(step.Events, item => item.EventType == MockSimulationEventType.VehicleMoved);
        var vehicle = fixture.Engine.GetVehicleState("AGV-A");
        Assert.NotNull(vehicle);
        Assert.Equal("N2", vehicle!.CurrentNodeId);
        Assert.Equal(1, vehicle.CurrentSegmentSequence);
        Assert.Equal(MockVehicleSimulationState.Running, vehicle.State);
        Assert.Equal(TaskState.Running, fixture.Tasks.GetTask("TASK-A")!.State);
        Assert.Equal("N2", fixture.Tasks.GetTask("TASK-A")!.CurrentNodeId);
    }

    [Fact]
    public async Task StepSingleVehicle_ShouldReleasePassedResource()
    {
        var fixture = CreateLinearFixture();
        await fixture.Engine.InitializeAsync(CreateLinearScenario());
        Assert.True((await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A")).Succeeded);

        await fixture.Engine.StepAsync();

        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Occupied, await NodeStateAsync(fixture.Traffic, "N2"));
    }

    [Fact]
    public async Task StepSingleVehicle_ShouldAcquireNextRollingWindow()
    {
        var fixture = CreateLinearFixture();
        await fixture.Engine.InitializeAsync(CreateLinearScenario());
        Assert.True((await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A")).Succeeded);

        var step = await fixture.Engine.StepAsync();

        Assert.Contains(step.Events, item => item.EventType == MockSimulationEventType.ResourceLocked);
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E3"));
        Assert.Equal(TaskState.Running, fixture.Tasks.GetTask("TASK-A")!.State);
    }

    [Fact]
    public async Task RunUntilIdle_ShouldCompleteTask()
    {
        var fixture = CreateLinearFixture();
        await fixture.Engine.InitializeAsync(CreateLinearScenario());
        Assert.True((await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A")).Succeeded);

        var finalStep = await RunUntilIdleAsync(fixture.Engine, "AGV-A");

        Assert.True(finalStep.Succeeded, finalStep.Message);
        Assert.Contains(finalStep.Events, item => item.EventType == MockSimulationEventType.TaskCompleted);

        var vehicle = fixture.Engine.GetVehicleState("AGV-A");
        Assert.NotNull(vehicle);
        Assert.Equal(MockVehicleSimulationState.Idle, vehicle!.State);
        Assert.Equal("N4", vehicle.CurrentNodeId);

        var task = fixture.Tasks.GetTask("TASK-A");
        Assert.NotNull(task);
        Assert.Equal(TaskState.Completed, task!.State);
        Assert.Equal(100, task.ProgressPercent);
        Assert.Equal("N4", task.CurrentNodeId);

        var execution = await fixture.Dispatch.GetExecutionAsync(new GetDispatchExecutionRequest
        {
            Context = Context,
            TaskId = "TASK-A"
        });
        Assert.True(execution.Success, execution.Message);
        Assert.Equal(DispatchExecutionState.Completed, execution.Data!.State);
    }

    [Fact]
    public async Task RunUntilIdle_ShouldReleaseAllResources()
    {
        var fixture = CreateLinearFixture();
        await fixture.Engine.InitializeAsync(CreateLinearScenario());
        Assert.True((await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A")).Succeeded);

        await RunUntilIdleAsync(fixture.Engine, "AGV-A");

        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E3"));
        Assert.Equal(TrafficResourceState.Free, await NodeStateAsync(fixture.Traffic, "N2"));
        Assert.Equal(TrafficResourceState.Free, await NodeStateAsync(fixture.Traffic, "N3"));
        Assert.Equal(TrafficResourceState.Free, await NodeStateAsync(fixture.Traffic, "N4"));

        var activeReservations = await fixture.Reservations.GetActiveReservationsAsync(
            new GetRouteReservationsRequest { Context = Context });
        Assert.True(activeReservations.Success, activeReservations.Message);
        Assert.Empty(activeReservations.Data!);
    }

    private static Fixture CreateFixture()
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
            new FakeMapService(CreateMap()),
            new DijkstraPathPlanner(),
            traffic,
            reservations,
            new FakeVehicleAdapterManager());
        var engine = new MockFleetSimulationEngine(dispatch, tasks, store);
        return new Fixture(engine, dispatch, tasks, store, traffic, reservations);
    }

    private static Fixture CreateLinearFixture()
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
            new FakeMapService(CreateLinearMap()),
            new DijkstraPathPlanner(),
            traffic,
            reservations,
            new FakeVehicleAdapterManager());
        var engine = new MockFleetSimulationEngine(dispatch, tasks, store, reservations, traffic);
        return new Fixture(engine, dispatch, tasks, store, traffic, reservations);
    }

    private static MockSimulationScenario CreateScenario(bool singleTask) => new()
    {
        ScenarioId = "dispatch-start",
        Name = "Dispatch start",
        MapSnapshot = CreateMap(),
        Vehicles = new[]
        {
            new MockSimulationVehicle
            {
                VehicleId = "AGV-A",
                Brand = "Mock-A",
                StartNodeId = "P1",
                BatteryLevel = 90
            },
            new MockSimulationVehicle
            {
                VehicleId = "AGV-B",
                Brand = "Mock-B",
                StartNodeId = "P2",
                BatteryLevel = 90
            }
        },
        Tasks = singleTask
            ? new[]
            {
                new MockSimulationTask
                {
                    TaskId = "TASK-A",
                    SourceNodeId = "P1",
                    TargetNodeId = "D1",
                    AssignedVehicleId = "AGV-A"
                }
            }
            : new[]
            {
                new MockSimulationTask
                {
                    TaskId = "TASK-A",
                    SourceNodeId = "P1",
                    TargetNodeId = "D1",
                    AssignedVehicleId = "AGV-A"
                },
                new MockSimulationTask
                {
                    TaskId = "TASK-B",
                    SourceNodeId = "P2",
                    TargetNodeId = "D1",
                    AssignedVehicleId = "AGV-B"
                }
            },
        Options = new MockSimulationOptions { RollingWindowSize = 2 }
    };

    private static MockSimulationScenario CreateLinearScenario() => new()
    {
        ScenarioId = "single-step",
        Name = "Single vehicle step",
        MapSnapshot = CreateLinearMap(),
        Vehicles = new[]
        {
            new MockSimulationVehicle
            {
                VehicleId = "AGV-A",
                Brand = "Mock-A",
                StartNodeId = "N1",
                BatteryLevel = 90
            }
        },
        Tasks = new[]
        {
            new MockSimulationTask
            {
                TaskId = "TASK-A",
                SourceNodeId = "N1",
                TargetNodeId = "N4",
                AssignedVehicleId = "AGV-A"
            }
        },
        Options = new MockSimulationOptions { RollingWindowSize = 1 }
    };

    private static MapSnapshotDto CreateMap() => new()
    {
        MapId = "SIM-DISPATCH-START",
        MapName = "Simulation dispatch start map",
        Version = "1.0",
        Nodes = new[] { Node("P1"), Node("P2"), Node("X1"), Node("D1") },
        Edges = new[]
        {
            Edge("E-P1-X1", "P1", "X1"),
            Edge("E-P2-X1", "P2", "X1"),
            Edge("E-X1-D1", "X1", "D1")
        }
    };

    private static MapSnapshotDto CreateLinearMap() => new()
    {
        MapId = "SIM-LINEAR",
        MapName = "Simulation linear map",
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

    private static async Task<TrafficResourceState> EdgeStateAsync(
        MockTrafficControlService traffic,
        string edgeId)
    {
        var result = await traffic.GetResourceStatusAsync(
            new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = edgeId },
            Context);
        return result.Data!.State;
    }

    private static async Task<TrafficResourceState> NodeStateAsync(
        MockTrafficControlService traffic,
        string nodeId)
    {
        var result = await traffic.GetResourceStatusAsync(
            new TrafficResourceKey { ResourceType = TrafficResourceType.Node, ResourceId = nodeId },
            Context);
        return result.Data!.State;
    }

    private static async Task<MockSimulationTickResult> RunUntilIdleAsync(
        MockFleetSimulationEngine engine,
        string vehicleId)
    {
        MockSimulationTickResult? last = null;
        for (var i = 0; i < 10; i++)
        {
            last = await engine.StepAsync();
            if (engine.GetVehicleState(vehicleId)?.State == MockVehicleSimulationState.Idle)
            {
                return last;
            }
        }

        return last ?? new MockSimulationTickResult
        {
            Succeeded = false,
            Message = "Simulation did not run."
        };
    }

    private sealed record Fixture(
        MockFleetSimulationEngine Engine,
        IDispatchOrchestrationService Dispatch,
        FakeTaskService Tasks,
        FakeVehicleStateStore VehicleStateStore,
        MockTrafficControlService Traffic,
        IRouteReservationService Reservations);

    private sealed class FakeTaskService : ITaskService
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

    private sealed class FakeVehicleStateStore : IVehicleStateStore
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
        private readonly MapSnapshotDto _map;

        public FakeMapService(MapSnapshotDto map)
        {
            _map = map;
        }

        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request) => AgvResult<MapSnapshotDto>.Ok(_map);
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request) => AgvResult<IReadOnlyList<MapNodeDto>>.Ok(_map.Nodes);
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request) => AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_map.Edges);
        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request) => AgvResult<MapNodeDto>.Ok(_map.Nodes.First(item => item.NodeId == request.NodeId));
        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request) => AgvResult<MapEdgeDto>.Ok(_map.Edges.First(item => item.EdgeId == request.EdgeId));
        public AgvResult<bool> NodeExists(GetMapNodeRequest request) => AgvResult<bool>.Ok(_map.Nodes.Any(item => item.NodeId == request.NodeId));
        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request) => AgvResult<bool>.Ok(_map.Edges.Any(item => item.EdgeId == request.EdgeId));
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request) =>
            AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_map.Edges.Where(item => item.FromNodeId == request.NodeId).ToArray());
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request) =>
            AgvResult<IReadOnlyList<MapNodeDto>>.Ok(_map.Nodes.Where(item => item.NodeType == request.NodeType).ToArray());
        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request) =>
            AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Not configured");
        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request) =>
            AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Not configured");
    }
}
