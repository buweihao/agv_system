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
    private bool _vehicleAHoldingTarget;
    private int _fullFlowRunning;

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
            "A 车从 N001、B 车从 N002 汇入 N003，经 N003→N005 单车窄道依次通行。",
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
        var layout = ResolveLayout(scenario.Key);
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
            _vehicleAHoldingTarget = false;
        }

        var occupancyA = await ReportInitialOccupancyAsync(
            VehicleAId,
            layout.SourceA,
            cancellationToken).ConfigureAwait(false);
        var occupancyB = occupancyA.Success
            ? await ReportInitialOccupancyAsync(
                VehicleBId,
                layout.SourceB,
                cancellationToken).ConfigureAwait(false)
            : occupancyA;
        if (!occupancyA.Success || !occupancyB.Success)
        {
            var message = !occupancyA.Success ? occupancyA.Message : occupancyB.Message;
            await ResetScenarioAsync(cancellationToken).ConfigureAwait(false);
            return MockScenarioOperationResult.Fail($"场景初始化占用失败：{message}");
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

    private Task<AgvResult> ReportInitialOccupancyAsync(
        string vehicleId,
        string sourceNodeId,
        CancellationToken cancellationToken)
    {
        return _trafficControlService.UpdateAgvOccupancyAsync(
            new AgvOccupancyUpdateRequest
            {
                Context = Context("InitializeScenario"),
                AgvId = vehicleId,
                CurrentNodeId = sourceNodeId,
                OccupiedNodeIds = new[] { sourceNodeId },
                ReportTime = DateTimeOffset.Now
            },
            cancellationToken);
    }

    public async Task<MockScenarioOperationResult> RunSameTargetFlowAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _fullFlowRunning, 1, 0) != 0)
        {
            return MockScenarioOperationResult.Fail("完整流程正在演示中，请等待当前演示结束。");
        }

        NotifyChanged();
        try
        {
            var initialized = await InitializeScenarioAsync("same-target", cancellationToken).ConfigureAwait(false);
            if (!initialized.Success) return initialized;

            AddLog("一键演示", "Step 1", "场景已初始化，准备启动 A 车。");
            NotifyChanged();
            await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken).ConfigureAwait(false);

            var startedA = await StartVehicleAsync(MockScenarioVehicleSlot.VehicleA, cancellationToken).ConfigureAwait(false);
            if (!startedA.Success) return startedA;
            await Task.Delay(TimeSpan.FromMilliseconds(1400), cancellationToken).ConfigureAwait(false);

            var startedB = await StartVehicleAsync(MockScenarioVehicleSlot.VehicleB, cancellationToken).ConfigureAwait(false);
            if (!startedB.Success) return startedB;
            AddLog("一键演示", "Step 2", "B 车已启动并因共享终点被 A 车锁定而等待。");
            NotifyChanged();
            await Task.Delay(TimeSpan.FromMilliseconds(2000), cancellationToken).ConfigureAwait(false);

            for (var step = 0; step < 64 && !CurrentState.IsVehicleAHoldingTarget; step++)
            {
                var arrived = await MoveOneDemoSegmentAsync(VehicleAId, cancellationToken).ConfigureAwait(false);
                if (!arrived.Success) return arrived;
                await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken).ConfigureAwait(false);
            }

            if (!CurrentState.IsVehicleAHoldingTarget)
            {
                return FailAndLog("一键演示", "A 车未能到达并占用共享终点。");
            }

            AddLog("一键演示", "Step 3", "A 车正在占用共享终点；B 车继续等待。");
            NotifyChanged();
            await Task.Delay(TimeSpan.FromMilliseconds(3000), cancellationToken).ConfigureAwait(false);

            var departed = await DepartVehicleAAsync(cancellationToken).ConfigureAwait(false);
            if (!departed.Success) return departed;
            await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken).ConfigureAwait(false);

            var retriedB = await RetryWaitingTaskAsync(VehicleBId, cancellationToken).ConfigureAwait(false);
            if (!retriedB.Success) return retriedB;
            AddLog("一键演示", "Step 4", "A 车已让出共享终点，B 车重试成功并继续运行。");
            NotifyChanged();
            await Task.Delay(TimeSpan.FromMilliseconds(1400), cancellationToken).ConfigureAwait(false);

            for (var step = 0; step < 64; step++)
            {
                var taskB = string.IsNullOrWhiteSpace(_taskBId) ? null : _taskService.GetTask(_taskBId);
                if (taskB?.State is TaskState.Completed or TaskState.Cancelled or TaskState.Failed)
                {
                    break;
                }

                var arrived = await MoveOneDemoSegmentAsync(VehicleBId, cancellationToken).ConfigureAwait(false);
                if (!arrived.Success) return arrived;
                await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken).ConfigureAwait(false);
            }

            var finalTaskB = string.IsNullOrWhiteSpace(_taskBId) ? null : _taskService.GetTask(_taskBId);
            if (finalTaskB?.State != TaskState.Completed)
            {
                return FailAndLog("一键演示", "B 车未能完成进入共享终点的流程。");
            }

            AddLog("一键演示", "Completed", "完整流程演示结束：A 车先占用并离开，B 车随后进入终点。");
            NotifyChanged();
            return MockScenarioOperationResult.Ok("两车同终点完整流程演示已完成。");
        }
        finally
        {
            Interlocked.Exchange(ref _fullFlowRunning, 0);
            NotifyChanged();
        }
    }

    public async Task<MockScenarioOperationResult> RunNarrowAisleFlowAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _fullFlowRunning, 1, 0) != 0)
        {
            return MockScenarioOperationResult.Fail("已有完整流程正在演示，请等待当前演示结束。");
        }

        NotifyChanged();
        try
        {
            var initialized = await InitializeScenarioAsync("narrow-aisle", cancellationToken).ConfigureAwait(false);
            if (!initialized.Success) return initialized;

            AddLog("窄道一键演示", "Step 1", "A 车位于 N001，B 车位于 N002，共同入口为 N003。");
            NotifyChanged();
            await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellationToken).ConfigureAwait(false);

            var startedA = await StartVehicleAsync(MockScenarioVehicleSlot.VehicleA, cancellationToken).ConfigureAwait(false);
            if (!startedA.Success) return startedA;
            await Task.Delay(TimeSpan.FromMilliseconds(1400), cancellationToken).ConfigureAwait(false);

            var startedB = await StartVehicleAsync(MockScenarioVehicleSlot.VehicleB, cancellationToken).ConfigureAwait(false);
            if (!startedB.Success) return startedB;
            AddLog("窄道一键演示", "Step 2", "A 车已锁定 N003→N005 窄道，B 车进入交通等待。");
            NotifyChanged();
            await Task.Delay(TimeSpan.FromMilliseconds(2500), cancellationToken).ConfigureAwait(false);

            for (var step = 0; step < 64 && !CurrentState.IsVehicleAHoldingTarget; step++)
            {
                var moved = await MoveOneDemoSegmentAsync(VehicleAId, cancellationToken).ConfigureAwait(false);
                if (!moved.Success) return moved;
                await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken).ConfigureAwait(false);
            }

            if (!CurrentState.IsVehicleAHoldingTarget)
            {
                return FailAndLog("窄道一键演示", "A 车未能通过窄道到达并占用 N005。");
            }

            AddLog("窄道一键演示", "Step 3", "A 车已到达并占用 N005，B 车继续在 N002 等待。");
            NotifyChanged();
            await Task.Delay(TimeSpan.FromMilliseconds(2200), cancellationToken).ConfigureAwait(false);

            var departedA = await DepartVehicleAAsync(cancellationToken).ConfigureAwait(false);
            if (!departedA.Success) return departedA;
            AddLog("窄道一键演示", "Step 4", "A 车已前往 N005 后续节点并让出出口，准备重试 B 车。");
            NotifyChanged();
            await Task.Delay(TimeSpan.FromMilliseconds(1400), cancellationToken).ConfigureAwait(false);

            var retriedB = await RetryWaitingTaskAsync(VehicleBId, cancellationToken).ConfigureAwait(false);
            if (!retriedB.Success) return retriedB;
            await Task.Delay(TimeSpan.FromMilliseconds(1400), cancellationToken).ConfigureAwait(false);

            for (var step = 0; step < 64; step++)
            {
                var taskB = string.IsNullOrWhiteSpace(_taskBId) ? null : _taskService.GetTask(_taskBId);
                if (taskB?.State is TaskState.Completed or TaskState.Cancelled or TaskState.Failed)
                {
                    break;
                }

                var moved = await MoveOneDemoSegmentAsync(VehicleBId, cancellationToken).ConfigureAwait(false);
                if (!moved.Success) return moved;
                await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken).ConfigureAwait(false);
            }

            var completedB = string.IsNullOrWhiteSpace(_taskBId) ? null : _taskService.GetTask(_taskBId);
            if (completedB?.State != TaskState.Completed)
            {
                return FailAndLog("窄道一键演示", "B 车未能通过窄道到达 N005。");
            }

            AddLog("窄道一键演示", "Completed", "演示完成：A 车先通过窄道、占用出口并离开，B 车随后再进入。");
            NotifyChanged();
            return MockScenarioOperationResult.Ok("窄道会车冲突完整流程演示已完成。");
        }
        finally
        {
            Interlocked.Exchange(ref _fullFlowRunning, 0);
            NotifyChanged();
        }
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

    public async Task<MockScenarioOperationResult> MoveVehicleToNextNodeAsync(
        string vehicleId,
        CancellationToken cancellationToken = default)
    {
        if (IsAutomaticRunning(vehicleId))
        {
            return FailAndLog($"前进 {vehicleId}", "自动运行中，不能同时执行手动前进。");
        }

        return await MoveOneDemoSegmentAsync(vehicleId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MockScenarioOperationResult> ArriveNextNodeCoreAsync(
        string vehicleId,
        CancellationToken cancellationToken)
    {
        if (IsVehicleAHoldingTarget(vehicleId))
        {
            return MockScenarioOperationResult.Ok($"{vehicleId} is holding the shared target; use 'A 车离开' to continue.");
        }

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
        UpsertVehicle(
            vehicleId,
            nextNode,
            taskId,
            isFinalSegment ? RobotState.Idle : RobotState.Running,
            clearAlarm: false,
            ResolveNodePosition(nextNode));
        AddLog(
            $"到达 {vehicleId}",
            advance.Data.Execution.State.ToString(),
            $"{vehicleId} 到达 {nextNode}，已释放至路线段 {next.Segment.Sequence}。");

        if (isFinalSegment)
        {
            if (ShouldVehicleAHoldTarget(vehicleId))
            {
                lock (_syncRoot)
                {
                    _vehicleAHoldingTarget = true;
                }

                AddLog(
                    $"A 车占用终点",
                    "Holding",
                    $"{vehicleId} 已到达并持续占用 {nextNode}；B 车必须等待 A 车离开。");
                NotifyChanged();
                return MockScenarioOperationResult.Ok($"{vehicleId} 已到达并占用共享终点 {nextNode}。");
            }

            return await CompleteVehicleTaskAsync(vehicleId, taskId, nextNode, cancellationToken)
                .ConfigureAwait(false);
        }

        NotifyChanged();
        return MockScenarioOperationResult.Ok($"{vehicleId} 已推进到 {nextNode}。");
    }

    public async Task<MockScenarioOperationResult> DepartVehicleAAsync(
        CancellationToken cancellationToken = default)
    {
        string? taskId;
        string target;
        string exitNode;
        lock (_syncRoot)
        {
            if (!string.Equals(_scenario.Key, "same-target", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_scenario.Key, "narrow-aisle", StringComparison.OrdinalIgnoreCase))
            {
                return MockScenarioOperationResult.Fail("A 车离开仅用于两车同终点或窄道会车场景。");
            }

            if (!_vehicleAHoldingTarget)
            {
                return MockScenarioOperationResult.Fail("A 车尚未到达并占用共享终点。");
            }

            taskId = _taskAId;
            target = _layout?.Target ?? string.Empty;
            exitNode = _layout?.VehicleAExitNode ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(exitNode))
        {
            return FailAndLog("A 车离开", "没有可用的后续节点，无法让 A 车离开终点。");
        }

        UpsertVehicle(
            VehicleAId,
            target,
            taskId,
            RobotState.Running,
            clearAlarm: false,
            ResolveNodePosition(target));

        var exitPosition = ResolveNodePosition(exitNode);
        if (exitPosition?.HasValidCoordinates() == true)
        {
            var departureSpeed = CalculateDemoSpeed(
                ResolveNodePosition(target),
                exitPosition,
                TimeSpan.FromSeconds(3));
            AddLog("A 车离开", "Moving", $"{VehicleAId} 正在从 {target} 平滑移动到 {exitNode}。");
            NotifyChanged();
            var motion = await _vehicleMotionService.MoveToAsync(
                VehicleAId,
                exitPosition,
                departureSpeed,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!motion.ReachedTarget)
            {
                UpsertVehicle(VehicleAId, target, taskId, RobotState.Idle, clearAlarm: false, ResolveNodePosition(target));
                return FailAndLog("A 车离开", motion.Message);
            }
        }

        var completed = await _dispatchOrchestrationService.CompleteTaskAsync(
            new CompleteDispatchTaskRequest
            {
                Context = Context("DepartVehicleA"),
                TaskId = taskId,
                VehicleId = VehicleAId,
                CurrentNodeId = exitNode,
                // 最后一段已在到达终点时结束；这里保留 A 车对后续节点的实际占用。
                ReleaseReservation = false
            },
            cancellationToken).ConfigureAwait(false);
        if (!completed.Success)
        {
            return FailAndLog("A 车离开", completed.Message);
        }

        lock (_syncRoot)
        {
            _vehicleAHoldingTarget = false;
        }

        UpsertVehicle(VehicleAId, exitNode, null, RobotState.Idle, clearAlarm: false, exitPosition);
        AddLog(
            "A 车离开",
            "OK",
            $"{VehicleAId} 已从 {target} 移动到 {exitNode}，共享终点已让出，B 车现在可以重试进入。");
        NotifyChanged();
        return MockScenarioOperationResult.Ok($"A 车已离开 {target} 并到达 {exitNode}；请重试 B 车。");
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
            _vehicleAHoldingTarget = false;
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

    private ScenarioLayout ResolveLayout(string scenarioKey)
    {
        var mapResult = _mapService.GetCurrentMap(new GetMapSnapshotRequest { Context = Context("ResolveLayout") });
        var nodeIds = mapResult.Success && mapResult.Data is not null
            ? mapResult.Data.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edgeIds = mapResult.Success && mapResult.Data is not null
            ? mapResult.Data.Edges.Select(edge => edge.EdgeId).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.Equals(scenarioKey, "narrow-aisle", StringComparison.OrdinalIgnoreCase) &&
            mapResult.Data is not null &&
            HasNodes(nodeIds, "N001", "N002", "N003", "N005"))
        {
            var routeAEntry = FindEnabledEdge(mapResult.Data, "N001", "N003");
            var routeBEntry = FindEnabledEdge(mapResult.Data, "N002", "N003");
            var narrowEdge = FindEnabledEdge(mapResult.Data, "N003", "N005");
            if (routeAEntry is not null && routeBEntry is not null && narrowEdge is not null)
            {
                return new ScenarioLayout(
                    "N001",
                    "N002",
                    "N005",
                    new TrafficResourceKey
                    {
                        ResourceType = TrafficResourceType.Edge,
                        ResourceId = narrowEdge.EdgeId
                    },
                    ResolveVehicleAExitNode(mapResult.Data, "N005", "N001", "N002", "N003"),
                    "N003");
            }
        }

        if (HasNodes(nodeIds, "PICK-A1", "PICK-A2", "CHG-02") &&
            edgeIds.Contains("E-PICK-A2-PUT-A1"))
        {
            return new ScenarioLayout(
                "PICK-A1",
                "PICK-A2",
                "CHG-02",
                new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = "E-PICK-A2-PUT-A1" },
                ResolveVehicleAExitNode(mapResult.Data, "CHG-02", "PICK-A1", "PICK-A2"));
        }

        if (HasNodes(nodeIds, "P1", "P2", "D1"))
        {
            var blockEdge = edgeIds.Contains("E-P2-X1") ? "E-P2-X1" : "E-X1-D1";
            return new ScenarioLayout(
                "P1",
                "P2",
                "D1",
                new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = blockEdge },
                ResolveVehicleAExitNode(mapResult.Data, "D1", "P1", "P2"));
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
                },
                ResolveVehicleAExitNode(mapResult.Data, fallback[2], fallback[0], fallback[1]));
        }

        return new ScenarioLayout(
            "P1",
            "P2",
            "D1",
            new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = "E-X1-D1" },
            "E1");
    }

    private static MapEdgeDto? FindEnabledEdge(
        MapSnapshotDto map,
        string fromNodeId,
        string toNodeId)
    {
        return map.Edges.FirstOrDefault(edge =>
            edge.Enabled &&
            string.Equals(edge.FromNodeId, fromNodeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(edge.ToNodeId, toNodeId, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveVehicleAExitNode(
        MapSnapshotDto? map,
        string target,
        params string[] excludedNodes)
    {
        if (map is null)
        {
            return string.Empty;
        }

        var excluded = excludedNodes
            .Append(target)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outgoing = map.Edges.FirstOrDefault(edge =>
            edge.Enabled &&
            string.Equals(edge.FromNodeId, target, StringComparison.OrdinalIgnoreCase) &&
            !excluded.Contains(edge.ToNodeId));
        if (outgoing is not null)
        {
            return outgoing.ToNodeId;
        }

        var reverse = map.Edges.FirstOrDefault(edge =>
            edge.Enabled &&
            edge.Direction == MapEdgeDirection.Bidirectional &&
            string.Equals(edge.ToNodeId, target, StringComparison.OrdinalIgnoreCase) &&
            !excluded.Contains(edge.FromNodeId));
        if (reverse is not null)
        {
            return reverse.FromNodeId;
        }

        return string.Empty;
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

    private bool ShouldVehicleAHoldTarget(string vehicleId)
    {
        lock (_syncRoot)
        {
            return (string.Equals(_scenario.Key, "same-target", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_scenario.Key, "narrow-aisle", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(vehicleId, VehicleAId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool IsVehicleAHoldingTarget(string vehicleId)
    {
        lock (_syncRoot)
        {
            return _vehicleAHoldingTarget &&
                string.Equals(vehicleId, VehicleAId, StringComparison.OrdinalIgnoreCase);
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

    private async Task<MockScenarioOperationResult> MoveOneDemoSegmentAsync(
        string vehicleId,
        CancellationToken cancellationToken)
    {
        var segment = await GetNextAutomaticSegmentAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        if (segment.State == AutomaticSegmentState.Completed)
        {
            return MockScenarioOperationResult.Ok($"{vehicleId} 已完成路线。");
        }

        if (segment.State != AutomaticSegmentState.Ready || segment.Target is null)
        {
            return FailAndLog("平滑移动", $"{vehicleId} 当前没有已锁定的下一段路线。");
        }

        var snapshot = _mockSimulationService.GetVehicle(vehicleId);
        var start = snapshot?.Position?.HasValidCoordinates() == true
            ? snapshot.Position
            : segment.Start;
        if (snapshot?.Position?.HasValidCoordinates() != true && segment.Start is not null)
        {
            _mockSimulationService.UpsertVehicle(new MockVehicleUpdate
            {
                VehicleId = vehicleId,
                Position = segment.Start
            });
        }

        var speed = CalculateDemoSpeed(start, segment.Target, TimeSpan.FromSeconds(3));
        AddLog(
            "平滑移动",
            "Moving",
            $"{vehicleId} 正在前往 {segment.Target.NodeId}，本段预计约 3 秒。");
        NotifyChanged();

        var motion = await _vehicleMotionService.MoveToAsync(
            vehicleId,
            segment.Target,
            speed,
            cancellationToken).ConfigureAwait(false);
        if (!motion.ReachedTarget)
        {
            return FailAndLog("平滑移动", motion.Message);
        }

        return await ArriveNextNodeCoreAsync(vehicleId, cancellationToken).ConfigureAwait(false);
    }

    private static double CalculateDemoSpeed(
        MapPosition? start,
        MapPosition target,
        TimeSpan desiredDuration)
    {
        if (start?.HasValidCoordinates() != true || !target.HasValidCoordinates())
        {
            return 30;
        }

        var distance = Math.Sqrt(
            Math.Pow(target.X - start.X, 2) +
            Math.Pow(target.Y - start.Y, 2));
        return Math.Max(0.1, distance / Math.Max(0.1, desiredDuration.TotalSeconds));
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
        MergeNode = _layout?.MergeNode ?? string.Empty,
        VehicleAExitNode = _layout?.VehicleAExitNode ?? string.Empty,
        IsVehicleAHoldingTarget = _vehicleAHoldingTarget,
        IsFullFlowRunning = Volatile.Read(ref _fullFlowRunning) != 0,
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
        TrafficResourceKey ManualBlockResource,
        string VehicleAExitNode,
        string MergeNode = "");

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
