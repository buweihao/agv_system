using System.Collections.Concurrent;
using System.Text.Json;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Enums;
using AgvDispatcher.Core.Contracts.Dispatching.Events;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Contracts.Dispatching.Models;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Dispatching.Results;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Planning.Interfaces;
using AgvDispatcher.Core.Contracts.Planning.Requests;
using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Events;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    /// <summary>
    /// Coordinates the first version of the end-to-end dispatch workflow without owning
    /// map, planning, traffic, reservation, or vehicle-protocol domain logic.
    /// </summary>
    public sealed class DispatchOrchestrationService : IDispatchOrchestrationService
    {
        private readonly ITaskService _taskService;
        private readonly IVehicleService _vehicleService;
        private readonly IDispatchScoringService _dispatchScoringService;
        private readonly IMapService _mapService;
        private readonly IPathPlanner _pathPlanner;
        private readonly ITrafficControlService _trafficControlService;
        private readonly IRouteReservationService _routeReservationService;
        private readonly IVehicleAdapterManager _vehicleAdapterManager;
        private readonly IAuditTrailService? _auditTrailService;
        private readonly IEventAggregator? _eventAggregator;
        private readonly ConcurrentDictionary<string, DispatchExecutionDto> _executions =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the dispatch orchestration service with existing module contracts.
        /// </summary>
        public DispatchOrchestrationService(
            ITaskService taskService,
            IVehicleService vehicleService,
            IDispatchScoringService dispatchScoringService,
            IMapService mapService,
            IPathPlanner pathPlanner,
            ITrafficControlService trafficControlService,
            IRouteReservationService routeReservationService,
            IVehicleAdapterManager vehicleAdapterManager,
            IAuditTrailService? auditTrailService = null,
            IEventAggregator? eventAggregator = null)
        {
            _taskService = taskService;
            _vehicleService = vehicleService;
            _dispatchScoringService = dispatchScoringService;
            _mapService = mapService;
            _pathPlanner = pathPlanner;
            _trafficControlService = trafficControlService;
            _routeReservationService = routeReservationService;
            _vehicleAdapterManager = vehicleAdapterManager;
            _auditTrailService = auditTrailService;
            _eventAggregator = eventAggregator;
        }

        /// <inheritdoc />
        public async Task<AgvResult<StartDispatchTaskResultDto>> StartTaskAsync(
            StartDispatchTaskRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TaskId) || request.RollingWindowSize <= 0)
            {
                return Fail<StartDispatchTaskResultDto>(
                    DispatchOrchestrationFailureCode.InvalidRequest,
                    "A task id and a positive rolling window size are required.");
            }

            var task = _taskService.GetTask(request.TaskId);
            if (task is null)
            {
                return Fail<StartDispatchTaskResultDto>(
                    DispatchOrchestrationFailureCode.TaskNotFound,
                    $"Task '{request.TaskId}' was not found.");
            }

            if (task.State != TaskState.Pending)
            {
                return Fail<StartDispatchTaskResultDto>(
                    DispatchOrchestrationFailureCode.TaskNotDispatchable,
                    $"Task '{task.TaskId}' is {task.State} and cannot be dispatched.");
            }

            var vehicleSelection = SelectVehicle(task, request.PreferredVehicleId);
            if (!vehicleSelection.Success)
            {
                return Fail<StartDispatchTaskResultDto>(vehicleSelection.Code, vehicleSelection.Message);
            }

            var vehicleId = vehicleSelection.VehicleId!;
            var execution = new DispatchExecutionDto
            {
                ExecutionId = Guid.NewGuid().ToString("N"),
                TaskId = task.TaskId,
                VehicleId = vehicleId,
                State = DispatchExecutionState.SelectingVehicle,
                CurrentNodeId = FirstNonEmpty(task.CurrentNodeId, task.SourceNodeId),
                RollingWindowSize = request.RollingWindowSize,
                CreatedAt = DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            };
            SaveExecution(execution, DispatchOrchestrationEventType.VehicleSelected, $"Vehicle {vehicleId} selected.");

            var mapResult = _mapService.GetCurrentMap(new GetMapSnapshotRequest { Context = request.Context });
            if (!mapResult.Success || mapResult.Data is null)
            {
                return FailExecution<StartDispatchTaskResultDto>(
                    execution,
                    DispatchOrchestrationFailureCode.MapUnavailable,
                    mapResult.Message);
            }

            var map = mapResult.Data;
            execution = CopyExecution(
                execution,
                state: DispatchExecutionState.PlanningPath,
                mapId: map.MapId,
                mapVersion: map.Version);
            SaveExecution(execution, DispatchOrchestrationEventType.MapLoaded, $"Map {map.MapId}/{map.Version} loaded.");

            var startNodeId = FirstNonEmpty(task.CurrentNodeId, task.SourceNodeId);
            if (string.IsNullOrWhiteSpace(startNodeId) || string.IsNullOrWhiteSpace(task.TargetNodeId))
            {
                return FailExecution<StartDispatchTaskResultDto>(
                    execution,
                    DispatchOrchestrationFailureCode.InvalidRequest,
                    "The task route must contain both a start and a target node.");
            }

            var maxReplans = Math.Max(0, request.MaxReplanCount);
            for (var attempt = 0; attempt <= maxReplans; attempt++)
            {
                var trafficResult = await _trafficControlService
                    .GetTrafficSnapshotAsync(request.Context, cancellationToken)
                    .ConfigureAwait(false);
                if (!trafficResult.Success || trafficResult.Data is null)
                {
                    return FailExecution<StartDispatchTaskResultDto>(
                        execution,
                        DispatchOrchestrationFailureCode.InternalError,
                        trafficResult.Message);
                }

                Publish(execution, DispatchOrchestrationEventType.TrafficSnapshotLoaded, "Traffic snapshot loaded.");

                // The planner remains stateless: orchestration translates runtime traffic
                // ownership/state into a PathPlanConstraint and supplies it with the map.
                var constraint = DispatchTrafficConstraintMapper.Map(trafficResult.Data, task.TaskId, vehicleId);
                var planResult = await _pathPlanner.PlanAsync(new PathPlanRequest
                {
                    Context = request.Context,
                    MapSnapshot = map,
                    VehicleId = vehicleId,
                    StartNodeId = startNodeId,
                    TargetNodeId = task.TargetNodeId,
                    Constraint = constraint
                }, cancellationToken).ConfigureAwait(false);

                if (!planResult.Success || planResult.Data is null)
                {
                    return FailExecution<StartDispatchTaskResultDto>(
                        execution,
                        DispatchOrchestrationFailureCode.PathPlanningFailed,
                        planResult.Message);
                }

                var plan = planResult.Data;
                if (!plan.IsReachable || plan.Segments is null)
                {
                    var blockedByTraffic = constraint.ForbiddenNodeIds?.Count > 0 ||
                        constraint.ForbiddenEdgeIds?.Count > 0;
                    return FailExecution<StartDispatchTaskResultDto>(
                        execution,
                        blockedByTraffic
                            ? DispatchOrchestrationFailureCode.ReplanRequired
                            : DispatchOrchestrationFailureCode.PathNotReachable,
                        $"No route is reachable from {startNodeId} to {task.TargetNodeId}.");
                }

                execution = CopyExecution(execution, state: DispatchExecutionState.ReservingRoute, planId: plan.PlanId);
                SaveExecution(execution, DispatchOrchestrationEventType.PathPlanned, $"Path {plan.PlanId} planned.");

                var reservationResult = await _routeReservationService.CreateReservationAsync(
                    new CreateRouteReservationRequest
                    {
                        Context = request.Context,
                        TaskId = task.TaskId,
                        VehicleId = vehicleId,
                        PlanId = plan.PlanId,
                        MapId = map.MapId,
                        MapVersion = map.Version,
                        Segments = plan.Segments,
                        RollingWindowSize = request.RollingWindowSize
                    },
                    cancellationToken).ConfigureAwait(false);
                if (!reservationResult.Success || reservationResult.Data is null)
                {
                    return FailExecution<StartDispatchTaskResultDto>(
                        execution,
                        DispatchOrchestrationFailureCode.RouteReservationFailed,
                        reservationResult.Message);
                }

                var reservationId = reservationResult.Data.ReservationId;
                execution = CopyExecution(
                    execution,
                    state: DispatchExecutionState.LockingFirstWindow,
                    reservationId: reservationId);
                SaveExecution(execution, DispatchOrchestrationEventType.RouteReservationCreated,
                    $"Route reservation {reservationId} created.");

                var lockResult = await _routeReservationService.AcquireNextWindowAsync(
                    new AcquireNextRouteWindowRequest
                    {
                        Context = request.Context,
                        ReservationId = reservationId,
                        CurrentNodeId = startNodeId,
                        CurrentSegmentSequence = 0,
                        RollingWindowSize = request.RollingWindowSize
                    },
                    cancellationToken).ConfigureAwait(false);

                if (lockResult.Success && lockResult.Data?.Acquired == true)
                {
                    return await CompleteDispatchStartAsync(
                        request,
                        task,
                        map,
                        plan,
                        execution,
                        reservationId,
                        cancellationToken).ConfigureAwait(false);
                }

                if (lockResult.Data?.ShouldWait == true)
                {
                    execution = CopyExecution(execution, state: DispatchExecutionState.WaitingForTraffic);
                    SaveExecution(execution, DispatchOrchestrationEventType.WaitingForTraffic, lockResult.Data.Message);
                    return AgvResult<StartDispatchTaskResultDto>.Ok(new StartDispatchTaskResultDto
                    {
                        Execution = execution,
                        TaskId = task.TaskId,
                        VehicleId = vehicleId,
                        PlanId = plan.PlanId,
                        ReservationId = reservationId,
                        PathPlanned = true,
                        ReservationCreated = true,
                        FirstWindowLocked = false,
                        VehicleCommandSent = false,
                        Message = lockResult.Data.Message
                    });
                }

                if (lockResult.Data?.RequiresReplan == true && attempt < maxReplans)
                {
                    await _routeReservationService.ReleaseReservationAsync(
                        new ReleaseRouteReservationRequest
                        {
                            Context = request.Context,
                            ReservationId = reservationId,
                            Reason = "Release before dispatch replan"
                        },
                        cancellationToken).ConfigureAwait(false);
                    execution = CopyExecution(
                        execution,
                        state: DispatchExecutionState.Replanning,
                        reservationId: string.Empty);
                    SaveExecution(execution, DispatchOrchestrationEventType.ReplanRequired,
                        $"Replanning attempt {attempt + 1} of {maxReplans}.");
                    continue;
                }

                var failureCode = lockResult.Data?.RequiresReplan == true
                    ? DispatchOrchestrationFailureCode.ReplanRequired
                    : DispatchOrchestrationFailureCode.FirstWindowAcquireFailed;
                return FailExecution<StartDispatchTaskResultDto>(execution, failureCode, lockResult.Message);
            }

            return FailExecution<StartDispatchTaskResultDto>(
                execution,
                DispatchOrchestrationFailureCode.ReplanRequired,
                "The dispatch route could not be locked after replanning.");
        }

        /// <inheritdoc />
        public async Task<AgvResult<AdvanceDispatchRouteResultDto>> AdvanceRouteAsync(
            AdvanceDispatchRouteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TaskId) ||
                !_executions.TryGetValue(request.TaskId, out var execution))
            {
                return Fail<AdvanceDispatchRouteResultDto>(
                    DispatchOrchestrationFailureCode.TaskNotFound,
                    "The dispatch execution was not found.");
            }

            if (!string.Equals(execution.VehicleId, request.VehicleId, StringComparison.OrdinalIgnoreCase))
            {
                return Fail<AdvanceDispatchRouteResultDto>(
                    DispatchOrchestrationFailureCode.VehicleNotFound,
                    "The vehicle does not own this dispatch execution.");
            }

            var occupancyResult = await _trafficControlService.UpdateAgvOccupancyAsync(
                new AgvOccupancyUpdateRequest
                {
                    Context = request.Context,
                    AgvId = request.VehicleId,
                    TaskId = request.TaskId,
                    CurrentNodeId = request.CurrentNodeId,
                    CurrentEdgeId = request.CurrentEdgeId,
                    OccupiedNodeIds = string.IsNullOrWhiteSpace(request.CurrentNodeId)
                        ? Array.Empty<string>()
                        : new[] { request.CurrentNodeId },
                    OccupiedEdgeIds = string.IsNullOrWhiteSpace(request.CurrentEdgeId)
                        ? Array.Empty<string>()
                        : new[] { request.CurrentEdgeId },
                    ReportTime = DateTimeOffset.Now
                },
                cancellationToken).ConfigureAwait(false);
            if (!occupancyResult.Success)
            {
                return Fail<AdvanceDispatchRouteResultDto>(
                    DispatchOrchestrationFailureCode.TrafficResourceBusy,
                    occupancyResult.Message);
            }

            // Rolling-window progress is deliberately two phase: release segments already
            // traversed, then request the next window from the reservation service.
            var releaseResult = await _routeReservationService.ReleasePassedResourcesAsync(
                new ReleasePassedRouteResourcesRequest
                {
                    Context = request.Context,
                    ReservationId = execution.ReservationId ?? string.Empty,
                    PassedSegmentSequence = request.PassedSegmentSequence,
                    CurrentNodeId = request.CurrentNodeId ?? string.Empty
                },
                cancellationToken).ConfigureAwait(false);
            if (!releaseResult.Success)
            {
                return Fail<AdvanceDispatchRouteResultDto>(
                    DispatchOrchestrationFailureCode.RouteReservationFailed,
                    releaseResult.Message);
            }

            var acquiredNextWindow = false;
            var shouldWait = false;
            var requiresReplan = false;
            var message = releaseResult.Message;
            if (request.AcquireNextWindow)
            {
                var acquireResult = await _routeReservationService.AcquireNextWindowAsync(
                    new AcquireNextRouteWindowRequest
                    {
                        Context = request.Context,
                        ReservationId = execution.ReservationId ?? string.Empty,
                        CurrentNodeId = request.CurrentNodeId ?? string.Empty,
                        CurrentSegmentSequence = request.PassedSegmentSequence,
                        RollingWindowSize = execution.RollingWindowSize
                    },
                    cancellationToken).ConfigureAwait(false);
                acquiredNextWindow = acquireResult.Success && acquireResult.Data?.Acquired == true;
                shouldWait = acquireResult.Data?.ShouldWait == true;
                requiresReplan = acquireResult.Data?.RequiresReplan == true;
                message = acquireResult.Data?.Message ?? acquireResult.Message;
                if (!acquireResult.Success && acquireResult.Data is null)
                {
                    return Fail<AdvanceDispatchRouteResultDto>(
                        DispatchOrchestrationFailureCode.FirstWindowAcquireFailed,
                        acquireResult.Message);
                }
            }

            var state = requiresReplan
                ? DispatchExecutionState.Replanning
                : shouldWait
                    ? DispatchExecutionState.WaitingForTraffic
                    : DispatchExecutionState.Running;
            execution = CopyExecution(
                execution,
                state: state,
                currentNodeId: request.CurrentNodeId,
                currentSegmentSequence: request.PassedSegmentSequence);
            SaveExecution(
                execution,
                requiresReplan
                    ? DispatchOrchestrationEventType.ReplanRequired
                    : shouldWait
                        ? DispatchOrchestrationEventType.WaitingForTraffic
                        : DispatchOrchestrationEventType.RouteAdvanced,
                message);

            return AgvResult<AdvanceDispatchRouteResultDto>.Ok(new AdvanceDispatchRouteResultDto
            {
                Execution = execution,
                TaskId = request.TaskId,
                VehicleId = request.VehicleId,
                ReleasedUpToSegmentSequence = request.PassedSegmentSequence,
                ReleasedPassedResources = true,
                AcquiredNextWindow = acquiredNextWindow,
                ShouldWait = shouldWait,
                RequiresReplan = requiresReplan,
                Message = message
            });
        }

        /// <inheritdoc />
        public async Task<AgvResult<RetryWaitingDispatchResultDto>> RetryWaitingTaskAsync(
            RetryWaitingDispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TaskId))
            {
                return Fail<RetryWaitingDispatchResultDto>(
                    DispatchOrchestrationFailureCode.InvalidRequest,
                    "A task id is required.");
            }

            if (!_executions.TryGetValue(request.TaskId, out var execution))
            {
                return Fail<RetryWaitingDispatchResultDto>(
                    DispatchOrchestrationFailureCode.TaskNotFound,
                    "The dispatch execution was not found.");
            }

            if (execution.State != DispatchExecutionState.WaitingForTraffic)
            {
                return Fail<RetryWaitingDispatchResultDto>(
                    DispatchOrchestrationFailureCode.TaskNotDispatchable,
                    $"Execution '{execution.ExecutionId}' is {execution.State}, not WaitingForTraffic.");
            }

            if (string.IsNullOrWhiteSpace(execution.ReservationId) ||
                string.IsNullOrWhiteSpace(execution.VehicleId) ||
                string.IsNullOrWhiteSpace(execution.PlanId))
            {
                return Fail<RetryWaitingDispatchResultDto>(
                    DispatchOrchestrationFailureCode.InvalidRequest,
                    "The waiting execution is missing its vehicle, plan, or reservation identifier.");
            }

            var reservationId = execution.ReservationId!;
            var vehicleId = execution.VehicleId!;
            var planId = execution.PlanId!;

            var task = _taskService.GetTask(request.TaskId);
            if (task is null)
            {
                return Fail<RetryWaitingDispatchResultDto>(
                    DispatchOrchestrationFailureCode.TaskNotFound,
                    $"Task '{request.TaskId}' was not found.");
            }

            // Retry only the existing rolling-window reservation. Replanning and reservation
            // creation deliberately remain outside this explicit waiting-state entry point.
            var acquireResult = await _routeReservationService.AcquireNextWindowAsync(
                new AcquireNextRouteWindowRequest
                {
                    Context = request.Context,
                    ReservationId = reservationId,
                    CurrentNodeId = FirstNonEmpty(execution.CurrentNodeId, task.SourceNodeId),
                    CurrentSegmentSequence = execution.CurrentSegmentSequence,
                    RollingWindowSize = execution.RollingWindowSize
                },
                cancellationToken).ConfigureAwait(false);

            if (acquireResult.Data?.ShouldWait == true)
            {
                execution = CopyExecution(execution, state: DispatchExecutionState.WaitingForTraffic);
                SaveExecution(execution, DispatchOrchestrationEventType.WaitingForTraffic, acquireResult.Data.Message);
                return AgvResult<RetryWaitingDispatchResultDto>.Ok(new RetryWaitingDispatchResultDto
                {
                    Execution = execution,
                    TaskId = execution.TaskId,
                    VehicleId = vehicleId,
                    PlanId = planId,
                    ReservationId = reservationId,
                    ShouldWait = true,
                    Message = acquireResult.Data.Message
                });
            }

            if (acquireResult.Data?.RequiresReplan == true)
            {
                execution = CopyExecution(execution, state: DispatchExecutionState.Replanning);
                SaveExecution(execution, DispatchOrchestrationEventType.ReplanRequired, acquireResult.Data.Message);
                return AgvResult<RetryWaitingDispatchResultDto>.Ok(new RetryWaitingDispatchResultDto
                {
                    Execution = execution,
                    TaskId = execution.TaskId,
                    VehicleId = vehicleId,
                    PlanId = planId,
                    ReservationId = reservationId,
                    RequiresReplan = true,
                    Message = acquireResult.Data.Message
                });
            }

            if (!acquireResult.Success || acquireResult.Data?.Acquired != true)
            {
                return Fail<RetryWaitingDispatchResultDto>(
                    DispatchOrchestrationFailureCode.FirstWindowAcquireFailed,
                    acquireResult.Message);
            }

            var reservationResult = await _routeReservationService.GetReservationAsync(
                new GetRouteReservationRequest
                {
                    Context = request.Context,
                    ReservationId = reservationId
                },
                cancellationToken).ConfigureAwait(false);
            if (!reservationResult.Success || reservationResult.Data is null)
            {
                return Fail<RetryWaitingDispatchResultDto>(
                    DispatchOrchestrationFailureCode.RouteReservationFailed,
                    reservationResult.Message);
            }

            // A running execution can enter WaitingForTraffic while advancing its rolling
            // window. In that case the vehicle already owns the task and must not receive a
            // second AssignTask command; only restore the execution to Running.
            if (task.State == TaskState.Running)
            {
                execution = CopyExecution(execution, state: DispatchExecutionState.Running);
                SaveExecution(
                    execution,
                    DispatchOrchestrationEventType.NextWindowLocked,
                    "The next rolling route window is locked.");
                return AgvResult<RetryWaitingDispatchResultDto>.Ok(new RetryWaitingDispatchResultDto
                {
                    Execution = execution,
                    TaskId = execution.TaskId,
                    VehicleId = vehicleId,
                    PlanId = planId,
                    ReservationId = reservationId,
                    FirstWindowLocked = true,
                    VehicleCommandSent = false,
                    Message = acquireResult.Data.Message
                });
            }

            var startResult = await CompleteDispatchStartAsync(
                new StartDispatchTaskRequest
                {
                    Context = request.Context,
                    TaskId = request.TaskId,
                    RollingWindowSize = execution.RollingWindowSize,
                    SendVehicleCommand = request.SendVehicleCommand
                },
                task,
                new MapSnapshotDto
                {
                    MapId = execution.MapId ?? reservationResult.Data.MapId,
                    Version = execution.MapVersion ?? reservationResult.Data.MapVersion
                },
                new PathPlanResult
                {
                    PlanId = planId,
                    IsReachable = true,
                    Segments = reservationResult.Data.Segments.Select(segment => segment.Segment).ToArray()
                },
                execution,
                reservationId,
                cancellationToken).ConfigureAwait(false);
            if (!startResult.Success || startResult.Data is null)
            {
                return AgvResult<RetryWaitingDispatchResultDto>.Fail(
                    startResult.Error ?? new AgvError(startResult.Code.ToString(), startResult.Message));
            }

            return AgvResult<RetryWaitingDispatchResultDto>.Ok(new RetryWaitingDispatchResultDto
            {
                Execution = startResult.Data.Execution,
                TaskId = startResult.Data.TaskId,
                VehicleId = startResult.Data.VehicleId,
                PlanId = startResult.Data.PlanId,
                ReservationId = startResult.Data.ReservationId,
                FirstWindowLocked = startResult.Data.FirstWindowLocked,
                VehicleCommandSent = startResult.Data.VehicleCommandSent,
                CommandId = startResult.Data.CommandId,
                Message = startResult.Data.Message
            });
        }

        /// <inheritdoc />
        public async Task<AgvResult> CancelTaskAsync(
            CancelDispatchTaskRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TaskId))
            {
                return Fail(DispatchOrchestrationFailureCode.InvalidRequest, "A task id is required.");
            }

            var task = _taskService.GetTask(request.TaskId);
            if (task is null)
            {
                return Fail(DispatchOrchestrationFailureCode.TaskNotFound, $"Task '{request.TaskId}' was not found.");
            }

            if (!_executions.TryGetValue(request.TaskId, out var execution))
            {
                _taskService.CancelTask(request.TaskId, request.Reason);
                return AgvResult.Ok($"Task '{request.TaskId}' was canceled without dispatch execution.");
            }

            execution = CopyExecution(execution, state: DispatchExecutionState.Canceling);
            SaveExecution(execution, DispatchOrchestrationEventType.TaskCanceling, request.Reason);

            if (request.SendCancelCommand && !string.IsNullOrWhiteSpace(execution.VehicleId))
            {
                var commandResult = await _vehicleAdapterManager.SendCommandAsync(new DispatchCommand
                {
                    CommandId = Guid.NewGuid().ToString("N"),
                    CommandType = DispatchCommandType.CancelTask,
                    TaskId = request.TaskId,
                    VehicleId = execution.VehicleId,
                    IssuedBy = nameof(DispatchOrchestrationService),
                    CorrelationId = request.Context.RequestId
                }, cancellationToken).ConfigureAwait(false);
                if (!commandResult.Succeeded)
                {
                    return Fail(DispatchOrchestrationFailureCode.CancelFailed, commandResult.Message);
                }
            }

            if (request.ReleaseReservation && !string.IsNullOrWhiteSpace(execution.ReservationId))
            {
                var releaseResult = await _routeReservationService.ReleaseReservationAsync(
                    new ReleaseRouteReservationRequest
                    {
                        Context = request.Context,
                        ReservationId = execution.ReservationId,
                        Reason = $"Task canceled: {request.Reason}"
                    },
                    cancellationToken).ConfigureAwait(false);
                if (!releaseResult.Success)
                {
                    return Fail(DispatchOrchestrationFailureCode.CancelFailed, releaseResult.Message);
                }
            }

            _taskService.CancelTask(request.TaskId, request.Reason);
            execution = CopyExecution(execution, state: DispatchExecutionState.Canceled);
            SaveExecution(execution, DispatchOrchestrationEventType.TaskCanceled, request.Reason);
            return AgvResult.Ok($"Task '{request.TaskId}' was canceled.");
        }

        /// <inheritdoc />
        public async Task<AgvResult> CompleteTaskAsync(
            CompleteDispatchTaskRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.TaskId) ||
                string.IsNullOrWhiteSpace(request.VehicleId))
            {
                return Fail(
                    DispatchOrchestrationFailureCode.InvalidRequest,
                    "A task id and vehicle id are required.");
            }

            var task = _taskService.GetTask(request.TaskId);
            if (task is null)
            {
                return Fail(
                    DispatchOrchestrationFailureCode.TaskNotFound,
                    $"Task '{request.TaskId}' was not found.");
            }

            if (!_executions.TryGetValue(request.TaskId, out var execution))
            {
                return Fail(
                    DispatchOrchestrationFailureCode.TaskNotFound,
                    $"Dispatch execution for task '{request.TaskId}' was not found.");
            }

            if (!string.Equals(execution.VehicleId, request.VehicleId, StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    DispatchOrchestrationFailureCode.VehicleNotFound,
                    "The vehicle does not own this dispatch execution.");
            }

            var finalNodeId = FirstNonEmpty(request.CurrentNodeId, task.TargetNodeId);
            if (!string.IsNullOrWhiteSpace(finalNodeId))
            {
                var occupancyResult = await _trafficControlService.UpdateAgvOccupancyAsync(
                    new AgvOccupancyUpdateRequest
                    {
                        Context = request.Context,
                        AgvId = request.VehicleId,
                        TaskId = request.TaskId,
                        CurrentNodeId = finalNodeId,
                        OccupiedNodeIds = new[] { finalNodeId },
                        ReportTime = DateTimeOffset.Now
                    },
                    cancellationToken).ConfigureAwait(false);
                if (!occupancyResult.Success)
                {
                    return Fail(
                        DispatchOrchestrationFailureCode.TrafficResourceBusy,
                        occupancyResult.Message);
                }
            }

            // Completion closes the rolling-window lifecycle. Any segments not explicitly
            // released by progress reports are released here so they cannot block the next task.
            if (request.ReleaseReservation && !string.IsNullOrWhiteSpace(execution.ReservationId))
            {
                var releaseResult = await _routeReservationService.ReleaseReservationAsync(
                    new ReleaseRouteReservationRequest
                    {
                        Context = request.Context,
                        ReservationId = execution.ReservationId,
                        Reason = "Task completed"
                    },
                    cancellationToken).ConfigureAwait(false);
                if (!releaseResult.Success)
                {
                    return Fail(
                        DispatchOrchestrationFailureCode.RouteReservationFailed,
                        releaseResult.Message);
                }
            }

            _taskService.UpdateTaskProgress(request.TaskId, 100, finalNodeId);
            _taskService.UpdateTaskState(request.TaskId, TaskState.Completed);
            execution = CopyExecution(
                execution,
                state: DispatchExecutionState.Completed,
                currentNodeId: finalNodeId);
            SaveExecution(execution, DispatchOrchestrationEventType.TaskCompleted, "Task execution completed.");
            return AgvResult.Ok($"Task '{request.TaskId}' was completed.");
        }

        /// <inheritdoc />
        public Task<AgvResult<DispatchExecutionDto>> GetExecutionAsync(
            GetDispatchExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request is null)
            {
                return Task.FromResult(Fail<DispatchExecutionDto>(
                    DispatchOrchestrationFailureCode.InvalidRequest,
                    "A request is required."));
            }

            DispatchExecutionDto? execution = null;
            if (!string.IsNullOrWhiteSpace(request.TaskId))
            {
                _executions.TryGetValue(request.TaskId, out execution);
            }

            if (execution is null && !string.IsNullOrWhiteSpace(request.ExecutionId))
            {
                execution = _executions.Values.FirstOrDefault(item =>
                    string.Equals(item.ExecutionId, request.ExecutionId, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(execution is null
                ? Fail<DispatchExecutionDto>(
                    DispatchOrchestrationFailureCode.TaskNotFound,
                    "The dispatch execution was not found.")
                : AgvResult<DispatchExecutionDto>.Ok(execution));
        }

        private async Task<AgvResult<StartDispatchTaskResultDto>> CompleteDispatchStartAsync(
            StartDispatchTaskRequest request,
            TaskOrder task,
            MapSnapshotDto map,
            PathPlanResult plan,
            DispatchExecutionDto execution,
            string reservationId,
            CancellationToken cancellationToken)
        {
            execution = CopyExecution(execution, state: DispatchExecutionState.ReadyToDispatch);
            SaveExecution(execution, DispatchOrchestrationEventType.FirstWindowLocked, "The first route window is locked.");

            DispatchResult? commandResult = null;
            var commandId = Guid.NewGuid().ToString("N");
            if (request.SendVehicleCommand)
            {
                commandResult = await _vehicleAdapterManager.SendCommandAsync(new DispatchCommand
                {
                    CommandId = commandId,
                    CommandType = DispatchCommandType.AssignTask,
                    TaskId = task.TaskId,
                    VehicleId = execution.VehicleId!,
                    SourceNodeId = task.SourceNodeId,
                    TargetNodeId = task.TargetNodeId,
                    Priority = (int)task.Priority,
                    IssuedBy = nameof(DispatchOrchestrationService),
                    CorrelationId = request.Context.RequestId,
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        plan.PlanId,
                        ReservationId = reservationId,
                        map.MapId,
                        MapVersion = map.Version,
                        plan.Segments
                    }),
                    Parameters = new Dictionary<string, string>
                    {
                        ["PlanId"] = plan.PlanId,
                        ["ReservationId"] = reservationId,
                        ["MapVersion"] = map.Version
                    }
                }, cancellationToken).ConfigureAwait(false);
                if (!commandResult.Succeeded)
                {
                    var failureMessage = commandResult.Message;
                    var releaseResult = await _routeReservationService.ReleaseReservationAsync(
                        new ReleaseRouteReservationRequest
                        {
                            Context = request.Context,
                            ReservationId = reservationId,
                            Reason = "Release reservation because vehicle command failed"
                        },
                        cancellationToken).ConfigureAwait(false);
                    if (!releaseResult.Success)
                    {
                        failureMessage = $"Vehicle command failed: {commandResult.Message}; " +
                            $"reservation release failed: {releaseResult.Message}";
                    }

                    return FailExecution<StartDispatchTaskResultDto>(
                        execution,
                        DispatchOrchestrationFailureCode.VehicleCommandFailed,
                        failureMessage);
                }

                execution = CopyExecution(execution, state: DispatchExecutionState.CommandSent);
                SaveExecution(execution, DispatchOrchestrationEventType.VehicleCommandSent,
                    $"Vehicle command {commandId} sent.");
            }

            _taskService.AssignVehicle(task.TaskId, execution.VehicleId!);
            _taskService.UpdateTaskState(task.TaskId, TaskState.Running);
            execution = CopyExecution(execution, state: DispatchExecutionState.Running);
            SaveExecution(execution, DispatchOrchestrationEventType.RouteAdvanced, "Dispatch execution is running.");

            return AgvResult<StartDispatchTaskResultDto>.Ok(new StartDispatchTaskResultDto
            {
                Execution = execution,
                TaskId = task.TaskId,
                VehicleId = execution.VehicleId!,
                PlanId = plan.PlanId,
                ReservationId = reservationId,
                PathPlanned = true,
                ReservationCreated = true,
                FirstWindowLocked = true,
                VehicleCommandSent = request.SendVehicleCommand,
                CommandId = request.SendVehicleCommand ? commandResult?.CommandId ?? commandId : null,
                Message = "Task dispatch started."
            });
        }

        private VehicleSelectionResult SelectVehicle(TaskOrder task, string? preferredVehicleId)
        {
            if (!string.IsNullOrWhiteSpace(preferredVehicleId))
            {
                var vehicleId = preferredVehicleId.Trim();
                var vehicle = _vehicleService.GetVehicle(vehicleId);
                var status = _vehicleService.GetVehicleStatus(vehicleId);
                if (vehicle is null || status is null)
                {
                    return VehicleSelectionResult.Fail(
                        DispatchOrchestrationFailureCode.VehicleNotFound,
                        $"Vehicle '{vehicleId}' was not found.");
                }

                var score = _dispatchScoringService.ScoreAndSelectVehicle(
                    task,
                    new[] { (Vehicle: vehicle, Status: status) });
                return score.Success
                    ? VehicleSelectionResult.Ok(vehicleId)
                    : VehicleSelectionResult.Fail(
                        DispatchOrchestrationFailureCode.VehicleNotAvailable,
                        score.Reason);
            }

            var vehicles = _vehicleService.GetVehicles();
            var candidates = _vehicleService.GetVehicleStatuses()
                .Select(status => (Vehicle: vehicles.FirstOrDefault(vehicle =>
                    string.Equals(vehicle.VehicleId, status.VehicleId, StringComparison.OrdinalIgnoreCase)), Status: status))
                .Where(candidate => candidate.Vehicle is not null)
                .Select(candidate => (candidate.Vehicle!, candidate.Status));
            var result = _dispatchScoringService.ScoreAndSelectVehicle(task, candidates);
            return result.Success
                ? VehicleSelectionResult.Ok(result.SelectedVehicleId!)
                : VehicleSelectionResult.Fail(DispatchOrchestrationFailureCode.VehicleNotAvailable, result.Reason);
        }

        private void SaveExecution(
            DispatchExecutionDto execution,
            DispatchOrchestrationEventType eventType,
            string? message)
        {
            _executions[execution.TaskId] = execution;
            Publish(execution, eventType, message);
            _auditTrailService?.Record(new OperationLog
            {
                Category = "DispatchOrchestration",
                Action = eventType.ToString(),
                Message = message ?? string.Empty,
                TaskId = execution.TaskId,
                VehicleId = execution.VehicleId,
                Operator = nameof(DispatchOrchestrationService)
            });
        }

        private void Publish(
            DispatchExecutionDto execution,
            DispatchOrchestrationEventType eventType,
            string? message) =>
            _eventAggregator?.GetEvent<PubSubEvent<DispatchOrchestrationChangedEvent>>().Publish(
                new DispatchOrchestrationChangedEvent
                {
                    TaskId = execution.TaskId,
                    ExecutionId = execution.ExecutionId,
                    VehicleId = execution.VehicleId,
                    State = execution.State,
                    EventType = eventType,
                    Message = message,
                    OccurredAt = DateTimeOffset.Now
                });

        private AgvResult<T> FailExecution<T>(
            DispatchExecutionDto execution,
            DispatchOrchestrationFailureCode code,
            string? message)
        {
            var failed = CopyExecution(
                execution,
                state: DispatchExecutionState.Failed,
                lastFailureCode: code.ToString(),
                lastFailureMessage: message ?? code.ToString());
            SaveExecution(failed, DispatchOrchestrationEventType.DispatchFailed, message);
            return Fail<T>(code, message);
        }

        private static AgvResult<T> Fail<T>(DispatchOrchestrationFailureCode code, string? message) =>
            AgvResult<T>.Fail(code.ToString(), string.IsNullOrWhiteSpace(message) ? code.ToString() : message);

        private static AgvResult Fail(DispatchOrchestrationFailureCode code, string? message) =>
            AgvResult.Fail(code.ToString(), string.IsNullOrWhiteSpace(message) ? code.ToString() : message);

        private static string FirstNonEmpty(string? first, string? second) =>
            !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;

        private static DispatchExecutionDto CopyExecution(
            DispatchExecutionDto source,
            DispatchExecutionState? state = null,
            string? mapId = null,
            string? mapVersion = null,
            string? planId = null,
            string? reservationId = null,
            string? currentNodeId = null,
            int? currentSegmentSequence = null,
            string? lastFailureCode = null,
            string? lastFailureMessage = null) => new()
        {
            ExecutionId = source.ExecutionId,
            TaskId = source.TaskId,
            VehicleId = source.VehicleId,
            State = state ?? source.State,
            MapId = mapId ?? source.MapId,
            MapVersion = mapVersion ?? source.MapVersion,
            PlanId = planId ?? source.PlanId,
            ReservationId = reservationId ?? source.ReservationId,
            CurrentNodeId = currentNodeId ?? source.CurrentNodeId,
            CurrentSegmentSequence = currentSegmentSequence ?? source.CurrentSegmentSequence,
            RollingWindowSize = source.RollingWindowSize,
            CreatedAt = source.CreatedAt,
            UpdatedAt = DateTimeOffset.Now,
            LastFailureCode = lastFailureCode ?? source.LastFailureCode,
            LastFailureMessage = lastFailureMessage ?? source.LastFailureMessage
        };

        private sealed record VehicleSelectionResult(
            bool Success,
            string? VehicleId,
            DispatchOrchestrationFailureCode Code,
            string Message)
        {
            internal static VehicleSelectionResult Ok(string vehicleId) =>
                new(true, vehicleId, DispatchOrchestrationFailureCode.None, string.Empty);

            internal static VehicleSelectionResult Fail(
                DispatchOrchestrationFailureCode code,
                string message) => new(false, null, code, message);
        }
    }
}
