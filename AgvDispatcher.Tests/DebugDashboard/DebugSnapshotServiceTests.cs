using System.Collections.Concurrent;
using System.Reflection;
using AgvDispatcher.Application.Dispatching;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Contracts.Dispatching.Models;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Dispatching.Results;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.DebugDashboard.Services;
using AgvDispatcher.Infrastructure.Mock.Reservations;
using AgvDispatcher.Infrastructure.Mock.Traffic;
using AgvDispatcher.Infrastructure.Okapi;
using AgvDispatcher.Infrastructure.Sqlite.Services;
using Xunit;

namespace AgvDispatcher.Tests.DebugDashboard;

public sealed class DebugSnapshotServiceTests
{
    private static readonly RequestContext Context = new() { SourceModule = "Tests" };

    [Fact]
    public async Task GetSnapshotAsync_AggregatesTasksVehiclesTrafficAndOkapi()
    {
        var task = new TaskOrder
        {
            TaskId = "T-1",
            State = TaskState.Running,
            AssignedVehicleId = "V-1",
            SourceNodeId = "N-1",
            TargetNodeId = "N-2"
        };
        var vehicle = new Vehicle { VehicleId = "V-1", VehicleCode = "AGV-1", Name = "AGV 1" };
        var status = new VehicleStatus
        {
            VehicleId = "V-1",
            State = RobotState.Running,
            IsOnline = true,
            BatteryLevel = 80,
            CurrentTaskId = "T-1"
        };
        var traffic = new MockTrafficControlService();
        await traffic.TryAcquireAsync(new TrafficAcquireRequest
        {
            Context = Context,
            AgvId = "V-1",
            TaskId = "T-1",
            LockMode = TrafficLockMode.Lock,
            Resources = new[]
            {
                new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = "E-1" }
            }
        });
        var traceStore = new InMemoryOkapiProtocolTraceStore();
        traceStore.Add(new OkapiProtocolTraceRecord
        {
            Direction = "Error",
            VehicleId = "V-1",
            CommandType = "Move",
            TaskId = "T-1",
            ErrorMessage = "timeout"
        });

        var service = new DebugSnapshotService(
            new FakeTaskService(task),
            new FakeVehicleService(vehicle, status),
            traffic,
            new MockRouteReservationService(traffic),
            new StubDispatchOrchestrationService(),
            traceStore);

        var snapshot = await service.GetSnapshotAsync();

        Assert.Single(snapshot.Tasks);
        Assert.Single(snapshot.Vehicles);
        Assert.Single(snapshot.VehicleStatuses);
        Assert.Single(snapshot.TrafficResources);
        Assert.Equal(TrafficResourceState.Locked, snapshot.TrafficResources[0].State);
        Assert.Single(snapshot.OkapiProtocolRecords);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithEmptyStores_ReturnsEmptySnapshot()
    {
        var traffic = new MockTrafficControlService();
        var service = new DebugSnapshotService(
            new FakeTaskService(),
            new FakeVehicleService(),
            traffic,
            new MockRouteReservationService(traffic),
            new StubDispatchOrchestrationService(),
            new InMemoryOkapiProtocolTraceStore());

        var snapshot = await service.GetSnapshotAsync();

        Assert.Empty(snapshot.Tasks);
        Assert.Empty(snapshot.Vehicles);
        Assert.Empty(snapshot.VehicleStatuses);
        Assert.Empty(snapshot.TrafficResources);
        Assert.Empty(snapshot.RouteReservations);
        Assert.Empty(snapshot.DispatchExecutions);
        Assert.Empty(snapshot.OkapiProtocolRecords);
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsLockedOccupiedAndBlockedResources()
    {
        var traffic = new MockTrafficControlService();
        await traffic.TryAcquireAsync(new TrafficAcquireRequest
        {
            Context = Context,
            AgvId = "V-1",
            TaskId = "T-1",
            LockMode = TrafficLockMode.Lock,
            Resources = new[] { new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = "E-1" } }
        });
        await traffic.TryAcquireAsync(new TrafficAcquireRequest
        {
            Context = Context,
            AgvId = "V-2",
            TaskId = "T-2",
            LockMode = TrafficLockMode.Occupy,
            Resources = new[] { new TrafficResourceKey { ResourceType = TrafficResourceType.Node, ResourceId = "N-2" } }
        });
        await traffic.BlockResourcesAsync(new TrafficBlockRequest
        {
            Context = Context,
            OperatorId = "tester",
            Reason = "maintenance",
            Resources = new[] { new TrafficResourceKey { ResourceType = TrafficResourceType.Node, ResourceId = "N-3" } }
        });

        var service = new DebugSnapshotService(
            new FakeTaskService(),
            new FakeVehicleService(),
            traffic,
            new MockRouteReservationService(traffic),
            new StubDispatchOrchestrationService(),
            new InMemoryOkapiProtocolTraceStore());

        var states = (await service.GetSnapshotAsync()).TrafficResources.Select(resource => resource.State).ToArray();

        Assert.Contains(TrafficResourceState.Locked, states);
        Assert.Contains(TrafficResourceState.Occupied, states);
        Assert.Contains(TrafficResourceState.Blocked, states);
    }

