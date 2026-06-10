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

        public AdapterDispatchService(
            IVehicleAdapterManager vehicleAdapterManager,
            IVehicleService vehicleService,
            ITaskService taskService,
            IMapService mapService)
        {
            _vehicleAdapterManager = vehicleAdapterManager;
            _vehicleService = vehicleService;
            _taskService = taskService;
            _mapService = mapService;
        }

        public DispatchResult AssignTask(string taskId, string? preferredVehicleId = null)
        {
            var task = _taskService.GetTask(taskId);
            if (task is null)
            {
                return DispatchResult.Failure("TaskNotFound", $"Task {taskId} was not found.", taskId);
            }

            if (task.State != TaskState.Pending)
            {
                return DispatchResult.Failure("TaskNotPending", $"Task {taskId} is {task.State} and cannot be dispatched.", taskId, task.AssignedVehicleId);
            }

            if (!TaskRouteIsKnown(task))
            {
                return DispatchResult.Failure("RouteUnavailable", $"Task {taskId} route is not available.", taskId, task.AssignedVehicleId);
            }

            var selectedVehicleId = string.IsNullOrWhiteSpace(preferredVehicleId)
                ? SelectVehicle(task)?.VehicleId
                : preferredVehicleId.Trim();

            if (string.IsNullOrWhiteSpace(preferredVehicleId))
            {
                if (string.IsNullOrWhiteSpace(selectedVehicleId))
                {
                    return DispatchResult.Failure("NoAvailableVehicle", $"No idle online vehicle with battery >= {MinimumDispatchBatteryPercent:0.#}% is available.", taskId);
                }
            }

            var selectedStatus = _vehicleService.GetVehicleStatus(selectedVehicleId);
            if (!IsVehicleDispatchable(selectedStatus))
            {
                return DispatchResult.Failure("VehicleNotAvailable", $"Vehicle {selectedVehicleId} is not available for dispatch.", taskId, selectedVehicleId);
            }

            var result = SendCommand(new DispatchCommand
            {
                CommandType = DispatchCommandType.AssignTask,
                TaskId = taskId,
                VehicleId = selectedVehicleId,
                SourceNodeId = task.SourceNodeId,
                TargetNodeId = task.TargetNodeId,
                Priority = (int)task.Priority,
                IssuedBy = "DispatchService"
            });

            if (!result.Succeeded)
            {
                return result;
            }

            _taskService.AssignVehicle(taskId, selectedVehicleId);
            _taskService.UpdateTaskState(taskId, TaskState.Running);
            return DispatchResult.Success($"Task {taskId} dispatched to {selectedVehicleId}.", taskId, selectedVehicleId, result.CommandId);
        }

        public DispatchResult SendCommand(DispatchCommand command)
        {
            return _vehicleAdapterManager.SendCommandAsync(command, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
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
                return DispatchResult.Failure("TaskNotFound", $"Task {taskId} was not found.", taskId);
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
                    return result;
                }
            }

            _taskService.CancelTask(taskId, reason);
            return DispatchResult.Success($"Task {taskId} cancelled.", taskId, task.AssignedVehicleId);
        }

        private VehicleStatus? SelectVehicle(TaskOrder task)
        {
            return _vehicleService.GetVehicleStatuses()
                .Where(IsVehicleDispatchable)
                .OrderByDescending(status => status.BatteryLevel)
                .ThenBy(status => status.VehicleId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static bool IsVehicleDispatchable(VehicleStatus? status)
        {
            return status is not null
                && status.State == RobotState.Idle
                && status.IsOnline
                && !status.HasAlarm
                && status.BatteryLevel >= MinimumDispatchBatteryPercent;
        }

        private bool TaskRouteIsKnown(TaskOrder task)
        {
            if (string.IsNullOrWhiteSpace(task.SourceNodeId) || string.IsNullOrWhiteSpace(task.TargetNodeId))
            {
                return false;
            }

            return _mapService.GetNode(task.SourceNodeId) is not null
                && _mapService.GetNode(task.TargetNodeId) is not null;
        }
    }
}
