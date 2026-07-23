using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.DebugDashboard.Services;

public sealed class MockScenarioController : IMockScenarioController, IDisposable
{
    private const string VehicleAId = "AGV-002";
    private const string VehicleBId = "AGV-003";
    private readonly ITaskService _taskService;
    private readonly ITrafficControlService _trafficControlService;
    private readonly IRouteReservationService _routeReservationService;
    private readonly IDispatchOrchestrationService _dispatchOrchestrationService;
    private readonly IMapService _mapService;
    private readonly IMockSimulationService _mockSimulationService;
    private readonly IVehicleMotionSimulationService _vehicleMotionService;
    private readonly List<MockScenarioStepLog> _logs = new();
    private readonly Dictionary<string, CancellationTokenSource> _automaticRuns =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private ScenarioDefinition _scenario;
    private ScenarioLayout? _layout;
    private string? _taskAId;
    private string? _taskBId;
    private bool _manualBlockActive;

    private static readonly IReadOnlyList<ScenarioDefinition> Scenarios = new[]
    {
        new ScenarioDefinition(
            "same-target",
            "两车同终点",
            "A 车先锁定共享通道，B 车进入 WaitingForTraffic，等待 A 车释放后重试。",
            RollingWindowSize: 2),
        new ScenarioDefinition(
            "narrow-aisle",
            "窄道会车冲突",
            "两条路线汇入同一条单车通行窄道，演示滚动窗口锁定下的等待与继续运行。",
            RollingWindowSize: 2),
        new ScenarioDefinition(
            "fault-occupancy",
            "故障车辆占用",
            "A 车在持有路线资源时可被置为故障，B 车因同资源被占用而不能进入。",
            RollingWindowSize: 2),
        new ScenarioDefinition(
            "manual-block",
            "人工封路",
            "对 B 车首段路线资源执行人工封路，触发等待或重规划状态。",
            RollingWindowSize: 1)
    };

    public MockScenarioController(
        ITaskService taskService,
        ITrafficControlService trafficControlService,
        IRouteReservationService routeReservationService,
        IDispatchOrchestrationService dispatchOrchestrationService,
        IMapService mapService,
        IMockSimulationService mockSimulationService,
        IVehicleMotionSimulationService vehicleMotionService)
    {
        _taskService = taskService;
        _trafficControlService = trafficControlService;
        _routeReservationService = routeReservationService;
        _dispatchOrchestrationService = dispatchOrchestrationService;
        _mapService = mapService;
        _mockSimulationService = mockSimulationService;
        _vehicleMotionService = vehicleMotionService;
        _scenario = Scenarios[0];
    }

    public event EventHandler? ScenarioChanged;

    public IReadOnlyList<MockScenarioOption> GetScenarios() => Scenarios
        .Select(item => new MockScenarioOption
        {
            Key = item.Key,
            Name = item.Name,
            Description = item.Description
        })
        .ToArray();

    public MockScenarioState CurrentState
    {
        get
        {
            lock (_syncRoot)
            {
                return CreateStateNoLock();
            }
        }
    }

    public async Task<MockScenarioOperationResult> InitializeScenarioAsync(
        string scenarioKey,
        CancellationToken cancellationToken = default)
    {
        var scenario = Scenarios.FirstOrDefault(item =>
            string.Equals(item.Key, scenarioKey, StringComparison.OrdinalIgnoreCase)) ?? Scenarios[0];

        await ResetScenarioAsync(cancellationToken).ConfigureAwait(false);
        var layout = ResolveLayout();
        var taskA = _taskService.CreateTask(CreateTaskRequest(
            sourceNodeId: layout.SourceA,
            targetNodeId: layout.Target,
            cargoCode: $"{scenario.Key}-A"));
        var taskB = _taskService.CreateTask(CreateTaskRequest(
            sourceNodeId: layout.SourceB,
            targetNodeId: layout.Target,
            cargoCode: $"{scenario.Key}-B"));

        lock (_syncRoot)
        {
            _scenario = scenario;
            _layout = layout;
            _taskAId = taskA.TaskId;
            _taskBId = taskB.TaskId;
            _manualBlockActive = false;
        }

        UpsertVehicle(VehicleAId, layout.SourceA, null, RobotState.Idle, clearAlarm: true, ResolveNodePosition(layout.SourceA));
        UpsertVehicle(VehicleBId, layout.SourceB, null, RobotState.Idle, clearAlarm: true, ResolveNodePosition(layout.SourceB));
        AddLog(
            "初始化场景",
            "OK",
            $"{scenario.Name}: A={VehicleAId}/{taskA.TaskId} {layout.SourceA}->{layout.Target}; " +
            $"B={VehicleBId}/{taskB.TaskId} {layout.SourceB}->{layout.Target}");
        NotifyChanged();
        return MockScenarioOperationResult.Ok("场景已初始化。");
    }

