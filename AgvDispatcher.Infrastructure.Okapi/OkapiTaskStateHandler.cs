using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Okapi.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiTaskStateHandler
    {
        private readonly ITaskService _taskService;
        private readonly OkapiVehicleIdentityMapper _identityMapper;
        private readonly OkapiProtocolLogger _logger;
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly OkapiDtoMapper _mapper;

        public OkapiTaskStateHandler(
            ITaskService taskService,
            OkapiVehicleIdentityMapper identityMapper,
            OkapiProtocolLogger logger,
            IVehicleStateStore vehicleStateStore,
            OkapiDtoMapper mapper)
        {
            _taskService = taskService;
            _identityMapper = identityMapper;
            _logger = logger;
            _vehicleStateStore = vehicleStateStore;
            _mapper = mapper;
        }

        public async Task<VehicleStatusSnapshot?> HandleTaskStateAsync(ReturnTaskStateRequest request, CancellationToken token = default)
        {
            var vehicleId = await _identityMapper.GetVehicleIdAsync(request.AgvId, token);
            if (vehicleId == null)
            {
                _logger.LogError($"AgvId:{request.AgvId}", "HandleTaskState", $"Could not find VehicleId for AgvId {request.AgvId}. Ignoring callback.");
                return null;
            }

            var taskState = _mapper.ConvertOkapiTaskState(request.State, request.FaultCode);
            var progress = _mapper.ConvertOkapiTaskProgress(request.State);
            var robotState = _mapper.ConvertRobotStateFromTaskCallback(request.State, request.FaultCode);

            try
            {
                if (!string.IsNullOrWhiteSpace(request.TaskId))
                {
                    var task = _taskService.GetTask(request.TaskId);
                    if (task != null)
                    {
                        if (string.IsNullOrEmpty(task.AssignedVehicleId))
                        {
                            _taskService.AssignVehicle(request.TaskId, vehicleId);
                        }
                        _taskService.UpdateTaskState(request.TaskId, taskState, $"Updated by Okapi callback (state={request.State}, fault={request.FaultCode})");
                        _taskService.UpdateTaskProgress(request.TaskId, progress);
                    }
                    else
                    {
                        _logger.LogError(vehicleId, "UpdateTaskState", $"Task {request.TaskId} not found. Ignoring callback update.", request.TaskId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(vehicleId, "UpdateTaskState", $"Failed to update task state for {request.TaskId}: {ex.Message}");
            }

            var current = _vehicleStateStore.GetVehicle(vehicleId);

            string? activeAlarmCode = null;
            string? activeAlarmMessage = null;

            if (request.FaultCode == 1)
            {
                activeAlarmCode = "OKAPI_FAULT_1";
                activeAlarmMessage = "Okapi faultCode=1: AGV exception";
            }
            else if (request.FaultCode == 2)
            {
                activeAlarmCode = "OKAPI_TASK_DELETED";
                activeAlarmMessage = "Okapi faultCode=2: Task cancelled";
            }
            else if (request.FaultCode != 0)
            {
                activeAlarmCode = $"OKAPI_FAULT_{request.FaultCode}";
                activeAlarmMessage = $"Okapi faultCode={request.FaultCode}: Unknown fault";
            }

            var telemetry = current?.Telemetry == null ? new Dictionary<string, string>() : new Dictionary<string, string>(current.Telemetry);
            telemetry["Vendor"] = "Okapi";
            telemetry["AgvId"] = request.AgvId.ToString();
            telemetry["TaskCallbackState"] = request.State.ToString();
            telemetry["FaultCode"] = request.FaultCode.ToString();
            telemetry["TaskUpdateTime"] = request.UpdateTime ?? "";

            var snapshot = new VehicleStatusSnapshot
            {
                VehicleId = vehicleId,
                Brand = current?.Brand ?? "Okapi",
                State = robotState,
                CurrentTaskId = string.IsNullOrWhiteSpace(request.TaskId) ? null : request.TaskId,
                BatteryLevel = current?.BatteryLevel ?? 100,
                Location = current?.Location ?? "Unassigned",
                LoadState = current?.LoadState ?? VehicleLoadState.Unknown,
                Position = current?.Position ?? new MapPosition(),
                IsOnline = robotState != RobotState.Offline,
                HasAlarm = request.FaultCode != 0,
                ActiveAlarmCode = activeAlarmCode,
                ActiveAlarmMessage = activeAlarmMessage,
                ReportedAt = _mapper.ParseOkapiTime(request.UpdateTime),
                Telemetry = telemetry
            };

            _logger.LogReceive(vehicleId, "TaskStateUpdate", $"Status: {robotState}, Task: {request.TaskId}, Fault: {request.FaultCode}");
            
            return snapshot;
        }
    }
}
