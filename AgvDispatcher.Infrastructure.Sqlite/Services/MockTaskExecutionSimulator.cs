using System.Collections.Concurrent;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Dispatching.Interfaces;
using AgvDispatcher.Core.Contracts.Dispatching.Requests;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class MockTaskExecutionSimulator : ITaskExecutionSimulator, IDisposable
    {
        private const int TicksPerSegment = 4;
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

        private readonly ITaskService _taskService;
        // private readonly IMapService _mapService;
        private readonly IVehicleAdapterManager _vehicleAdapterManager;
        private readonly IAuditTrailService _auditTrail;
        private readonly IChargeStationRepository _chargeStationRepository;
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly IDispatchOrchestrationService _dispatchOrchestrationService;
        private readonly IRouteReservationService _routeReservationService;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTasks = new(StringComparer.OrdinalIgnoreCase);

        public MockTaskExecutionSimulator(
            ITaskService taskService,
            // IMapService mapService,
            IVehicleAdapterManager vehicleAdapterManager,
            IAuditTrailService auditTrail,
            IChargeStationRepository chargeStationRepository,
            IVehicleStateStore vehicleStateStore,
            IDispatchOrchestrationService dispatchOrchestrationService,
            IRouteReservationService routeReservationService)
        {
            _taskService = taskService;
            // _mapService = mapService;
            _vehicleAdapterManager = vehicleAdapterManager;
            _auditTrail = auditTrail;
            _chargeStationRepository = chargeStationRepository;
            _vehicleStateStore = vehicleStateStore;
            _dispatchOrchestrationService = dispatchOrchestrationService;
            _routeReservationService = routeReservationService;
        }

        public void Start(TaskOrder task, string vehicleId)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (string.IsNullOrWhiteSpace(task.TaskId) || string.IsNullOrWhiteSpace(vehicleId))
            {
                return;
            }

            Cancel(task.TaskId);

            var cancellation = new CancellationTokenSource();
            if (!_runningTasks.TryAdd(task.TaskId, cancellation))
            {
                cancellation.Dispose();
                return;
            }

            _ = RunTaskAsync(task.TaskId, vehicleId.Trim(), cancellation.Token);
        }

        public void Cancel(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            if (_runningTasks.TryRemove(taskId, out var cancellation))
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var taskId in _runningTasks.Keys)
            {
                Cancel(taskId);
            }
        }

        private async System.Threading.Tasks.Task RunTaskAsync(string taskId, string vehicleId, CancellationToken cancellationToken)
        {
            try
            {
                var task = _taskService.GetTask(taskId);
                if (task is null)
                {
                    return;
                }

                _auditTrail.Record(new OperationLog
                {
                    Category = "Task",
                    Action = "SimulationStarted",
                    Message = $"Task {taskId} execution simulation started on {vehicleId}.",
                    TaskId = taskId,
                    VehicleId = vehicleId,
                    Operator = "MockTaskExecutionSimulator"
                });

                var context = new RequestContext { SourceModule = nameof(MockTaskExecutionSimulator) };
                var route = await ResolveRouteAsync(task, context, cancellationToken);
                _taskService.UpdateTaskProgress(taskId, 0, route.FirstOrDefault() ?? task.SourceNodeId);

                var segmentCount = Math.Max(route.Count - 1, 1);
                var totalTicks = segmentCount * TicksPerSegment;
                var completedTicks = 0;

                for (var segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                {
                    var fromNodeId = route[Math.Min(segmentIndex, route.Count - 1)];
                    var toNodeId = route[Math.Min(segmentIndex + 1, route.Count - 1)];

                    for (var tick = 1; tick <= TicksPerSegment; tick++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await System.Threading.Tasks.Task.Delay(TickInterval, cancellationToken);

                        completedTicks++;
                        var progress = Math.Min(95, (int)Math.Round((double)completedTicks / totalTicks * 95));
                        var currentNodeId = tick == TicksPerSegment ? toNodeId : fromNodeId;
                        _taskService.UpdateTaskProgress(taskId, progress, currentNodeId);

                        if (tick == TicksPerSegment)
                        {
                            await _vehicleAdapterManager.SendCommandAsync(new DispatchCommand
                            {
                                CommandType = DispatchCommandType.MoveToNode,
                                TaskId = taskId,
                                VehicleId = vehicleId,
                                TargetNodeId = toNodeId,
                                IssuedBy = nameof(MockTaskExecutionSimulator)
                            }, cancellationToken);

                            var advance = await _dispatchOrchestrationService.AdvanceRouteAsync(
                                new AdvanceDispatchRouteRequest
                                {
                                    Context = context,
                                    TaskId = taskId,
                                    VehicleId = vehicleId,
                                    CurrentNodeId = toNodeId,
                                    PassedSegmentSequence = segmentIndex + 1,
                                    AcquireNextWindow = segmentIndex < segmentCount - 1
                                },
                                cancellationToken);
                            if (!advance.Success || advance.Data is null)
                            {
                                RecordSimulationStop(
                                    "SimulationAdvanceFailed",
                                    $"Task {taskId} could not advance after segment {segmentIndex + 1}: {advance.Message}",
                                    taskId,
                                    vehicleId);
                                return;
                            }

                            if (advance.Data.RequiresReplan)
                            {
                                RecordSimulationStop(
                                    "SimulationReplanRequired",
                                    $"Task {taskId} requires replanning after segment {segmentIndex + 1}.",
                                    taskId,
                                    vehicleId);
                                return;
                            }

                            if (advance.Data.ShouldWait &&
                                !await WaitForTrafficAsync(taskId, vehicleId, context, cancellationToken))
                            {
                                return;
                            }
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                await _vehicleAdapterManager.SendCommandAsync(new DispatchCommand
                {
                    CommandType = DispatchCommandType.CompleteTask,
                    TaskId = taskId,
                    VehicleId = vehicleId,
                    TargetNodeId = task.TargetNodeId,
                    IssuedBy = nameof(MockTaskExecutionSimulator)
                }, cancellationToken);

                if (task.TaskType == "Charge")
                {
                    await SimulateChargingAsync(task, vehicleId, cancellationToken);
                }

                var completion = await _dispatchOrchestrationService.CompleteTaskAsync(
                    new CompleteDispatchTaskRequest
                    {
                        Context = context,
                        TaskId = taskId,
                        VehicleId = vehicleId,
                        CurrentNodeId = task.TargetNodeId
                    },
                    cancellationToken);
                if (!completion.Success)
                {
                    _auditTrail.Record(new OperationLog
                    {
                        Category = "Task",
                        Action = "SimulationCompletionFailed",
                        Message = $"Task {taskId} reached its target but completion cleanup failed: {completion.Message}",
                        TaskId = taskId,
                        VehicleId = vehicleId,
                        Operator = nameof(MockTaskExecutionSimulator)
                    });
                    return;
                }

                _auditTrail.Record(new OperationLog
                {
                    Category = "Task",
                    Action = "SimulationCompleted",
                    Message = $"Task {taskId} completed by {vehicleId}.",
                    TaskId = taskId,
                    VehicleId = vehicleId,
                    Operator = "MockTaskExecutionSimulator"
                });
            }
            catch (OperationCanceledException)
            {
                _auditTrail.Record(new OperationLog
                {
                    Category = "Task",
                    Action = "SimulationCancelled",
                    Message = $"Task {taskId} execution simulation cancelled.",
                    TaskId = taskId,
                    VehicleId = vehicleId,
                    Operator = "MockTaskExecutionSimulator"
                });
            }
            finally
            {
                if (_runningTasks.TryRemove(taskId, out var cancellation))
                {
                    cancellation.Dispose();
                }
            }
        }

        private async Task<List<string>> ResolveRouteAsync(
            TaskOrder task,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            var execution = await _dispatchOrchestrationService.GetExecutionAsync(
                new GetDispatchExecutionRequest
                {
                    Context = context,
                    TaskId = task.TaskId
                },
                cancellationToken);
            if (execution.Success && !string.IsNullOrWhiteSpace(execution.Data?.ReservationId))
            {
                var reservation = await _routeReservationService.GetReservationAsync(
                    new GetRouteReservationRequest
                    {
                        Context = context,
                        ReservationId = execution.Data.ReservationId
                    },
                    cancellationToken);
                var segments = reservation.Data?.Segments
                    .OrderBy(item => item.Segment.Sequence)
                    .Select(item => item.Segment)
                    .ToArray();
                if (segments is { Length: > 0 })
                {
                    var plannedRoute = new List<string> { segments[0].FromNodeId };
                    plannedRoute.AddRange(segments.Select(segment => segment.ToNodeId));
                    return plannedRoute;
                }
            }

            // A source-to-target fallback keeps the simulator usable for legacy dispatches that
            // do not have an orchestration execution or route reservation.
            var route = new List<string> { task.SourceNodeId };

            if (!route.Any(nodeId => string.Equals(nodeId, task.TargetNodeId, StringComparison.OrdinalIgnoreCase)))
            {
                route.Add(task.TargetNodeId);
            }

            return route;
        }

        private async Task<bool> WaitForTrafficAsync(
            string taskId,
            string vehicleId,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            const int maxRetryCount = 30;
            for (var retry = 1; retry <= maxRetryCount; retry++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                var retryResult = await _dispatchOrchestrationService.RetryWaitingTaskAsync(
                    new RetryWaitingDispatchRequest
                    {
                        Context = context,
                        TaskId = taskId,
                        SendVehicleCommand = true
                    },
                    cancellationToken);
                if (!retryResult.Success || retryResult.Data is null)
                {
                    RecordSimulationStop(
                        "SimulationAdvanceFailed",
                        $"Task {taskId} traffic retry {retry} failed: {retryResult.Message}",
                        taskId,
                        vehicleId);
                    return false;
                }

                if (retryResult.Data.RequiresReplan)
                {
                    RecordSimulationStop(
                        "SimulationReplanRequired",
                        $"Task {taskId} requires replanning while waiting for traffic.",
                        taskId,
                        vehicleId);
                    return false;
                }

                if (retryResult.Data.FirstWindowLocked)
                {
                    return true;
                }

                if (!retryResult.Data.ShouldWait)
                {
                    RecordSimulationStop(
                        "SimulationAdvanceFailed",
                        $"Task {taskId} traffic retry ended without acquiring the next window.",
                        taskId,
                        vehicleId);
                    return false;
                }
            }

            RecordSimulationStop(
                "SimulationWaitingTimeout",
                $"Task {taskId} was still waiting for traffic after {maxRetryCount} retries.",
                taskId,
                vehicleId);
            return false;
        }

        private void RecordSimulationStop(string action, string message, string taskId, string vehicleId)
        {
            _auditTrail.Record(new OperationLog
            {
                Category = "Task",
                Action = action,
                Message = message,
                TaskId = taskId,
                VehicleId = vehicleId,
                Operator = nameof(MockTaskExecutionSimulator)
            });
        }

        private async System.Threading.Tasks.Task SimulateChargingAsync(TaskOrder task, string vehicleId, CancellationToken cancellationToken)
        {
            if (!task.Attributes.TryGetValue("StationId", out var stationId))
            {
                return;
            }

            var station = await _chargeStationRepository.GetByIdAsync(stationId);
            if (station == null) return;

            station.State = ChargeStationState.Charging;
            station.BoundVehicleId = vehicleId;
            await _chargeStationRepository.SaveAsync(station);

            var sessionId = Guid.NewGuid().ToString("N");
            var record = new ChargeSessionRecord
            {
                SessionId = sessionId,
                StationId = stationId,
                VehicleId = vehicleId,
                StartTime = DateTime.Now,
                Status = "Charging",
                EnergyConsumedKwh = 0,
                EndBatteryLevel = 0
            };
            await _chargeStationRepository.AddSessionAsync(record);

            var vehicle = _vehicleStateStore.GetVehicle(vehicleId);
            if (vehicle != null)
            {
                record.StartBatteryLevel = vehicle.BatteryLevel;
                while (vehicle.BatteryLevel < 100)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await System.Threading.Tasks.Task.Delay(500, cancellationToken);
                    vehicle.BatteryLevel += 5;
                    if (vehicle.BatteryLevel > 100) vehicle.BatteryLevel = 100;
                    _vehicleStateStore.UpsertStatus(vehicle);
                }
                record.EndBatteryLevel = vehicle.BatteryLevel;
            }

            record.EndTime = DateTime.Now;
            record.Status = "Completed";
            record.EnergyConsumedKwh = (record.EndBatteryLevel - record.StartBatteryLevel) * 0.1; // Mock formula
            await _chargeStationRepository.UpdateSessionAsync(record);

            station.State = ChargeStationState.Available;
            station.BoundVehicleId = null;
            await _chargeStationRepository.SaveAsync(station);
        }
    }
}
