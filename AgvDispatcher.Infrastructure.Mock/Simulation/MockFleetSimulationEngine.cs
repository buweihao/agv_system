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

namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public sealed class MockFleetSimulationEngine : IMockFleetSimulationEngine
    {
        private readonly IDispatchOrchestrationService? _dispatchOrchestrationService;
        private readonly ITaskService? _taskService;
        private readonly IVehicleStateStore? _vehicleStateStore;
        private readonly IRouteReservationService? _routeReservationService;
        private readonly ITrafficControlService? _trafficControlService;
        private readonly IMapService? _mapService;
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, MockVehicleRuntimeState> _vehicles =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _createdTaskIds =
            new(StringComparer.OrdinalIgnoreCase);

        private MockSimulationScenario? _currentScenario;
        private long _tick;

        public MockFleetSimulationEngine()
        {
        }

        public MockFleetSimulationEngine(
            IDispatchOrchestrationService dispatchOrchestrationService,
            ITaskService taskService,
            IVehicleStateStore vehicleStateStore)
        {
            _dispatchOrchestrationService = dispatchOrchestrationService ??
                throw new ArgumentNullException(nameof(dispatchOrchestrationService));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _vehicleStateStore = vehicleStateStore ?? throw new ArgumentNullException(nameof(vehicleStateStore));
        }

        public MockFleetSimulationEngine(
            IDispatchOrchestrationService dispatchOrchestrationService,
            ITaskService taskService,
            IVehicleStateStore vehicleStateStore,
            IRouteReservationService routeReservationService,
            ITrafficControlService trafficControlService)
            : this(dispatchOrchestrationService, taskService, vehicleStateStore)
        {
            _routeReservationService = routeReservationService ??
                throw new ArgumentNullException(nameof(routeReservationService));
            _trafficControlService = trafficControlService ??
                throw new ArgumentNullException(nameof(trafficControlService));
        }

        public MockFleetSimulationEngine(
            IDispatchOrchestrationService dispatchOrchestrationService,
            ITaskService taskService,
            IVehicleStateStore vehicleStateStore,
            IRouteReservationService routeReservationService,
            ITrafficControlService trafficControlService,
            IMapService mapService)
            : this(dispatchOrchestrationService, taskService, vehicleStateStore, routeReservationService, trafficControlService)
        {
            _mapService = mapService ?? throw new ArgumentNullException(nameof(mapService));
        }

        public MockSimulationScenario? CurrentScenario
        {
            get
            {
                lock (_syncRoot)
                {
                    return _currentScenario?.Clone();
                }
            }
        }

        public IReadOnlyList<MockVehicleRuntimeState> GetVehicleStates()
        {
            lock (_syncRoot)
            {
                return _vehicles.Values
                    .OrderBy(vehicle => vehicle.VehicleId, StringComparer.OrdinalIgnoreCase)
                    .Select(vehicle => vehicle.Clone())
                    .ToArray();
            }
        }

        public MockVehicleRuntimeState? GetVehicleState(string vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return null;
            }

            lock (_syncRoot)
            {
                return _vehicles.TryGetValue(vehicleId, out var vehicle)
                    ? vehicle.Clone()
                    : null;
            }
        }

        public Task<MockSimulationTickResult> InitializeAsync(
            MockSimulationScenario scenario,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(scenario);
            scenario.Validate();

            lock (_syncRoot)
            {
                _currentScenario = scenario.Clone();
                _tick = 0;
                _vehicles.Clear();
                _createdTaskIds.Clear();

                foreach (var task in scenario.Tasks)
                {
                    var createdTask = _taskService?.CreateTask(new TaskCreateRequest
                    {
                        TaskType = string.IsNullOrWhiteSpace(task.TaskType) ? "MockSimulation" : task.TaskType,
                        TemplateId = "MOCK-SIMULATION",
                        SourceNodeId = task.SourceNodeId,
                        TargetNodeId = task.TargetNodeId,
                        Priority = task.Priority,
                        CargoCode = task.TaskId,
                        CargoName = task.TaskId,
                        CargoWeight = 1,
                        CreatedBy = nameof(MockFleetSimulationEngine),
                        Attributes = new Dictionary<string, string>
                        {
                            ["MockSimulationScenarioId"] = scenario.ScenarioId,
                            ["MockSimulationTaskId"] = task.TaskId
                        }
                    });
                    _createdTaskIds[task.TaskId] = createdTask?.TaskId ?? task.TaskId;
                }

                var tasksByVehicle = scenario.Tasks
                    .Where(task => !string.IsNullOrWhiteSpace(task.AssignedVehicleId))
                    .GroupBy(task => task.AssignedVehicleId!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var vehicle in scenario.Vehicles)
                {
                    tasksByVehicle.TryGetValue(vehicle.VehicleId, out var task);
                    _vehicles[vehicle.VehicleId] = new MockVehicleRuntimeState
                    {
                        VehicleId = vehicle.VehicleId,
                        Brand = vehicle.Brand,
                        State = MockVehicleSimulationState.Idle,
                        CurrentNodeId = vehicle.StartNodeId,
                        CurrentTaskId = task is null ? null : ResolveCreatedTaskIdNoLock(task.TaskId),
                        TargetNodeId = task?.TargetNodeId,
                        BatteryLevel = vehicle.BatteryLevel,
                        IsOnline = vehicle.IsOnline,
                        UpdatedAt = DateTimeOffset.Now
                    };

                    _vehicleStateStore?.UpsertStatus(new VehicleStatusSnapshot
                    {
                        VehicleId = vehicle.VehicleId,
                        Brand = vehicle.Brand,
                        State = vehicle.IsOnline ? RobotState.Idle : RobotState.Offline,
                        BatteryLevel = vehicle.BatteryLevel,
                        Location = vehicle.StartNodeId,
                        CurrentTaskId = null,
                        IsOnline = vehicle.IsOnline,
                        ReportedAt = DateTime.Now,
                        Position = new MapPosition { NodeId = vehicle.StartNodeId },
                        Telemetry = new Dictionary<string, string>
                        {
                            ["MockSimulationScenarioId"] = scenario.ScenarioId
                        }
                    });
                }

                return Task.FromResult(new MockSimulationTickResult
                {
                    Tick = _tick,
                    Succeeded = true,
                    Events = _vehicles.Values
                        .OrderBy(vehicle => vehicle.VehicleId, StringComparer.OrdinalIgnoreCase)
                        .Select(vehicle => MockSimulationEvent.VehicleInitialized(
                            _tick,
                            vehicle.VehicleId,
                            vehicle.CurrentTaskId,
                            vehicle.CurrentNodeId))
                        .ToArray(),
                    VehicleStates = GetVehicleStatesNoLock()
                });
            }
        }

        public async Task<MockSimulationTickResult> StartTaskAsync(
            string vehicleId,
            string taskId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_dispatchOrchestrationService is null || _taskService is null || _vehicleStateStore is null)
            {
                return Failure("Dispatch dependencies were not supplied to the simulation engine.");
            }

            MockSimulationScenario scenario;
            MockVehicleRuntimeState vehicle;
            string dispatchTaskId;
            lock (_syncRoot)
            {
                if (_currentScenario is null)
                {
                    return Failure("Simulation scenario has not been initialized.");
                }

                if (!_vehicles.TryGetValue(vehicleId, out var currentVehicle))
                {
                    return Failure($"Vehicle '{vehicleId}' was not found in the current simulation.");
                }

                var scenarioTask = _currentScenario.Tasks.FirstOrDefault(task =>
                    string.Equals(task.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
                if (scenarioTask is null)
                {
                    return Failure($"Task '{taskId}' was not found in the current simulation.");
                }

                scenario = _currentScenario.Clone();
                vehicle = currentVehicle.Clone();
                dispatchTaskId = ResolveCreatedTaskIdNoLock(taskId);
            }

            var start = await _dispatchOrchestrationService.StartTaskAsync(
                new StartDispatchTaskRequest
                {
                    Context = Context("StartTask"),
                    TaskId = dispatchTaskId,
                    PreferredVehicleId = vehicleId,
                    RollingWindowSize = scenario.Options.RollingWindowSize,
                    MaxReplanCount = scenario.Options.MaxReplanCount,
                    SendVehicleCommand = true
                },
                cancellationToken).ConfigureAwait(false);

            if (!start.Success || start.Data is null)
            {
                return Failure(start.Message, vehicleId, dispatchTaskId);
            }

            var nextState = start.Data.FirstWindowLocked
                ? MockVehicleSimulationState.Running
                : start.Data.Execution.State == DispatchExecutionState.WaitingForTraffic
                    ? MockVehicleSimulationState.WaitingForTraffic
                    : MockVehicleSimulationState.Dispatching;
            var waitingSince = nextState == MockVehicleSimulationState.WaitingForTraffic
                ? DateTimeOffset.Now
                : (DateTimeOffset?)null;
            var updated = CopyVehicle(
                vehicle,
                state: nextState,
                currentTaskId: dispatchTaskId,
                targetNodeId: _taskService.GetTask(dispatchTaskId)?.TargetNodeId,
                reservationId: start.Data.ReservationId,
                planId: start.Data.PlanId,
                waitingSince: waitingSince);

            lock (_syncRoot)
            {
                _vehicles[vehicleId] = updated;
            }

            _vehicleStateStore.UpsertStatus(new VehicleStatusSnapshot
            {
                VehicleId = updated.VehicleId,
                Brand = updated.Brand,
                State = nextState == MockVehicleSimulationState.Running ? RobotState.Running : RobotState.Idle,
                BatteryLevel = updated.BatteryLevel,
                Location = updated.CurrentNodeId,
                CurrentTaskId = nextState == MockVehicleSimulationState.Running ? dispatchTaskId : null,
                IsOnline = updated.IsOnline,
                ReportedAt = DateTime.Now,
                Position = new MapPosition { NodeId = updated.CurrentNodeId },
                Telemetry = new Dictionary<string, string>
                {
                    ["MockSimulationState"] = nextState.ToString()
                }
            });

            return new MockSimulationTickResult
            {
                Tick = CurrentTick,
                Succeeded = true,
                Message = start.Data.Message ?? start.Message,
                Events = new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = CurrentTick,
                        EventType = start.Data.FirstWindowLocked
                            ? MockSimulationEventType.TaskDispatchStarted
                            : MockSimulationEventType.WaitingForTraffic,
                        VehicleId = vehicleId,
                        TaskId = dispatchTaskId,
                        ResourceId = start.Data.ReservationId,
                        Message = start.Data.Message ?? start.Message
                    }
                },
                VehicleStates = GetVehicleStates()
            };
        }

        public async Task<MockSimulationTickResult> StartAllAsync(CancellationToken cancellationToken = default)
        {
            MockSimulationTask[] tasks;
            lock (_syncRoot)
            {
                tasks = _currentScenario?.Tasks
                    .Where(task => !string.IsNullOrWhiteSpace(task.AssignedVehicleId))
                    .ToArray() ?? Array.Empty<MockSimulationTask>();
            }

            var events = new List<MockSimulationEvent>();
            var succeeded = true;
            var messages = new List<string>();
            foreach (var task in tasks)
            {
                var result = await StartTaskAsync(task.AssignedVehicleId!, task.TaskId, cancellationToken)
                    .ConfigureAwait(false);
                succeeded &= result.Succeeded;
                events.AddRange(result.Events);
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    messages.Add(result.Message);
                }
            }

            return new MockSimulationTickResult
            {
                Tick = CurrentTick,
                Succeeded = succeeded,
                Message = string.Join("; ", messages),
                Events = events,
                VehicleStates = GetVehicleStates()
            };
        }

        public async Task<MockSimulationTickResult> CancelTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_dispatchOrchestrationService is null || _vehicleStateStore is null || _trafficControlService is null)
            {
                return Failure("Cancellation dependencies were not supplied to the simulation engine.", taskId: taskId);
            }

            var dispatchTaskId = ResolveCreatedTaskId(taskId);
            var vehicle = FindVehicleByTask(dispatchTaskId);
            var cancel = await _dispatchOrchestrationService.CancelTaskAsync(
                new CancelDispatchTaskRequest
                {
                    Context = Context("CancelTask"),
                    TaskId = dispatchTaskId,
                    Reason = "Mock simulation task cancellation",
                    ReleaseReservation = true,
                    SendCancelCommand = true
                },
                cancellationToken).ConfigureAwait(false);
            if (!cancel.Success)
            {
                return Failure(cancel.Message, vehicle?.VehicleId, dispatchTaskId);
            }

            if (vehicle is not null)
            {
                await ReleaseVehicleResourcesAsync(vehicle.VehicleId, dispatchTaskId, cancellationToken)
                    .ConfigureAwait(false);
                var updated = CopyVehicle(
                    vehicle,
                    state: MockVehicleSimulationState.Idle,
                    currentTaskId: string.Empty,
                    waitingSince: null,
                    lastRetryAt: null);
                lock (_syncRoot)
                {
                    _vehicles[vehicle.VehicleId] = updated;
                }

                UpsertVehicleSnapshot(updated, RobotState.Idle, null, hasAlarm: false);
            }

            return new MockSimulationTickResult
            {
                Tick = CurrentTick,
                Succeeded = true,
                Events = new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = CurrentTick,
                        EventType = MockSimulationEventType.TaskCanceled,
                        VehicleId = vehicle?.VehicleId,
                        TaskId = dispatchTaskId,
                        Message = "Mock simulation task was canceled."
                    }
                },
                VehicleStates = GetVehicleStates()
            };
        }

        public async Task<MockSimulationTickResult> InjectFaultAsync(
            string vehicleId,
            MockFaultPolicy faultPolicy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_vehicleStateStore is null)
            {
                return Failure("Vehicle state dependency was not supplied to the simulation engine.", vehicleId);
            }

            MockVehicleRuntimeState vehicle;
            lock (_syncRoot)
            {
                if (!_vehicles.TryGetValue(vehicleId, out var current))
                {
                    return Failure($"Vehicle '{vehicleId}' was not found in the current simulation.", vehicleId);
                }

                vehicle = current.Clone();
            }

            var taskId = vehicle.CurrentTaskId;
            if ((faultPolicy is MockFaultPolicy.ReleaseReservation or MockFaultPolicy.FailTaskAndRelease) &&
                !string.IsNullOrWhiteSpace(taskId))
            {
                await ReleaseExecutionReservationAsync(taskId!, cancellationToken).ConfigureAwait(false);
                await ReleaseVehicleResourcesAsync(vehicle.VehicleId, taskId!, cancellationToken).ConfigureAwait(false);
            }

            if (faultPolicy == MockFaultPolicy.FailTaskAndRelease &&
                _taskService is not null &&
                !string.IsNullOrWhiteSpace(taskId))
            {
                _taskService.UpdateTaskState(taskId!, TaskState.Failed, "Mock simulation vehicle fault.");
            }

            var updated = CopyVehicle(
                vehicle,
                state: MockVehicleSimulationState.Fault,
                currentTaskId: taskId,
                waitingSince: null,
                lastRetryAt: null);
            updated = CopyFault(updated, hasFault: true, faultCode: faultPolicy.ToString());
            lock (_syncRoot)
            {
                _vehicles[vehicle.VehicleId] = updated;
            }

            UpsertVehicleSnapshot(
                updated,
                RobotState.Fault,
                string.IsNullOrWhiteSpace(taskId) ? null : taskId,
                hasAlarm: true,
                alarmCode: faultPolicy.ToString(),
                alarmMessage: $"Mock simulation fault policy: {faultPolicy}");

            return new MockSimulationTickResult
            {
                Tick = CurrentTick,
                Succeeded = true,
                Events = new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = CurrentTick,
                        EventType = MockSimulationEventType.VehicleFaulted,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        Message = $"Vehicle fault injected with policy {faultPolicy}."
                    }
                },
                VehicleStates = GetVehicleStates()
            };
        }

        public Task<MockSimulationTickResult> RecoverVehicleAsync(
            string vehicleId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_vehicleStateStore is null)
            {
                return Task.FromResult(Failure("Vehicle state dependency was not supplied to the simulation engine.", vehicleId));
            }

            MockVehicleRuntimeState updated;
            lock (_syncRoot)
            {
                if (!_vehicles.TryGetValue(vehicleId, out var vehicle))
                {
                    return Task.FromResult(Failure($"Vehicle '{vehicleId}' was not found in the current simulation.", vehicleId));
                }

                updated = CopyFault(
                    CopyVehicle(
                        vehicle,
                        state: MockVehicleSimulationState.Idle,
                        currentTaskId: string.Empty,
                        waitingSince: null,
                        lastRetryAt: null),
                    hasFault: false,
                    faultCode: null);
                _vehicles[vehicleId] = updated;
            }

            UpsertVehicleSnapshot(updated, RobotState.Idle, null, hasAlarm: false);
            return Task.FromResult(new MockSimulationTickResult
            {
                Tick = CurrentTick,
                Succeeded = true,
                Events = new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = CurrentTick,
                        EventType = MockSimulationEventType.VehicleRecovered,
                        VehicleId = vehicleId,
                        Message = "Vehicle recovered from mock fault."
                    }
                },
                VehicleStates = GetVehicleStates()
            });
        }

        public Task<MockSimulationTickResult> StepAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MockVehicleRuntimeState[] activeVehicles;
            long tick;
            lock (_syncRoot)
            {
                _tick++;
                tick = _tick;
                activeVehicles = _vehicles.Values
                    .Where(vehicle => IsTickManagedState(vehicle.State) &&
                                      !string.IsNullOrWhiteSpace(vehicle.CurrentTaskId))
                    .OrderBy(vehicle => vehicle.VehicleId, StringComparer.OrdinalIgnoreCase)
                    .Select(vehicle => vehicle.Clone())
                    .ToArray();
            }

            if (activeVehicles.Length == 0)
            {
                return Task.FromResult(new MockSimulationTickResult
                {
                    Tick = tick,
                    Succeeded = true,
                    Events = new[]
                    {
                        new MockSimulationEvent
                        {
                            Tick = tick,
                            EventType = MockSimulationEventType.NoOp,
                            Message = "No running vehicles are available to step."
                        }
                    },
                    VehicleStates = GetVehicleStates()
                });
            }

            if (_dispatchOrchestrationService is null ||
                _taskService is null ||
                _vehicleStateStore is null ||
                _routeReservationService is null ||
                _trafficControlService is null)
            {
                return Task.FromResult(Failure("Route stepping dependencies were not supplied to the simulation engine."));
            }

            return StepActiveVehiclesAsync(activeVehicles, tick, cancellationToken);
        }

        private async Task<MockSimulationTickResult> StepActiveVehiclesAsync(
            IReadOnlyList<MockVehicleRuntimeState> activeVehicles,
            long tick,
            CancellationToken cancellationToken)
        {
            var events = new List<MockSimulationEvent>();
            var succeeded = true;

            foreach (var vehicle in activeVehicles)
            {
                if (vehicle.State == MockVehicleSimulationState.WaitingForTraffic)
                {
                    var retry = await RetryWaitingVehicleAsync(vehicle, tick, cancellationToken)
                        .ConfigureAwait(false);
                    succeeded &= retry.Succeeded;
                    events.AddRange(retry.Events);
                    continue;
                }

                var taskId = vehicle.CurrentTaskId!;
                var execution = await _dispatchOrchestrationService!.GetExecutionAsync(
                    new GetDispatchExecutionRequest
                    {
                        Context = Context("Step"),
                        TaskId = taskId
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!execution.Success || execution.Data is null ||
                    string.IsNullOrWhiteSpace(execution.Data.ReservationId))
                {
                    succeeded = false;
                    events.Add(FailureEvent(tick, vehicle.VehicleId, taskId, execution.Message));
                    continue;
                }

                var reservation = await _routeReservationService!.GetReservationAsync(
                    new GetRouteReservationRequest
                    {
                        Context = Context("Step"),
                        ReservationId = execution.Data.ReservationId
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!reservation.Success || reservation.Data is null)
                {
                    succeeded = false;
                    events.Add(FailureEvent(tick, vehicle.VehicleId, taskId, reservation.Message));
                    continue;
                }

                var routeInvalid = await ReplanIfCurrentRouteInvalidAsync(
                    vehicle,
                    taskId,
                    execution.Data.CurrentSegmentSequence,
                    reservation.Data,
                    tick,
                    cancellationToken).ConfigureAwait(false);
                if (routeInvalid is not null)
                {
                    succeeded &= routeInvalid.Succeeded;
                    events.AddRange(routeInvalid.Events);
                    continue;
                }

                var trafficConflict = await ReplanIfReservationTrafficBlockedAsync(
                    vehicle,
                    taskId,
                    execution.Data.CurrentSegmentSequence,
                    reservation.Data,
                    tick,
                    CurrentScenario?.Options ?? new MockSimulationOptions(),
                    cancellationToken).ConfigureAwait(false);
                if (trafficConflict is not null)
                {
                    succeeded &= trafficConflict.Succeeded;
                    events.AddRange(trafficConflict.Events);
                    continue;
                }

                var next = reservation.Data.Segments
                    .Where(segment => !segment.IsReleased &&
                                      segment.Segment.Sequence > execution.Data.CurrentSegmentSequence)
                    .OrderBy(segment => segment.Segment.Sequence)
                    .FirstOrDefault();
                if (next is null)
                {
                    events.Add(new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.NoOp,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        Message = $"Vehicle {vehicle.VehicleId} has no remaining route segment."
                    });
                    continue;
                }

                var segment = next.Segment;
                var isFinalSegment = reservation.Data.Segments.Count > 0 &&
                    segment.Sequence >= reservation.Data.Segments.Max(item => item.Segment.Sequence);
                var occupancy = await _trafficControlService!.UpdateAgvOccupancyAsync(
                    new AgvDispatcher.Core.Contracts.Traffic.Requests.AgvOccupancyUpdateRequest
                    {
                        Context = Context("Step"),
                        AgvId = vehicle.VehicleId,
                        TaskId = taskId,
                        CurrentNodeId = segment.ToNodeId,
                        CurrentEdgeId = segment.EdgeId,
                        ReportTime = DateTimeOffset.Now
                    },
                    cancellationToken).ConfigureAwait(false);
                if (!occupancy.Success)
                {
                    succeeded = false;
                    events.Add(FailureEvent(tick, vehicle.VehicleId, taskId, occupancy.Message));
                    continue;
                }

                var advance = await _dispatchOrchestrationService.AdvanceRouteAsync(
                    new AdvanceDispatchRouteRequest
                    {
                        Context = Context("Step"),
                        TaskId = taskId,
                        VehicleId = vehicle.VehicleId,
                        CurrentNodeId = segment.ToNodeId,
                        PassedSegmentSequence = segment.Sequence,
                        AcquireNextWindow = !isFinalSegment
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!advance.Success || advance.Data is null)
                {
                    succeeded = false;
                    events.Add(FailureEvent(tick, vehicle.VehicleId, taskId, advance.Message));
                    continue;
                }

                var progress = CalculateProgress(reservation.Data.Segments.Count, segment.Sequence);
                _taskService!.UpdateTaskProgress(taskId, progress, segment.ToNodeId);

                var nextState = isFinalSegment
                    ? MockVehicleSimulationState.Idle
                    : advance.Data.ShouldWait
                    ? MockVehicleSimulationState.WaitingForTraffic
                    : advance.Data.RequiresReplan
                        ? MockVehicleSimulationState.Replanning
                        : MockVehicleSimulationState.Running;

                if (isFinalSegment)
                {
                    var completion = await _dispatchOrchestrationService.CompleteTaskAsync(
                        new CompleteDispatchTaskRequest
                        {
                            Context = Context("StepComplete"),
                            TaskId = taskId,
                            VehicleId = vehicle.VehicleId,
                            CurrentNodeId = segment.ToNodeId,
                            ReleaseReservation = true
                        },
                        cancellationToken).ConfigureAwait(false);
                    if (!completion.Success)
                    {
                        succeeded = false;
                        events.Add(FailureEvent(tick, vehicle.VehicleId, taskId, completion.Message));
                        continue;
                    }

                    await _trafficControlService.ReleaseAsync(
                        new AgvDispatcher.Core.Contracts.Traffic.Requests.TrafficReleaseRequest
                        {
                            Context = Context("StepComplete"),
                            AgvId = vehicle.VehicleId,
                            TaskId = taskId,
                            Reason = "Mock simulation task completed"
                        },
                        cancellationToken).ConfigureAwait(false);
                    _taskService.UpdateTaskProgress(taskId, 100, segment.ToNodeId);
                }

                var updated = CopyVehicle(
                    vehicle,
                    state: nextState,
                    currentNodeId: segment.ToNodeId,
                    currentEdgeId: null,
                    currentTaskId: isFinalSegment ? string.Empty : taskId,
                    currentSegmentSequence: segment.Sequence,
                    waitingSince: advance.Data.ShouldWait ? DateTimeOffset.Now : null);

                lock (_syncRoot)
                {
                    _vehicles[vehicle.VehicleId] = updated;
                }

                _vehicleStateStore!.UpsertStatus(new VehicleStatusSnapshot
                {
                    VehicleId = updated.VehicleId,
                    Brand = updated.Brand,
                    State = nextState == MockVehicleSimulationState.Running
                        ? RobotState.Running
                        : RobotState.Idle,
                    BatteryLevel = updated.BatteryLevel,
                    Location = updated.CurrentNodeId,
                    CurrentTaskId = isFinalSegment ? null : taskId,
                    IsOnline = updated.IsOnline,
                    ReportedAt = DateTime.Now,
                    Position = new MapPosition { NodeId = updated.CurrentNodeId },
                    Telemetry = new Dictionary<string, string>
                    {
                        ["MockSimulationState"] = nextState.ToString(),
                        ["CurrentSegmentSequence"] = segment.Sequence.ToString()
                    }
                });

                events.Add(new MockSimulationEvent
                {
                    Tick = tick,
                    EventType = MockSimulationEventType.VehicleMoved,
                    VehicleId = vehicle.VehicleId,
                    TaskId = taskId,
                    FromNodeId = segment.FromNodeId,
                    ToNodeId = segment.ToNodeId,
                    ResourceId = segment.EdgeId,
                    Message = $"Vehicle {vehicle.VehicleId} moved from {segment.FromNodeId} to {segment.ToNodeId}."
                });

                if (advance.Data.ReleasedPassedResources)
                {
                    events.Add(new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.ResourceReleased,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = segment.EdgeId,
                        Message = $"Passed route resources were released through segment {segment.Sequence}."
                    });
                }

                if (advance.Data.AcquiredNextWindow)
                {
                    events.Add(new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.ResourceLocked,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        Message = "The next rolling route window was locked."
                    });
                }

                if (advance.Data.RequiresReplan)
                {
                    events.Add(new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.ReplanRequired,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = segment.EdgeId,
                        Message = advance.Data.Message ?? "Current route requires replan."
                    });
                }

                if (isFinalSegment)
                {
                    events.Add(new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.TaskCompleted,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ToNodeId = segment.ToNodeId,
                        Message = $"Task {taskId} completed at {segment.ToNodeId}."
                    });
                }
            }

            return new MockSimulationTickResult
            {
                Tick = tick,
                Succeeded = succeeded,
                Events = events,
                VehicleStates = GetVehicleStates()
            };
        }

        private async Task<MockSimulationTickResult> RetryWaitingVehicleAsync(
            MockVehicleRuntimeState vehicle,
            long tick,
            CancellationToken cancellationToken)
        {
            var taskId = vehicle.CurrentTaskId!;
            var options = CurrentScenario?.Options ?? new MockSimulationOptions();
            var now = DateTimeOffset.Now;
            var immediateReplan = await ReplanIfWaitingConflictPolicyRequiresAsync(
                vehicle,
                taskId,
                tick,
                options,
                now,
                cancellationToken).ConfigureAwait(false);
            if (immediateReplan is not null)
            {
                return immediateReplan;
            }

            var waitingSince = vehicle.WaitingSince ?? now;
            if (now - waitingSince < options.WaitTimeout)
            {
                return new MockSimulationTickResult
                {
                    Tick = tick,
                    Succeeded = true,
                    Events = new[]
                    {
                        new MockSimulationEvent
                        {
                            Tick = tick,
                            EventType = MockSimulationEventType.WaitingForTraffic,
                            VehicleId = vehicle.VehicleId,
                            TaskId = taskId,
                            ResourceId = vehicle.ReservationId,
                            Message = "Vehicle is waiting for traffic; timeout has not elapsed."
                        }
                    }
                };
            }

            if (vehicle.LastRetryAt.HasValue && now - vehicle.LastRetryAt.Value < options.RetryInterval)
            {
                return new MockSimulationTickResult
                {
                    Tick = tick,
                    Succeeded = true,
                    Events = new[]
                    {
                        new MockSimulationEvent
                        {
                            Tick = tick,
                            EventType = MockSimulationEventType.WaitingForTraffic,
                            VehicleId = vehicle.VehicleId,
                            TaskId = taskId,
                            ResourceId = vehicle.ReservationId,
                            Message = "Vehicle is waiting for the next retry interval."
                        }
                    }
                };
            }

            if (vehicle.WaitRetryCount >= options.MaxRetryCount)
            {
                return await HandleWaitingTimeoutLimitAsync(vehicle, taskId, tick, options, cancellationToken)
                    .ConfigureAwait(false);
            }

            var retry = await _dispatchOrchestrationService!.RetryWaitingTaskAsync(
                new RetryWaitingDispatchRequest
                {
                    Context = Context("StepRetryWaiting"),
                    TaskId = taskId,
                    SendVehicleCommand = true
                },
                cancellationToken).ConfigureAwait(false);

            if (!retry.Success || retry.Data is null)
            {
                return new MockSimulationTickResult
                {
                    Tick = tick,
                    Succeeded = false,
                    Events = new[] { FailureEvent(tick, vehicle.VehicleId, taskId, retry.Message) }
                };
            }

            if (retry.Data.FirstWindowLocked)
            {
                var updated = CopyVehicle(
                    vehicle,
                    state: MockVehicleSimulationState.Running,
                    currentTaskId: taskId,
                    reservationId: retry.Data.ReservationId,
                    planId: retry.Data.PlanId,
                    waitingSince: null,
                    lastRetryAt: now,
                    waitRetryCount: vehicle.WaitRetryCount + 1);
                lock (_syncRoot)
                {
                    _vehicles[vehicle.VehicleId] = updated;
                }

                _vehicleStateStore!.UpsertStatus(new VehicleStatusSnapshot
                {
                    VehicleId = updated.VehicleId,
                    Brand = updated.Brand,
                    State = RobotState.Running,
                    BatteryLevel = updated.BatteryLevel,
                    Location = updated.CurrentNodeId,
                    CurrentTaskId = taskId,
                    IsOnline = updated.IsOnline,
                    ReportedAt = DateTime.Now,
                    Position = new MapPosition { NodeId = updated.CurrentNodeId },
                    Telemetry = new Dictionary<string, string>
                    {
                        ["MockSimulationState"] = MockVehicleSimulationState.Running.ToString(),
                        ["WaitRetryCount"] = (vehicle.WaitRetryCount + 1).ToString()
                    }
                });

                return new MockSimulationTickResult
                {
                    Tick = tick,
                    Succeeded = true,
                    Events = new[]
                    {
                        new MockSimulationEvent
                        {
                            Tick = tick,
                            EventType = MockSimulationEventType.TaskDispatchStarted,
                            VehicleId = vehicle.VehicleId,
                            TaskId = taskId,
                            ResourceId = retry.Data.ReservationId,
                            Message = retry.Data.Message ?? "Waiting dispatch acquired the first rolling window."
                        },
                        new MockSimulationEvent
                        {
                            Tick = tick,
                            EventType = MockSimulationEventType.RetryAttempted,
                            VehicleId = vehicle.VehicleId,
                            TaskId = taskId,
                            ResourceId = retry.Data.ReservationId,
                            Message = "Waiting dispatch retry succeeded."
                        }
                    }
                };
            }

            if (retry.Data.RequiresReplan)
            {
                var retryEvents = new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.RetryAttempted,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = retry.Data.ReservationId,
                        Message = "Waiting dispatch retry attempted."
                    },
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.ReplanRequired,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = retry.Data.ReservationId,
                        Message = retry.Data.Message ?? "Waiting dispatch retry requires replanning."
                    }
                };
                return await ExecuteWaitingReplanAsync(
                    vehicle,
                    taskId,
                    tick,
                    "Waiting dispatch retry requires replanning.",
                    retryEvents,
                    now,
                    vehicle.WaitRetryCount + 1,
                    cancellationToken).ConfigureAwait(false);
            }

            var waiting = CopyVehicle(
                vehicle,
                state: MockVehicleSimulationState.WaitingForTraffic,
                currentTaskId: taskId,
                reservationId: retry.Data.ReservationId,
                planId: retry.Data.PlanId,
                waitingSince: vehicle.WaitingSince ?? DateTimeOffset.Now,
                lastRetryAt: now,
                waitRetryCount: vehicle.WaitRetryCount + 1);
            lock (_syncRoot)
            {
                _vehicles[vehicle.VehicleId] = waiting;
            }

            return new MockSimulationTickResult
            {
                Tick = tick,
                Succeeded = true,
                Events = new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.RetryAttempted,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = retry.Data.ReservationId,
                        Message = "Waiting dispatch retry attempted."
                    },
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = retry.Data.RequiresReplan
                            ? MockSimulationEventType.ReplanRequired
                            : MockSimulationEventType.WaitingForTraffic,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = retry.Data.ReservationId,
                        Message = retry.Data.Message ?? "Vehicle is still waiting for traffic."
                    }
                }
            };
        }

        private async Task<MockSimulationTickResult> ExecuteWaitingReplanAsync(
            MockVehicleRuntimeState vehicle,
            string taskId,
            long tick,
            string reason,
            IReadOnlyList<MockSimulationEvent> prefixEvents,
            DateTimeOffset now,
            int waitRetryCount,
            CancellationToken cancellationToken,
            IReadOnlyList<string>? forbiddenEdgeIds = null,
            IReadOnlyList<string>? forbiddenNodeIds = null)
        {
            var options = CurrentScenario?.Options ?? new MockSimulationOptions();
            if (vehicle.ReplanCount >= options.MaxReplanCount)
            {
                return HandleReplanLimitExceeded(vehicle, taskId, tick, options);
            }

            var replan = await _dispatchOrchestrationService!.ReplanTaskAsync(
                new ReplanDispatchTaskRequest
                {
                    Context = Context("StepReplanWaiting"),
                    TaskId = taskId,
                    VehicleId = vehicle.VehicleId,
                    CurrentNodeId = vehicle.CurrentNodeId,
                    CurrentSegmentSequence = vehicle.CurrentSegmentSequence,
                    Reason = reason,
                    ForbiddenEdgeIds = forbiddenEdgeIds ?? Array.Empty<string>(),
                    ForbiddenNodeIds = forbiddenNodeIds ?? Array.Empty<string>(),
                    AvoidOccupiedResources = true,
                    ReleaseOldReservation = true,
                    AcquireFirstWindow = true
                },
                cancellationToken).ConfigureAwait(false);

            if (!replan.Success || replan.Data is null)
            {
                return new MockSimulationTickResult
                {
                    Tick = tick,
                    Succeeded = false,
                    Events = prefixEvents
                        .Append(new MockSimulationEvent
                        {
                            Tick = tick,
                            EventType = MockSimulationEventType.ReplanFailed,
                            VehicleId = vehicle.VehicleId,
                            TaskId = taskId,
                            ResourceId = vehicle.ReservationId,
                            OldPlanId = vehicle.PlanId,
                            OldReservationId = vehicle.ReservationId,
                            Reason = reason,
                            Message = replan.Message ?? "Replan failed."
                        })
                        .ToArray()
                };
            }

            var data = replan.Data;
            var replanCount = vehicle.ReplanCount + 1;
            var nextState = data.RequiresReplan
                ? MockVehicleSimulationState.Replanning
                : data.ShouldWait
                    ? MockVehicleSimulationState.WaitingForTraffic
                    : MockVehicleSimulationState.Running;
            var updated = CopyVehicle(
                vehicle,
                state: nextState,
                currentTaskId: taskId,
                reservationId: data.NewReservationId ?? vehicle.ReservationId,
                planId: data.NewPlanId ?? vehicle.PlanId,
                waitingSince: nextState == MockVehicleSimulationState.WaitingForTraffic
                    ? vehicle.WaitingSince ?? now
                    : null,
                lastRetryAt: now,
                waitRetryCount: waitRetryCount,
                replanCount: replanCount);
            lock (_syncRoot)
            {
                _vehicles[vehicle.VehicleId] = updated;
            }

            UpsertVehicleSnapshot(
                updated,
                nextState == MockVehicleSimulationState.Running ? RobotState.Running : RobotState.Idle,
                taskId,
                hasAlarm: false);

            var events = new List<MockSimulationEvent>(prefixEvents);
            if (data.RequiresReplan)
            {
                events.Add(new MockSimulationEvent
                {
                    Tick = tick,
                    EventType = MockSimulationEventType.ReplanFailed,
                    VehicleId = vehicle.VehicleId,
                    TaskId = taskId,
                    ResourceId = data.OldReservationId,
                    OldPlanId = data.OldPlanId,
                    NewPlanId = data.NewPlanId,
                    OldReservationId = data.OldReservationId,
                    NewReservationId = data.NewReservationId,
                    Reason = reason,
                    Message = data.Message ?? "Replan could not produce a runnable route."
                });
            }
            else
            {
                events.Add(new MockSimulationEvent
                {
                    Tick = tick,
                    EventType = MockSimulationEventType.TaskReplanned,
                    VehicleId = vehicle.VehicleId,
                    TaskId = taskId,
                    ResourceId = data.NewReservationId,
                    OldPlanId = data.OldPlanId,
                    NewPlanId = data.NewPlanId,
                    OldReservationId = data.OldReservationId,
                    NewReservationId = data.NewReservationId,
                    Reason = reason,
                    Message = data.Message ?? "Waiting task was replanned."
                });
            }

            if (data.FirstWindowLocked)
            {
                events.Add(new MockSimulationEvent
                {
                    Tick = tick,
                    EventType = MockSimulationEventType.ResourceLocked,
                    VehicleId = vehicle.VehicleId,
                    TaskId = taskId,
                    ResourceId = data.NewReservationId,
                    OldReservationId = data.OldReservationId,
                    NewReservationId = data.NewReservationId,
                    Reason = reason,
                    Message = "Replanned first rolling window was locked."
                });
            }

            if (data.ShouldWait)
            {
                events.Add(new MockSimulationEvent
                {
                    Tick = tick,
                    EventType = MockSimulationEventType.WaitingForTraffic,
                    VehicleId = vehicle.VehicleId,
                    TaskId = taskId,
                    ResourceId = data.NewReservationId,
                    OldReservationId = data.OldReservationId,
                    NewReservationId = data.NewReservationId,
                    Reason = reason,
                    Message = data.Message ?? "Replanned route is waiting for traffic."
                });
            }

            return new MockSimulationTickResult
            {
                Tick = tick,
                Succeeded = true,
                Events = events,
                VehicleStates = GetVehicleStates()
            };
        }

        private async Task<MockSimulationTickResult?> ReplanIfCurrentRouteInvalidAsync(
            MockVehicleRuntimeState vehicle,
            string taskId,
            int currentSegmentSequence,
            AgvDispatcher.Core.Contracts.Reservations.Models.RouteReservationDto reservation,
            long tick,
            CancellationToken cancellationToken)
        {
            if (_mapService is null)
            {
                return null;
            }

            var mapResult = _mapService.GetCurrentMap(new GetMapSnapshotRequest { Context = Context("StepValidateMap") });
            if (!mapResult.Success || mapResult.Data is null)
            {
                return new MockSimulationTickResult
                {
                    Tick = tick,
                    Succeeded = false,
                    Events = new[] { FailureEvent(tick, vehicle.VehicleId, taskId, mapResult.Message) }
                };
            }

            var invalid = FindInvalidRemainingRouteResources(
                reservation,
                currentSegmentSequence,
                mapResult.Data);
            if (invalid.DisabledEdgeIds.Count == 0 && invalid.DisabledNodeIds.Count == 0)
            {
                return null;
            }

            var disabledResource = invalid.DisabledEdgeIds.FirstOrDefault() ??
                invalid.DisabledNodeIds.FirstOrDefault() ??
                reservation.ReservationId;
            var prefixEvents = new[]
            {
                new MockSimulationEvent
                {
                    Tick = tick,
                    EventType = MockSimulationEventType.RouteInvalidated,
                    VehicleId = vehicle.VehicleId,
                    TaskId = taskId,
                    ResourceId = disabledResource,
                    OldPlanId = vehicle.PlanId,
                    OldReservationId = reservation.ReservationId,
                    Reason = "MapResourceDisabled",
                    Message = "Current route contains a disabled map resource; running replan is required."
                }
            };

            return await ExecuteWaitingReplanAsync(
                vehicle,
                taskId,
                tick,
                "Current route contains disabled map resources.",
                prefixEvents,
                DateTimeOffset.Now,
                vehicle.WaitRetryCount,
                cancellationToken,
                invalid.DisabledEdgeIds,
                invalid.DisabledNodeIds).ConfigureAwait(false);
        }

        private async Task<MockSimulationTickResult?> ReplanIfReservationTrafficBlockedAsync(
            MockVehicleRuntimeState vehicle,
            string taskId,
            int currentSegmentSequence,
            RouteReservationDto reservation,
            long tick,
            MockSimulationOptions options,
            CancellationToken cancellationToken)
        {
            var conflict = await FindRemainingRouteTrafficConflictAsync(
                vehicle,
                taskId,
                currentSegmentSequence,
                reservation,
                cancellationToken).ConfigureAwait(false);
            if (conflict is null || !ShouldReplanForTrafficConflict(conflict.Value.State, options))
            {
                return null;
            }

            var prefixEvents = new[]
            {
                new MockSimulationEvent
                {
                    Tick = tick,
                    EventType = MockSimulationEventType.RouteInvalidated,
                    VehicleId = vehicle.VehicleId,
                    TaskId = taskId,
                    ResourceId = conflict.Value.ResourceId,
                    OldPlanId = vehicle.PlanId,
                    OldReservationId = reservation.ReservationId,
                    Reason = conflict.Value.State.ToString(),
                    Message = $"Route resource {conflict.Value.ResourceId} is {conflict.Value.State}; running replan is required."
                }
            };

            return await ExecuteWaitingReplanAsync(
                vehicle,
                taskId,
                tick,
                conflict.Value.State.ToString(),
                prefixEvents,
                DateTimeOffset.Now,
                vehicle.WaitRetryCount,
                cancellationToken,
                conflict.Value.ResourceType == TrafficResourceType.Edge ? new[] { conflict.Value.ResourceId } : null,
                conflict.Value.ResourceType == TrafficResourceType.Node ? new[] { conflict.Value.ResourceId } : null)
                .ConfigureAwait(false);
        }

        private async Task<MockSimulationTickResult?> ReplanIfWaitingConflictPolicyRequiresAsync(
            MockVehicleRuntimeState vehicle,
            string taskId,
            long tick,
            MockSimulationOptions options,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            if (_routeReservationService is null)
            {
                return null;
            }

            var reservationId = vehicle.ReservationId;
            if (string.IsNullOrWhiteSpace(reservationId))
            {
                return null;
            }

            var reservation = await _routeReservationService.GetReservationAsync(
                new GetRouteReservationRequest
                {
                    Context = Context("WaitingConflictPolicy"),
                    ReservationId = reservationId
                },
                cancellationToken).ConfigureAwait(false);
            if (!reservation.Success || reservation.Data is null)
            {
                return null;
            }

            var conflict = await FindRemainingRouteTrafficConflictAsync(
                vehicle,
                taskId,
                vehicle.CurrentSegmentSequence,
                reservation.Data,
                cancellationToken).ConfigureAwait(false);
            if (conflict is null || !ShouldReplanForTrafficConflict(conflict.Value.State, options))
            {
                return null;
            }

            var prefixEvents = new[]
            {
                new MockSimulationEvent
                {
                    Tick = tick,
                    EventType = MockSimulationEventType.ReplanRequired,
                    VehicleId = vehicle.VehicleId,
                    TaskId = taskId,
                    ResourceId = conflict.Value.ResourceId,
                    OldPlanId = vehicle.PlanId,
                    OldReservationId = reservation.Data.ReservationId,
                    Reason = conflict.Value.State.ToString(),
                    Message = $"Waiting route resource {conflict.Value.ResourceId} is {conflict.Value.State}; running replan is required."
                }
            };

            return await ExecuteWaitingReplanAsync(
                vehicle,
                taskId,
                tick,
                conflict.Value.State.ToString(),
                prefixEvents,
                now,
                vehicle.WaitRetryCount,
                cancellationToken,
                conflict.Value.ResourceType == TrafficResourceType.Edge ? new[] { conflict.Value.ResourceId } : null,
                conflict.Value.ResourceType == TrafficResourceType.Node ? new[] { conflict.Value.ResourceId } : null)
                .ConfigureAwait(false);
        }

        private async Task<RouteTrafficConflict?> FindRemainingRouteTrafficConflictAsync(
            MockVehicleRuntimeState vehicle,
            string taskId,
            int currentSegmentSequence,
            RouteReservationDto reservation,
            CancellationToken cancellationToken)
        {
            if (_trafficControlService is null)
            {
                return null;
            }

            var resources = reservation.Segments
                .Where(segment => !segment.IsReleased &&
                                  segment.Segment.Sequence > currentSegmentSequence)
                .OrderBy(segment => segment.Segment.Sequence)
                .SelectMany(segment => segment.Resources)
                .GroupBy(resource => (resource.ResourceType, resource.ResourceId))
                .Select(group => group.First())
                .ToArray();
            if (resources.Length == 0)
            {
                return null;
            }

            var statuses = await _trafficControlService.GetResourceStatusesAsync(
                resources,
                Context("RouteTrafficConflict"),
                cancellationToken).ConfigureAwait(false);
            if (!statuses.Success || statuses.Data is null)
            {
                return null;
            }

            return statuses.Data
                .Where(status => status.State != TrafficResourceState.Free &&
                                 !IsOwnedByVehicleTask(status, vehicle.VehicleId, taskId))
                .Select(status => new RouteTrafficConflict(
                    status.Resource.ResourceType,
                    status.Resource.ResourceId,
                    status.State))
                .FirstOrDefault();
        }

        private static bool ShouldReplanForTrafficConflict(
            TrafficResourceState state,
            MockSimulationOptions options)
        {
            return state switch
            {
                TrafficResourceState.Blocked or TrafficResourceState.Disabled =>
                    options.ReplanOnBlockedResource,
                TrafficResourceState.Locked or TrafficResourceState.Reserved or TrafficResourceState.Occupied =>
                    options.ReplanOnLockedResource && !options.PreferWaitingOverReplan,
                _ => false
            };
        }

        private static bool IsOwnedByVehicleTask(
            TrafficResourceStatusDto status,
            string vehicleId,
            string taskId)
        {
            return string.Equals(status.TaskId, taskId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status.OccupiedByAgvId, vehicleId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status.ReservedByAgvId, vehicleId, StringComparison.OrdinalIgnoreCase);
        }

        private MockSimulationTickResult HandleReplanLimitExceeded(
            MockVehicleRuntimeState vehicle,
            string taskId,
            long tick,
            MockSimulationOptions options)
        {
            var terminalState = options.OnTimeoutPolicy == MockSimulationTimeoutPolicy.FailTask
                ? MockVehicleSimulationState.Failed
                : MockVehicleSimulationState.TimedOut;
            if (terminalState == MockVehicleSimulationState.Failed)
            {
                _taskService?.UpdateTaskState(taskId, TaskState.Failed, "Mock simulation exceeded max replan count.");
            }

            var updated = CopyVehicle(
                vehicle,
                state: terminalState,
                currentTaskId: taskId,
                waitingSince: null,
                lastRetryAt: vehicle.LastRetryAt);
            lock (_syncRoot)
            {
                _vehicles[vehicle.VehicleId] = updated;
            }

            UpsertVehicleSnapshot(
                updated,
                terminalState == MockVehicleSimulationState.Failed ? RobotState.Fault : RobotState.Idle,
                taskId,
                hasAlarm: terminalState == MockVehicleSimulationState.Failed,
                alarmCode: terminalState == MockVehicleSimulationState.Failed ? "MOCK_REPLAN_LIMIT" : null,
                alarmMessage: terminalState == MockVehicleSimulationState.Failed
                    ? "Mock simulation exceeded max replan count."
                    : null);

            return new MockSimulationTickResult
            {
                Tick = tick,
                Succeeded = true,
                Events = new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.ReplanSkipped,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = vehicle.ReservationId,
                        OldPlanId = vehicle.PlanId,
                        OldReservationId = vehicle.ReservationId,
                        Reason = "MaxReplanCountExceeded",
                        Message = "Maximum replan count was exceeded."
                    }
                },
                VehicleStates = GetVehicleStates()
            };
        }

        private static (IReadOnlyList<string> DisabledEdgeIds, IReadOnlyList<string> DisabledNodeIds)
            FindInvalidRemainingRouteResources(
                AgvDispatcher.Core.Contracts.Reservations.Models.RouteReservationDto reservation,
                int currentSegmentSequence,
                MapSnapshotDto map)
        {
            var edges = (map.Edges ?? Array.Empty<MapEdgeDto>())
                .GroupBy(edge => edge.EdgeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var nodes = (map.Nodes ?? Array.Empty<MapNodeDto>())
                .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var disabledEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var disabledNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var reservedSegment in reservation.Segments
                .Where(segment => !segment.IsReleased &&
                                  segment.Segment.Sequence > currentSegmentSequence)
                .OrderBy(segment => segment.Segment.Sequence))
            {
                var segment = reservedSegment.Segment;
                if (!edges.TryGetValue(segment.EdgeId, out var edge) || !edge.Enabled)
                {
                    disabledEdges.Add(segment.EdgeId);
                }

                if (!nodes.TryGetValue(segment.FromNodeId, out var fromNode) || !fromNode.Enabled)
                {
                    disabledNodes.Add(segment.FromNodeId);
                }

                if (!nodes.TryGetValue(segment.ToNodeId, out var toNode) || !toNode.Enabled)
                {
                    disabledNodes.Add(segment.ToNodeId);
                }
            }

            return (disabledEdges.ToArray(), disabledNodes.ToArray());
        }

        private async Task<MockSimulationTickResult> HandleWaitingTimeoutLimitAsync(
            MockVehicleRuntimeState vehicle,
            string taskId,
            long tick,
            MockSimulationOptions options,
            CancellationToken cancellationToken)
        {
            if (options.OnTimeoutPolicy == MockSimulationTimeoutPolicy.RetryOnly)
            {
                var now = DateTimeOffset.Now;
                var timeoutEvents = new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.WaitingTimedOut,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = vehicle.ReservationId,
                        Message = "Waiting dispatch exceeded retry limit."
                    },
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.ReplanRequired,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = vehicle.ReservationId,
                        OldPlanId = vehicle.PlanId,
                        OldReservationId = vehicle.ReservationId,
                        Reason = "WaitingTimeout",
                        Message = "Waiting timeout reached; running replan is required."
                    }
                };
                return await ExecuteWaitingReplanAsync(
                    vehicle,
                    taskId,
                    tick,
                    "Waiting timeout reached.",
                    timeoutEvents,
                    now,
                    vehicle.WaitRetryCount,
                    cancellationToken).ConfigureAwait(false);
            }

            var terminalState = options.OnTimeoutPolicy == MockSimulationTimeoutPolicy.FailTask
                ? MockVehicleSimulationState.Failed
                : MockVehicleSimulationState.TimedOut;

            if (options.OnTimeoutPolicy == MockSimulationTimeoutPolicy.FailTask)
            {
                _taskService!.UpdateTaskState(taskId, TaskState.Failed, "Mock simulation waiting timeout.");
            }

            var updated = CopyVehicle(
                vehicle,
                state: terminalState,
                currentTaskId: taskId,
                waitingSince: vehicle.WaitingSince,
                lastRetryAt: vehicle.LastRetryAt,
                waitRetryCount: vehicle.WaitRetryCount);
            lock (_syncRoot)
            {
                _vehicles[vehicle.VehicleId] = updated;
            }

            _vehicleStateStore!.UpsertStatus(new VehicleStatusSnapshot
            {
                VehicleId = updated.VehicleId,
                Brand = updated.Brand,
                State = terminalState == MockVehicleSimulationState.Failed
                    ? RobotState.Fault
                    : RobotState.Idle,
                BatteryLevel = updated.BatteryLevel,
                Location = updated.CurrentNodeId,
                CurrentTaskId = taskId,
                IsOnline = updated.IsOnline,
                HasAlarm = terminalState == MockVehicleSimulationState.Failed,
                ActiveAlarmCode = terminalState == MockVehicleSimulationState.Failed ? "MOCK_WAIT_TIMEOUT" : null,
                ActiveAlarmMessage = terminalState == MockVehicleSimulationState.Failed
                    ? "Mock simulation waiting timeout."
                    : null,
                ReportedAt = DateTime.Now,
                Position = new MapPosition { NodeId = updated.CurrentNodeId },
                Telemetry = new Dictionary<string, string>
                {
                    ["MockSimulationState"] = terminalState.ToString(),
                    ["WaitRetryCount"] = vehicle.WaitRetryCount.ToString()
                }
            });

            return new MockSimulationTickResult
            {
                Tick = tick,
                Succeeded = true,
                Events = options.OnTimeoutPolicy == MockSimulationTimeoutPolicy.FailTask
                    ? new[]
                    {
                        new MockSimulationEvent
                        {
                            Tick = tick,
                            EventType = MockSimulationEventType.WaitingTimedOut,
                            VehicleId = vehicle.VehicleId,
                            TaskId = taskId,
                            ResourceId = vehicle.ReservationId,
                            Message = "Waiting dispatch exceeded retry limit; task failed."
                        }
                    }
                    : new[]
                {
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.WaitingTimedOut,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = vehicle.ReservationId,
                        Message = "Waiting dispatch exceeded retry limit; vehicle marked timed out."
                    },
                    new MockSimulationEvent
                    {
                        Tick = tick,
                        EventType = MockSimulationEventType.ReplanRequired,
                        VehicleId = vehicle.VehicleId,
                        TaskId = taskId,
                        ResourceId = vehicle.ReservationId,
                        Message = "Waiting timeout reached; running replan is required in the next phase."
                    }
                },
                VehicleStates = GetVehicleStates()
            };
        }

        private IReadOnlyList<MockVehicleRuntimeState> GetVehicleStatesNoLock()
        {
            return _vehicles.Values
                .OrderBy(vehicle => vehicle.VehicleId, StringComparer.OrdinalIgnoreCase)
                .Select(vehicle => vehicle.Clone())
                .ToArray();
        }

        private long CurrentTick
        {
            get
            {
                lock (_syncRoot)
                {
                    return _tick;
                }
            }
        }

        private string ResolveCreatedTaskId(string taskId)
        {
            lock (_syncRoot)
            {
                return ResolveCreatedTaskIdNoLock(taskId);
            }
        }

        private string ResolveCreatedTaskIdNoLock(string taskId) =>
            _createdTaskIds.TryGetValue(taskId, out var createdTaskId) ? createdTaskId : taskId;

        private MockVehicleRuntimeState? FindVehicleByTask(string taskId)
        {
            lock (_syncRoot)
            {
                return _vehicles.Values.FirstOrDefault(vehicle =>
                    string.Equals(vehicle.CurrentTaskId, taskId, StringComparison.OrdinalIgnoreCase))?.Clone();
            }
        }

        private static MockSimulationTickResult Failure(
            string? message,
            string? vehicleId = null,
            string? taskId = null) => new()
        {
            Succeeded = false,
            Message = string.IsNullOrWhiteSpace(message) ? "Simulation operation failed." : message,
            Events = new[]
            {
                new MockSimulationEvent
                {
                    EventType = MockSimulationEventType.SimulationFailed,
                    VehicleId = vehicleId,
                    TaskId = taskId,
                    Message = string.IsNullOrWhiteSpace(message) ? "Simulation operation failed." : message
                }
            }
        };

        private static MockVehicleRuntimeState CopyVehicle(
            MockVehicleRuntimeState source,
            MockVehicleSimulationState? state = null,
            string? currentNodeId = null,
            string? currentEdgeId = null,
            string? currentTaskId = null,
            string? targetNodeId = null,
            string? reservationId = null,
            string? planId = null,
            int? currentSegmentSequence = null,
            DateTimeOffset? waitingSince = null,
            DateTimeOffset? lastRetryAt = null,
            int? waitRetryCount = null,
            int? replanCount = null) => new()
        {
            VehicleId = source.VehicleId,
            Brand = source.Brand,
            State = state ?? source.State,
            CurrentNodeId = currentNodeId ?? source.CurrentNodeId,
            CurrentEdgeId = currentEdgeId ?? source.CurrentEdgeId,
            CurrentTaskId = currentTaskId ?? source.CurrentTaskId,
            TargetNodeId = targetNodeId ?? source.TargetNodeId,
            CurrentSegmentSequence = currentSegmentSequence ?? source.CurrentSegmentSequence,
            ReservationId = reservationId ?? source.ReservationId,
            PlanId = planId ?? source.PlanId,
            WaitingSince = waitingSince,
            LastRetryAt = lastRetryAt ?? source.LastRetryAt,
            WaitRetryCount = waitRetryCount ?? source.WaitRetryCount,
            ReplanCount = replanCount ?? source.ReplanCount,
            BatteryLevel = source.BatteryLevel,
            IsOnline = source.IsOnline,
            HasFault = source.HasFault,
            FaultCode = source.FaultCode,
            UpdatedAt = DateTimeOffset.Now
        };

        private static MockVehicleRuntimeState CopyFault(
            MockVehicleRuntimeState source,
            bool hasFault,
            string? faultCode) => new()
        {
            VehicleId = source.VehicleId,
            Brand = source.Brand,
            State = source.State,
            CurrentNodeId = source.CurrentNodeId,
            CurrentEdgeId = source.CurrentEdgeId,
            CurrentTaskId = source.CurrentTaskId,
            TargetNodeId = source.TargetNodeId,
            CurrentSegmentSequence = source.CurrentSegmentSequence,
            ReservationId = source.ReservationId,
            PlanId = source.PlanId,
            WaitingSince = source.WaitingSince,
            LastRetryAt = source.LastRetryAt,
            WaitRetryCount = source.WaitRetryCount,
            ReplanCount = source.ReplanCount,
            BatteryLevel = source.BatteryLevel,
            IsOnline = source.IsOnline,
            HasFault = hasFault,
            FaultCode = faultCode,
            UpdatedAt = DateTimeOffset.Now
        };

        private static RequestContext Context(string operation) => new()
        {
            SourceModule = nameof(MockFleetSimulationEngine),
            CorrelationId = operation
        };

        private static int CalculateProgress(int segmentCount, int sequence)
        {
            var max = Math.Max(1, segmentCount);
            return Math.Clamp((int)Math.Round(sequence * 100.0 / max), 0, 99);
        }

        private static MockSimulationEvent FailureEvent(
            long tick,
            string vehicleId,
            string taskId,
            string? message) => new()
        {
            Tick = tick,
            EventType = MockSimulationEventType.SimulationFailed,
            VehicleId = vehicleId,
            TaskId = taskId,
            Message = string.IsNullOrWhiteSpace(message) ? "Simulation step failed." : message
        };

        private static bool IsTickManagedState(MockVehicleSimulationState state) =>
            state is MockVehicleSimulationState.Running or MockVehicleSimulationState.WaitingForTraffic;

        private async Task ReleaseExecutionReservationAsync(
            string taskId,
            CancellationToken cancellationToken)
        {
            if (_dispatchOrchestrationService is null || _routeReservationService is null)
            {
                return;
            }

            var execution = await _dispatchOrchestrationService.GetExecutionAsync(
                new GetDispatchExecutionRequest
                {
                    Context = Context("ReleaseFaultReservation"),
                    TaskId = taskId
                },
                cancellationToken).ConfigureAwait(false);
            if (execution.Success && !string.IsNullOrWhiteSpace(execution.Data?.ReservationId))
            {
                await _routeReservationService.ReleaseReservationAsync(
                    new ReleaseRouteReservationRequest
                    {
                        Context = Context("ReleaseFaultReservation"),
                        ReservationId = execution.Data.ReservationId,
                        Reason = "Mock simulation fault release"
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ReleaseVehicleResourcesAsync(
            string vehicleId,
            string taskId,
            CancellationToken cancellationToken)
        {
            if (_trafficControlService is null)
            {
                return;
            }

            await _trafficControlService.ReleaseAsync(
                new TrafficReleaseRequest
                {
                    Context = Context("ReleaseVehicleResources"),
                    AgvId = vehicleId,
                    TaskId = taskId,
                    Reason = "Mock simulation resource release"
                },
                cancellationToken).ConfigureAwait(false);
        }

        private void UpsertVehicleSnapshot(
            MockVehicleRuntimeState vehicle,
            RobotState state,
            string? taskId,
            bool hasAlarm,
            string? alarmCode = null,
            string? alarmMessage = null)
        {
            _vehicleStateStore?.UpsertStatus(new VehicleStatusSnapshot
            {
                VehicleId = vehicle.VehicleId,
                Brand = vehicle.Brand,
                State = state,
                BatteryLevel = vehicle.BatteryLevel,
                Location = vehicle.CurrentNodeId,
                CurrentTaskId = taskId,
                IsOnline = vehicle.IsOnline,
                HasAlarm = hasAlarm,
                ActiveAlarmCode = alarmCode,
                ActiveAlarmMessage = alarmMessage,
                ReportedAt = DateTime.Now,
                Position = new MapPosition { NodeId = vehicle.CurrentNodeId },
                Telemetry = new Dictionary<string, string>
                {
                    ["MockSimulationState"] = vehicle.State.ToString()
                }
            });
        }

        private readonly record struct RouteTrafficConflict(
            TrafficResourceType ResourceType,
            string ResourceId,
            TrafficResourceState State);
    }
}