    public async Task<MockScenarioOperationResult> StartVehicleAsync(
        MockScenarioVehicleSlot slot,
        CancellationToken cancellationToken = default)
    {
        var (vehicleId, taskId, scenario) = GetSlot(slot);
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return FailAndLog($"启动{slot}", "请先初始化演示场景。");
        }

        var result = await _dispatchOrchestrationService.StartTaskAsync(
            new StartDispatchTaskRequest
            {
                Context = Context("StartVehicle"),
                TaskId = taskId,
                PreferredVehicleId = vehicleId,
                RollingWindowSize = scenario.RollingWindowSize,
                MaxReplanCount = 0,
                SendVehicleCommand = true
            },
            cancellationToken).ConfigureAwait(false);

        if (result.Success && result.Data is not null)
        {
            if (result.Data.FirstWindowLocked)
            {
                UpsertVehicle(vehicleId, CurrentSourceFor(vehicleId), taskId, RobotState.Running, clearAlarm: false);
            }

                AddLog(
                $"启动 {vehicleId}",
                result.Data.Execution.State.ToString(),
                result.Data.Message ?? result.Message);
            NotifyChanged();
            return MockScenarioOperationResult.Ok(result.Data.Message ?? "调度已启动。");
        }

        return FailAndLog($"启动 {vehicleId}", result.Message);
    }

    public async Task<MockScenarioOperationResult> StartAutomaticVehicleAsync(
        MockScenarioVehicleSlot slot,
        CancellationToken cancellationToken = default)
    {
        var (vehicleId, _, _) = GetSlot(slot);
        lock (_syncRoot)
        {
            if (_automaticRuns.ContainsKey(vehicleId))
            {
                return MockScenarioOperationResult.Ok($"{vehicleId} is already running automatically.");
            }
        }

        var taskId = GetTaskIdForVehicle(vehicleId);
        var execution = string.IsNullOrWhiteSpace(taskId)
            ? null
            : await _dispatchOrchestrationService.GetExecutionAsync(
                new GetDispatchExecutionRequest
                {
                    Context = Context("StartAutomaticVehicle"),
                    TaskId = taskId
                },
                cancellationToken).ConfigureAwait(false);
        var alreadyDispatched = execution?.Success == true &&
            execution.Data?.State is DispatchExecutionState.Running or DispatchExecutionState.WaitingForTraffic;
        if (!alreadyDispatched)
        {
            var started = await StartVehicleAsync(slot, cancellationToken).ConfigureAwait(false);
            if (!started.Success)
            {
                return started;
            }
        }

        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_syncRoot)
        {
            if (_automaticRuns.ContainsKey(vehicleId))
            {
                linkedCancellation.Dispose();
                return MockScenarioOperationResult.Ok($"{vehicleId} is already running automatically.");
            }

            _automaticRuns[vehicleId] = linkedCancellation;
        }

        _ = RunAutomaticVehicleAsync(vehicleId, linkedCancellation);
        AddLog($"自动运行 {vehicleId}", "Started", "Continuous route motion started.");
        NotifyChanged();
        return MockScenarioOperationResult.Ok($"{vehicleId} automatic motion started.");
    }

    public MockScenarioOperationResult StopAutomaticVehicle(string vehicleId)
    {
        CancellationTokenSource? cancellation;
        lock (_syncRoot)
        {
            _automaticRuns.Remove(vehicleId, out cancellation);
        }

        cancellation?.Cancel();
        _vehicleMotionService.StopVehicle(vehicleId, "Automatic motion stopped.");
        AddLog($"停止 {vehicleId}", "OK", "Automatic route motion stopped.");
        NotifyChanged();
        return MockScenarioOperationResult.Ok($"{vehicleId} automatic motion stopped.");
    }

    public void StopAllAutomaticVehicles()
    {
        CancellationTokenSource[] cancellations;
        lock (_syncRoot)
        {
            cancellations = _automaticRuns.Values.ToArray();
            _automaticRuns.Clear();
        }

        foreach (var cancellation in cancellations)
        {
            cancellation.Cancel();
        }

        _vehicleMotionService.StopAll("All automatic motion stopped.");
        NotifyChanged();
    }

    public bool IsAutomaticRunning(string vehicleId)
    {
        lock (_syncRoot)
        {
            return _automaticRuns.ContainsKey(vehicleId);
        }
    }

    public async Task<MockScenarioOperationResult> ArriveNextNodeAsync(
        string vehicleId,
        CancellationToken cancellationToken = default)
    {
        if (IsAutomaticRunning(vehicleId))
        {
            return FailAndLog($"到达 {vehicleId}", "Automatic motion is active; manual advance is disabled.");
        }

        return await ArriveNextNodeCoreAsync(vehicleId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MockScenarioOperationResult> ArriveNextNodeCoreAsync(
        string vehicleId,
        CancellationToken cancellationToken)
    {
        var taskId = GetTaskIdForVehicle(vehicleId);
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return FailAndLog($"到达 {vehicleId}", "该车辆没有绑定演示任务。");
        }

        var executionResult = await _dispatchOrchestrationService.GetExecutionAsync(
            new GetDispatchExecutionRequest
            {
                Context = Context("ArriveNextNode"),
                TaskId = taskId
            },
            cancellationToken).ConfigureAwait(false);
        if (!executionResult.Success || executionResult.Data is null)
        {
            return FailAndLog($"到达 {vehicleId}", executionResult.Message);
        }

        var execution = executionResult.Data;
        if (execution.State == DispatchExecutionState.WaitingForTraffic)
        {
            return FailAndLog($"到达 {vehicleId}", "该调度正在等待交通资源，不能推进路线。");
        }

        var reservationResult = await _routeReservationService.GetReservationAsync(
            new GetRouteReservationRequest
            {
                Context = Context("ArriveNextNode"),
                ReservationId = execution.ReservationId ?? string.Empty
            },
            cancellationToken).ConfigureAwait(false);
        if (!reservationResult.Success || reservationResult.Data is null)
        {
            return FailAndLog($"到达 {vehicleId}", reservationResult.Message);
        }

        var next = NextSegment(reservationResult.Data, execution.CurrentSegmentSequence);
        if (next is null)
        {
            return await CompleteVehicleTaskAsync(vehicleId, taskId, execution.CurrentNodeId, cancellationToken)
                .ConfigureAwait(false);
        }

        var nextNode = next.Segment.ToNodeId;
        var isFinalSegment = IsFinalSegment(reservationResult.Data, next.Segment.Sequence);
        var advance = await _dispatchOrchestrationService.AdvanceRouteAsync(
            new AdvanceDispatchRouteRequest
            {
                Context = Context("ArriveNextNode"),
                TaskId = taskId,
                VehicleId = vehicleId,
                CurrentNodeId = nextNode,
                PassedSegmentSequence = next.Segment.Sequence,
                AcquireNextWindow = !isFinalSegment
            },
            cancellationToken).ConfigureAwait(false);
        if (!advance.Success || advance.Data is null)
        {
            return FailAndLog($"到达 {vehicleId}", advance.Message);
        }

        var progress = CalculateProgress(reservationResult.Data, next.Segment.Sequence);
        _taskService.UpdateTaskProgress(taskId, progress, nextNode);
        UpsertVehicle(vehicleId, nextNode, taskId, RobotState.Running, clearAlarm: false);
        AddLog(
            $"到达 {vehicleId}",
            advance.Data.Execution.State.ToString(),
            $"{vehicleId} 到达 {nextNode}，已释放至路线段 {next.Segment.Sequence}。");

        if (isFinalSegment)
        {
            return await CompleteVehicleTaskAsync(vehicleId, taskId, nextNode, cancellationToken)
                .ConfigureAwait(false);
        }

        NotifyChanged();
        return MockScenarioOperationResult.Ok($"{vehicleId} 已推进到 {nextNode}。");
    }

    public async Task<MockScenarioOperationResult> RetryWaitingTaskAsync(
        string vehicleId,
        CancellationToken cancellationToken = default)
    {
        var taskId = GetTaskIdForVehicle(vehicleId);
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return FailAndLog($"重试 {vehicleId}", "该车辆没有绑定演示任务。");
        }

        var result = await _dispatchOrchestrationService.RetryWaitingTaskAsync(
            new RetryWaitingDispatchRequest
            {
                Context = Context("RetryWaitingTask"),
                TaskId = taskId,
                SendVehicleCommand = true
            },
            cancellationToken).ConfigureAwait(false);
        if (result.Success && result.Data is not null)
        {
            if (result.Data.FirstWindowLocked)
            {
                var currentLocation = _mockSimulationService.GetVehicle(vehicleId)?.Location;
                UpsertVehicle(
                    vehicleId,
                    string.IsNullOrWhiteSpace(currentLocation) ? CurrentSourceFor(vehicleId) : currentLocation,
                    taskId,
                    RobotState.Running,
                    clearAlarm: false);
            }

            AddLog(
                $"重试 {vehicleId}",
                result.Data.Execution.State.ToString(),
                result.Data.Message ?? result.Message);
            NotifyChanged();
            return MockScenarioOperationResult.Ok(result.Data.Message ?? "等待任务重试完成。");
        }

        return FailAndLog($"重试 {vehicleId}", result.Message);
    }

    public async Task<MockScenarioOperationResult> BlockScenarioResourceAsync(
        CancellationToken cancellationToken = default)
    {
        var resource = CurrentState.ManualBlockResource;
        if (resource is null)
        {
            return FailAndLog("人工封路", "当前场景没有可封锁资源。");
        }

        var result = await _trafficControlService.BlockResourcesAsync(
            new TrafficBlockRequest
            {
                Context = Context("ManualBlock"),
                Resources = new[] { resource },
                Reason = "Debug dashboard scenario block",
                OperatorId = "DebugDashboard"
            },
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return FailAndLog("人工封路", result.Message);
        }

        lock (_syncRoot)
        {
            _manualBlockActive = true;
        }

        AddLog("人工封路", "OK", $"{resource.ResourceType}:{resource.ResourceId} 已封锁。");
        NotifyChanged();
        return MockScenarioOperationResult.Ok("场景资源已封锁。");
    }

    public async Task<MockScenarioOperationResult> UnblockScenarioResourceAsync(
        CancellationToken cancellationToken = default)
    {
        var resource = CurrentState.ManualBlockResource;
        if (resource is null)
        {
            return FailAndLog("人工解封", "当前场景没有可解封资源。");
        }

        var result = await _trafficControlService.UnblockResourcesAsync(
            new TrafficUnblockRequest
            {
                Context = Context("ManualUnblock"),
                Resources = new[] { resource },
                Reason = "Debug dashboard scenario unblock",
                OperatorId = "DebugDashboard"
            },
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return FailAndLog("人工解封", result.Message);
        }

        lock (_syncRoot)
        {
            _manualBlockActive = false;
        }

        AddLog("人工解封", "OK", $"{resource.ResourceType}:{resource.ResourceId} 已解封。");
        NotifyChanged();
        return MockScenarioOperationResult.Ok("场景资源已解封。");
    }

    public async Task<MockScenarioOperationResult> ReleaseCurrentOccupancyAsync(
        string vehicleId,
        CancellationToken cancellationToken = default)
    {
        var taskId = GetTaskIdForVehicle(vehicleId);
        var result = await _trafficControlService.ReleaseAsync(
            new TrafficReleaseRequest
            {
                Context = Context("ReleaseCurrentOccupancy"),
                AgvId = vehicleId,
                TaskId = taskId,
                Reason = "Debug dashboard manual vehicle release"
            },
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return FailAndLog($"释放 {vehicleId}", result.Message);
        }

        AddLog($"释放 {vehicleId}", "OK", result.Message);
        NotifyChanged();
        return MockScenarioOperationResult.Ok(result.Message);
    }

    public async Task<MockScenarioOperationResult> ResetScenarioAsync(
        CancellationToken cancellationToken = default)
    {
        StopAllAutomaticVehicles();
        var state = CurrentState;
        foreach (var taskId in new[] { state.TaskAId, state.TaskBId }.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            await CancelTaskIfActiveAsync(taskId!, cancellationToken).ConfigureAwait(false);
        }

        foreach (var vehicleId in new[] { VehicleAId, VehicleBId })
        {
            await _trafficControlService.ReleaseAsync(
                new TrafficReleaseRequest
                {
                    Context = Context("ResetScenario"),
                    AgvId = vehicleId,
                    Reason = "Debug dashboard scenario reset"
                },
                cancellationToken).ConfigureAwait(false);
        }

        if (state.ManualBlockResource is not null)
        {
            await _trafficControlService.UnblockResourcesAsync(
                new TrafficUnblockRequest
                {
                    Context = Context("ResetScenario"),
                    Resources = new[] { state.ManualBlockResource },
                    Reason = "Debug dashboard scenario reset",
                    OperatorId = "DebugDashboard"
                },
                cancellationToken).ConfigureAwait(false);
        }

        lock (_syncRoot)
        {
            _taskAId = null;
            _taskBId = null;
            _manualBlockActive = false;
            _layout = null;
            _logs.Clear();
        }

        UpsertVehicle(VehicleAId, string.Empty, null, RobotState.Idle, clearAlarm: true, new MapPosition());
        UpsertVehicle(VehicleBId, string.Empty, null, RobotState.Idle, clearAlarm: true, new MapPosition());
        AddLog("重置场景", "OK", "演示任务和交通资源已释放。");
        NotifyChanged();
        return MockScenarioOperationResult.Ok("场景已重置。");
    }

    private async Task<MockScenarioOperationResult> CompleteVehicleTaskAsync(
        string vehicleId,
        string taskId,
        string? currentNodeId,
        CancellationToken cancellationToken)
    {
        var completed = await _dispatchOrchestrationService.CompleteTaskAsync(
            new CompleteDispatchTaskRequest
            {
                Context = Context("CompleteVehicleTask"),
                TaskId = taskId,
                VehicleId = vehicleId,
                CurrentNodeId = currentNodeId,
                ReleaseReservation = true
            },
            cancellationToken).ConfigureAwait(false);

        if (!completed.Success)
        {
            return FailAndLog($"完成 {vehicleId}", completed.Message);
        }

        await _trafficControlService.ReleaseAsync(
            new TrafficReleaseRequest
            {
                Context = Context("CompleteVehicleTask"),
                AgvId = vehicleId,
                TaskId = taskId,
                Reason = "Debug dashboard completion release"
            },
            cancellationToken).ConfigureAwait(false);
        UpsertVehicle(vehicleId, currentNodeId ?? CurrentTarget(), null, RobotState.Idle, clearAlarm: false);
        AddLog($"完成 {vehicleId}", "OK", completed.Message);
        NotifyChanged();
        return MockScenarioOperationResult.Ok(completed.Message);
    }

    private async Task CancelTaskIfActiveAsync(string taskId, CancellationToken cancellationToken)
    {
        var task = _taskService.GetTask(taskId);
        if (task is null || task.State is TaskState.Completed or TaskState.Cancelled or TaskState.Failed)
        {
            return;
        }

        var execution = await _dispatchOrchestrationService.GetExecutionAsync(
            new GetDispatchExecutionRequest { Context = Context("ResetScenario"), TaskId = taskId },
            cancellationToken).ConfigureAwait(false);
        if (execution.Success)
        {
            await _dispatchOrchestrationService.CancelTaskAsync(
                new CancelDispatchTaskRequest
                {
                    Context = Context("ResetScenario"),
                    TaskId = taskId,
                    Reason = "Debug dashboard scenario reset",
                    ReleaseReservation = true,
                    SendCancelCommand = false
                },
                cancellationToken).ConfigureAwait(false);
            return;
        }

        _taskService.CancelTask(taskId, "Debug dashboard scenario reset");
    }

    private ScenarioLayout ResolveLayout()
    {
        var mapResult = _mapService.GetCurrentMap(new GetMapSnapshotRequest { Context = Context("ResolveLayout") });
        var nodeIds = mapResult.Success && mapResult.Data is not null
            ? mapResult.Data.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edgeIds = mapResult.Success && mapResult.Data is not null
            ? mapResult.Data.Edges.Select(edge => edge.EdgeId).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (HasNodes(nodeIds, "PICK-A1", "PICK-A2", "CHG-02") &&
            edgeIds.Contains("E-PICK-A2-PUT-A1"))
        {
            return new ScenarioLayout(
                "PICK-A1",
                "PICK-A2",
                "CHG-02",
                new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = "E-PICK-A2-PUT-A1" });
        }

        if (HasNodes(nodeIds, "P1", "P2", "D1"))
        {
            var blockEdge = edgeIds.Contains("E-P2-X1") ? "E-P2-X1" : "E-X1-D1";
            return new ScenarioLayout(
                "P1",
                "P2",
                "D1",
                new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = blockEdge });
        }

        var fallback = nodeIds.Take(3).ToArray();
        if (fallback.Length >= 3)
        {
            return new ScenarioLayout(
                fallback[0],
                fallback[1],
                fallback[2],
                new TrafficResourceKey
                {
                    ResourceType = TrafficResourceType.Node,
                    ResourceId = fallback[1]
                });
        }

        return new ScenarioLayout(
            "P1",
            "P2",
            "D1",
            new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = "E-X1-D1" });
    }

    private (string VehicleId, string? TaskId, ScenarioDefinition Scenario) GetSlot(MockScenarioVehicleSlot slot)
    {
        lock (_syncRoot)
        {
            return slot == MockScenarioVehicleSlot.VehicleA
                ? (VehicleAId, _taskAId, _scenario)
                : (VehicleBId, _taskBId, _scenario);
        }
    }

    private string? GetTaskIdForVehicle(string vehicleId)
    {
        lock (_syncRoot)
        {
            if (string.Equals(vehicleId, VehicleAId, StringComparison.OrdinalIgnoreCase))
            {
                return _taskAId;
            }

            if (string.Equals(vehicleId, VehicleBId, StringComparison.OrdinalIgnoreCase))
            {
                return _taskBId;
            }
        }

        var snapshot = _mockSimulationService.GetVehicle(vehicleId);
        return snapshot?.CurrentTaskId;
    }

    private string CurrentSourceFor(string vehicleId)
    {
        lock (_syncRoot)
        {
            if (_layout is null)
            {
                return string.Empty;
            }

            return string.Equals(vehicleId, VehicleAId, StringComparison.OrdinalIgnoreCase)
                ? _layout.SourceA
                : _layout.SourceB;
        }
    }

    private string CurrentTarget()
    {
        lock (_syncRoot)
        {
            return _layout?.Target ?? string.Empty;
        }
    }

    private void UpsertVehicle(
        string vehicleId,
        string? location,
        string? taskId,
        RobotState state,
        bool clearAlarm,
        MapPosition? position = null)
    {
        var update = new MockVehicleUpdate
        {
            VehicleId = vehicleId,
            State = state,
            IsOnline = true,
            Location = string.IsNullOrWhiteSpace(location) ? null : location,
            Position = position,
            CurrentTaskId = taskId ?? string.Empty,
            HasAlarm = clearAlarm ? false : null,
            ActiveAlarmCode = clearAlarm ? string.Empty : null,
            ActiveAlarmMessage = clearAlarm ? string.Empty : null,
            Telemetry = new Dictionary<string, string>
            {
                ["Scenario"] = _scenario.Key,
                ["Role"] = string.Equals(vehicleId, VehicleAId, StringComparison.OrdinalIgnoreCase) ? "A" : "B"
            }
        };
        _mockSimulationService.UpsertVehicle(update);
    }

    public void Dispose()
    {
        StopAllAutomaticVehicles();
    }

    private async Task RunAutomaticVehicleAsync(
        string vehicleId,
        CancellationTokenSource runCancellation)
    {
        try
        {
            while (!runCancellation.IsCancellationRequested)
            {
                var snapshot = _mockSimulationService.GetVehicle(vehicleId);
                if (snapshot is null || !snapshot.IsOnline || snapshot.State is RobotState.Fault or RobotState.Offline)
                {
                    _vehicleMotionService.StopVehicle(vehicleId, "Vehicle is unavailable.");
                    break;
                }

                var segment = await GetNextAutomaticSegmentAsync(vehicleId, runCancellation.Token)
                    .ConfigureAwait(false);
                if (segment.State == AutomaticSegmentState.Completed)
                {
                    break;
                }

                if (segment.State == AutomaticSegmentState.Waiting)
                {
                    await RetryWaitingTaskAsync(vehicleId, runCancellation.Token).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromMilliseconds(500), runCancellation.Token).ConfigureAwait(false);
                    continue;
                }

                if (segment.Target is null)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), runCancellation.Token).ConfigureAwait(false);
                    continue;
                }

                if (snapshot.Position?.HasValidCoordinates() != true && segment.Start is not null)
                {
                    _mockSimulationService.UpsertVehicle(new MockVehicleUpdate
                    {
                        VehicleId = vehicleId,
                        Position = segment.Start
                    });
                }

                var motion = await _vehicleMotionService.MoveToAsync(
                    vehicleId,
                    segment.Target,
                    cancellationToken: runCancellation.Token).ConfigureAwait(false);
                if (!motion.ReachedTarget)
                {
                    break;
                }

                var advanced = await ArriveNextNodeCoreAsync(vehicleId, runCancellation.Token).ConfigureAwait(false);
                if (!advanced.Success)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            AddLog($"自动运行 {vehicleId}", "Failed", ex.Message);
        }
        finally
        {
            lock (_syncRoot)
            {
                if (_automaticRuns.TryGetValue(vehicleId, out var current) && ReferenceEquals(current, runCancellation))
                {
                    _automaticRuns.Remove(vehicleId);
                }
            }

            runCancellation.Dispose();
            NotifyChanged();
        }
    }

    private async Task<AutomaticSegment> GetNextAutomaticSegmentAsync(
        string vehicleId,
        CancellationToken cancellationToken)
    {
        var taskId = GetTaskIdForVehicle(vehicleId);
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return AutomaticSegment.Completed;
        }

        var executionResult = await _dispatchOrchestrationService.GetExecutionAsync(
            new GetDispatchExecutionRequest
            {
                Context = Context("AutomaticMotion"),
                TaskId = taskId
            },
            cancellationToken).ConfigureAwait(false);
        if (!executionResult.Success || executionResult.Data is null)
        {
            return AutomaticSegment.Completed;
        }

        var execution = executionResult.Data;
        if (execution.State == DispatchExecutionState.WaitingForTraffic)
        {
            return AutomaticSegment.Waiting;
        }

        if (execution.State is DispatchExecutionState.Completed or DispatchExecutionState.Canceled or DispatchExecutionState.Failed)
        {
            return AutomaticSegment.Completed;
        }

        var reservationResult = await _routeReservationService.GetReservationAsync(
            new GetRouteReservationRequest
            {
                Context = Context("AutomaticMotion"),
                ReservationId = execution.ReservationId ?? string.Empty
            },
            cancellationToken).ConfigureAwait(false);
        if (!reservationResult.Success || reservationResult.Data is null)
        {
            return AutomaticSegment.Waiting;
        }

        var next = NextSegment(reservationResult.Data, execution.CurrentSegmentSequence);
        if (next is null)
        {
            var completed = await ArriveNextNodeCoreAsync(vehicleId, cancellationToken).ConfigureAwait(false);
            return completed.Success ? AutomaticSegment.Completed : AutomaticSegment.Waiting;
        }

        if (!next.IsLocked)
        {
            return AutomaticSegment.Waiting;
        }

        var start = ResolveNodePosition(next.Segment.FromNodeId);
        var target = ResolveNodePosition(next.Segment.ToNodeId);
        return target is null
            ? AutomaticSegment.Waiting
            : new AutomaticSegment(AutomaticSegmentState.Ready, start, target);
    }

    private MapPosition? ResolveNodePosition(string nodeId)
    {
        var map = _mapService.GetCurrentMap(new GetMapSnapshotRequest { Context = Context("ResolveNodePosition") });
        var node = map.Data?.Nodes.FirstOrDefault(item =>
            string.Equals(item.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (node is null || map.Data is null)
        {
            return null;
        }

        return new MapPosition
        {
            MapId = map.Data.MapId,
            NodeId = node.NodeId,
            X = node.X,
            Y = node.Y,
            Heading = node.Angle ?? 0,
            AreaCode = node.AreaId
        };
    }

    private MockScenarioOperationResult FailAndLog(string step, string? message)
    {
        var details = string.IsNullOrWhiteSpace(message) ? "Operation failed." : message;
        AddLog(step, "Failed", details);
        NotifyChanged();
        return MockScenarioOperationResult.Fail(details);
    }

    private void AddLog(string step, string result, string? details)
    {
        lock (_syncRoot)
        {
            _logs.Insert(0, new MockScenarioStepLog
            {
                Time = DateTimeOffset.Now,
                Step = step,
                Result = result,
                Details = details ?? string.Empty
            });

            if (_logs.Count > 200)
            {
                _logs.RemoveRange(200, _logs.Count - 200);
            }
        }
    }

    private MockScenarioState CreateStateNoLock() => new()
    {
        ScenarioKey = _scenario.Key,
        ScenarioName = _scenario.Name,
        VehicleAId = VehicleAId,
        VehicleBId = VehicleBId,
        TaskAId = _taskAId,
        TaskBId = _taskBId,
        SourceA = _layout?.SourceA ?? string.Empty,
        SourceB = _layout?.SourceB ?? string.Empty,
        Target = _layout?.Target ?? string.Empty,
        ManualBlockResource = _layout?.ManualBlockResource,
        IsManualBlockActive = _manualBlockActive,
        IsVehicleAAutomaticRunning = _automaticRuns.ContainsKey(VehicleAId),
        IsVehicleBAutomaticRunning = _automaticRuns.ContainsKey(VehicleBId),
        Logs = _logs.ToArray()
    };

    private void NotifyChanged() => ScenarioChanged?.Invoke(this, EventArgs.Empty);

    private static TaskCreateRequest CreateTaskRequest(
        string sourceNodeId,
        string targetNodeId,
        string cargoCode) => new()
    {
        TaskType = "MockConflictDemo",
        TemplateId = "DEBUG-SCENARIO",
        SourceNodeId = sourceNodeId,
        TargetNodeId = targetNodeId,
        Priority = TaskPriority.High,
        CargoCode = cargoCode,
        CargoName = "Debug scenario payload",
        CargoWeight = 1,
        CreatedBy = "DebugDashboard",
        Attributes = new Dictionary<string, string>
        {
            ["Scenario"] = "MockConflictDemo"
        },
        MinBatteryRequired = 1
    };

    private static RouteReservedSegmentDto? NextSegment(RouteReservationDto reservation, int currentSequence) =>
        reservation.Segments
            .Where(segment => !segment.IsReleased && segment.Segment.Sequence > currentSequence)
            .OrderBy(segment => segment.Segment.Sequence)
            .FirstOrDefault();

    private static bool IsFinalSegment(RouteReservationDto reservation, int sequence) =>
        reservation.Segments.Count > 0 &&
        sequence >= reservation.Segments.Max(segment => segment.Segment.Sequence);

    private static int CalculateProgress(RouteReservationDto reservation, int sequence)
    {
        var max = Math.Max(1, reservation.Segments.Count);
        return Math.Clamp((int)Math.Round(sequence * 100.0 / max), 0, 99);
    }

    private static RequestContext Context(string operation) => new()
    {
        SourceModule = nameof(AgvDispatcher.DebugDashboard),
        OperatorId = "DebugDashboard",
        CorrelationId = operation
    };

    private static bool HasNodes(HashSet<string> nodeIds, params string[] ids) =>
        ids.All(nodeIds.Contains);

    private sealed record ScenarioDefinition(
        string Key,
        string Name,
        string Description,
        int RollingWindowSize);

    private sealed record ScenarioLayout(
        string SourceA,
        string SourceB,
        string Target,
        TrafficResourceKey ManualBlockResource);

    private enum AutomaticSegmentState
    {
        Ready,
        Waiting,
        Completed
    }

    private sealed record AutomaticSegment(
        AutomaticSegmentState State,
        MapPosition? Start,
        MapPosition? Target)
    {
        public static AutomaticSegment Waiting { get; } = new(AutomaticSegmentState.Waiting, null, null);
        public static AutomaticSegment Completed { get; } = new(AutomaticSegmentState.Completed, null, null);
    }
}