    [Fact]
    public async Task GetActiveExecutionsAsync_ReturnsSnapshotWithoutChangingState()
    {
        var service = new DispatchOrchestrationService(
            null!, null!, null!, null!, null!, null!, null!, null!);
        var execution = new DispatchExecutionDto
        {
            ExecutionId = "EX-1",
            TaskId = "T-1",
            VehicleId = "V-1",
            State = DispatchExecutionState.WaitingForTraffic,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };
        GetExecutions(service)["T-1"] = execution;

        var result = await service.GetActiveExecutionsAsync(new GetDispatchExecutionsRequest { Context = Context });

        Assert.True(result.Success);
        var returned = Assert.Single(result.Data!);
        Assert.Equal("EX-1", returned.ExecutionId);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, returned.State);
        Assert.Equal(DispatchExecutionState.WaitingForTraffic, GetExecutions(service)["T-1"].State);
    }

    private static ConcurrentDictionary<string, DispatchExecutionDto> GetExecutions(DispatchOrchestrationService service)
    {
        var field = typeof(DispatchOrchestrationService).GetField("_executions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<ConcurrentDictionary<string, DispatchExecutionDto>>(field.GetValue(service));
    }

    private sealed class FakeTaskService : ITaskService
    {
        private readonly List<TaskOrder> _tasks;

        public FakeTaskService(params TaskOrder[] tasks) => _tasks = tasks.ToList();

        public IReadOnlyList<TaskOrder> GetTasks() => _tasks;
        public TaskOrder? GetTask(string taskId) => _tasks.FirstOrDefault(task => task.TaskId == taskId);
        public TaskOrder CreateTask(TaskCreateRequest request) => throw new NotSupportedException();
        public void AssignVehicle(string taskId, string vehicleId) => throw new NotSupportedException();
        public void UpdateTaskProgress(string taskId, int progressPercent, string? currentNodeId = null) => throw new NotSupportedException();
        public void UpdateTaskState(string taskId, TaskState state, string? reason = null) => throw new NotSupportedException();
        public void CancelTask(string taskId, string? reason = null) => throw new NotSupportedException();
        public void RequeueInterruptedTask(string taskId, string? reason = null) => throw new NotSupportedException();
        public void CompleteInterruptedTaskManually(string taskId, string? reason = null) => throw new NotSupportedException();
        public void FailInterruptedTask(string taskId, string? reason = null) => throw new NotSupportedException();
    }

    private sealed class FakeVehicleService : IVehicleService
    {
        private readonly List<Vehicle> _vehicles;
        private readonly List<VehicleStatus> _statuses;

        public FakeVehicleService(params object[] values)
        {
            _vehicles = values.OfType<Vehicle>().ToList();
            _statuses = values.OfType<VehicleStatus>().ToList();
        }

        public IReadOnlyList<Vehicle> GetVehicles() => _vehicles;
        public Vehicle? GetVehicle(string vehicleId) => _vehicles.FirstOrDefault(vehicle => vehicle.VehicleId == vehicleId);
        public VehicleStatus? GetVehicleStatus(string vehicleId) => _statuses.FirstOrDefault(status => status.VehicleId == vehicleId);
        public IReadOnlyList<VehicleStatus> GetVehicleStatuses() => _statuses;
        public void UpdateVehicleStatus(VehicleStatus status) => throw new NotSupportedException();
        public bool IsVehicleAvailable(string vehicleId) => false;
    }

    private sealed class StubDispatchOrchestrationService : IDispatchOrchestrationService
    {
        public Task<AgvResult<StartDispatchTaskResultDto>> StartTaskAsync(StartDispatchTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult<RetryWaitingDispatchResultDto>> RetryWaitingTaskAsync(RetryWaitingDispatchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult<AdvanceDispatchRouteResultDto>> AdvanceRouteAsync(AdvanceDispatchRouteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult> CompleteTaskAsync(CompleteDispatchTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult> CancelTaskAsync(CancelDispatchTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult<DispatchExecutionDto>> GetExecutionAsync(GetDispatchExecutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AgvResult<IReadOnlyList<DispatchExecutionDto>>> GetActiveExecutionsAsync(GetDispatchExecutionsRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(AgvResult<IReadOnlyList<DispatchExecutionDto>>.Ok(Array.Empty<DispatchExecutionDto>()));
    }
}
