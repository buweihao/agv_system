using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class AdapterDispatchService : IDispatchService
    {
        private const double MinimumDispatchBatteryPercent = 30;

        private readonly IVehicleAdapterManager _vehicleAdapterManager;
        private readonly IVehicleService _vehicleService;
        private readonly ITaskService _taskService;
        private readonly IMapService _mapService;
        private readonly IAuditTrailService _auditTrail;
        private readonly ITaskExecutionSimulator _taskExecutionSimulator;
        private readonly IDispatchScoringService _scoringService;

        public AdapterDispatchService(
            IVehicleAdapterManager vehicleAdapterManager,
            IVehicleService vehicleService,
            ITaskService taskService,
            IMapService mapService,
            IAuditTrailService auditTrail,
            ITaskExecutionSimulator taskExecutionSimulator,
            IDispatchScoringService scoringService)
        {
            _vehicleAdapterManager = vehicleAdapterManager;
            _vehicleService = vehicleService;
            _taskService = taskService;
            _mapService = mapService;
            _auditTrail = auditTrail;
            _taskExecutionSimulator = taskExecutionSimulator;
            _scoringService = scoringService;
        }

        public DispatchResult AssignTask(string taskId, string? preferredVehicleId = null)
        {
            return Task.Run(() => AssignTaskAsync(taskId, preferredVehicleId, CancellationToken.None)).GetAwaiter().GetResult();
        }

        public async Task<DispatchResult> AssignTaskAsync(string taskId, string? preferredVehicleId = null, CancellationToken cancellationToken = default)
        {
            var task = _taskService.GetTask(taskId);
            if (task is null)
            {
                return AuditAndReturn(DispatchResult.Failure("TaskNotFound", $"Task {taskId} was not found.", taskId), "AssignTask");
            }

            if (task.State != TaskState.Pending)
            {
                return AuditAndReturn(DispatchResult.Failure("TaskNotPending", $"Task {taskId} is {task.State} and cannot be dispatched.", taskId, task.AssignedVehicleId), "AssignTask");
            }

            if (!TaskRouteIsKnown(task))
            {
                return AuditAndReturn(DispatchResult.Failure("RouteUnavailable", $"Task {taskId} route is not available.", taskId, task.AssignedVehicleId), "AssignTask");
            }

            string? selectedVehicleId;
            string? failReason = null;
            DispatchScoringResult? scoreResult = null;

            if (!string.IsNullOrWhiteSpace(preferredVehicleId))
            {
                selectedVehicleId = preferredVehicleId.Trim();
                var selectedStatus = _vehicleService.GetVehicleStatus(selectedVehicleId);
                var vehicle = _vehicleService.GetVehicle(selectedVehicleId);

                if (selectedStatus == null || vehicle == null)
                {
                    return AuditAndReturn(DispatchResult.Failure("VehicleNotFound", $"Vehicle {selectedVehicleId} not found.", taskId, selectedVehicleId), "AssignTask");
                }

                // Call ScoreAndSelectVehicle just to check constraints, but forcing the only candidate
                var availableList = new List<(Vehicle, VehicleStatus)> { (vehicle, selectedStatus) };
                scoreResult = _scoringService.ScoreAndSelectVehicle(task, availableList);
                if (!scoreResult.Success)
                {
                    return AuditAndReturn(DispatchResult.Failure("VehicleNotAvailable", $"Preferred vehicle {selectedVehicleId} rejected: {scoreResult.Reason}", taskId, selectedVehicleId), "AssignTask");
                }
            }
            else
            {
                var availableList = GetAvailableVehicles();
                scoreResult = _scoringService.ScoreAndSelectVehicle(task, availableList);
                selectedVehicleId = scoreResult.SelectedVehicleId;
                failReason = scoreResult.Reason;
            }

            if (string.IsNullOrWhiteSpace(selectedVehicleId))
            {
                var failLog = new OperationLog
                {
                    Category = "DispatchScore",
                    Action = "Rejected",
                    Message = $"No vehicle available. Candidates: {scoreResult.Candidates.Count}, Rejections: {scoreResult.Rejections.Count}",
                    TaskId = taskId,
                    Operator = "System"
                };
                failLog.Metadata["ScoreData"] = System.Text.Json.JsonSerializer.Serialize(scoreResult);
                _auditTrail.Record(failLog);
                return AuditAndReturn(DispatchResult.Failure("NoAvailableVehicle", $"No vehicle available. Reason: {failReason}", taskId), "AssignTask");
            }

            var successLog = new OperationLog
            {
                Category = "DispatchScore",
                Action = "Selected",
                Message = $"Vehicle {selectedVehicleId} selected. Score: {scoreResult.Candidates.FirstOrDefault(c => c.VehicleId == selectedVehicleId)?.TotalScore:0.##}",
                TaskId = taskId,
                VehicleId = selectedVehicleId,
                Operator = "System"
            };
            successLog.Metadata["ScoreData"] = System.Text.Json.JsonSerializer.Serialize(scoreResult);
            _auditTrail.Record(successLog);

            var result = await SendCommandAsync(new DispatchCommand
            {
                CommandType = DispatchCommandType.AssignTask,
                TaskId = taskId,
                VehicleId = selectedVehicleId,
                SourceNodeId = task.SourceNodeId,
                TargetNodeId = task.TargetNodeId,
                Priority = (int)task.Priority,
                IssuedBy = "DispatchService"
            }, cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return AuditAndReturn(result, "AssignTask");
            }

            _taskService.AssignVehicle(taskId, selectedVehicleId);
            _taskService.UpdateTaskState(taskId, TaskState.Running);

            var assignedVehicle = _vehicleService.GetVehicle(selectedVehicleId);
            if (assignedVehicle != null && assignedVehicle.AdapterType.StartsWith("Mock", StringComparison.OrdinalIgnoreCase))
            {
                _taskExecutionSimulator.Start(task, selectedVehicleId);
            }

            return AuditAndReturn(DispatchResult.Success($"Task {taskId} dispatched to {selectedVehicleId}.", taskId, selectedVehicleId, result.CommandId), "AssignTask");
        }

        public DispatchResult SendCommand(DispatchCommand command)
        {
            return Task.Run(() => SendCommandAsync(command, CancellationToken.None)).GetAwaiter().GetResult();
        }

        public async Task<DispatchResult> SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken = default)
        {
            var result = await _vehicleAdapterManager.SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
            return AuditAndReturn(result, command.CommandType.ToString());
        }

        public DispatchResult PauseVehicle(string vehicleId)
        {
            return SendCommand(new DispatchCommand
            {
                CommandType = DispatchCommandType.Pause,
                VehicleId = vehicleId,
                IssuedBy = "DispatchService"
            });
        }

        public DispatchResult ResumeVehicle(string vehicleId)
        {
            return SendCommand(new DispatchCommand
            {
                CommandType = DispatchCommandType.Resume,
                VehicleId = vehicleId,
                IssuedBy = "DispatchService"
            });
        }

        public DispatchResult CancelTask(string taskId, string? reason = null)
        {
            var task = _taskService.GetTask(taskId);
            if (task is null)
            {
                return AuditAndReturn(DispatchResult.Failure("TaskNotFound", $"Task {taskId} was not found.", taskId), "CancelTask");
            }

            if (!string.IsNullOrWhiteSpace(task.AssignedVehicleId))
            {
                var result = SendCommand(new DispatchCommand
                {
                    CommandType = DispatchCommandType.CancelTask,
                    TaskId = taskId,
                    VehicleId = task.AssignedVehicleId,
                    IssuedBy = "DispatchService"
                });

                if (!result.Succeeded)
                {
                    return AuditAndReturn(result, "CancelTask");
                }
            }

            _taskExecutionSimulator.Cancel(taskId);
            _taskService.CancelTask(taskId, reason);
            return AuditAndReturn(DispatchResult.Success($"Task {taskId} cancelled.", taskId, task.AssignedVehicleId), "CancelTask");
        }

        private DispatchResult AuditAndReturn(DispatchResult result, string action)
        {
            _auditTrail.RecordDispatchResult(result, action);
            return result;
        }

        private IEnumerable<(Vehicle, VehicleStatus)> GetAvailableVehicles()
        {
            var vehicles = _vehicleService.GetVehicles();
            var statuses = _vehicleService.GetVehicleStatuses();

            foreach (var status in statuses)
            {
                var v = vehicles.FirstOrDefault(x => x.VehicleId == status.VehicleId);
                if (v != null)
                {
                    yield return (v, status);
                }
            }
        }

        private bool TaskRouteIsKnown(TaskOrder task)
        {
            if (string.IsNullOrWhiteSpace(task.SourceNodeId) || string.IsNullOrWhiteSpace(task.TargetNodeId))
            {
                return false;
            }

            return _mapService.IsPathAvailable(task.SourceNodeId, task.TargetNodeId);
        }
    }
}
