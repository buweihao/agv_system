using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Okapi.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiTaskStateHandler
    {
        private readonly ITaskService _taskService;
        private readonly IVehicleStatusPublisher _statusPublisher;
        private readonly OkapiVehicleIdentityMapper _identityMapper;
        private readonly OkapiProtocolLogger _logger;
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly IAlarmService? _alarmService;

        public OkapiTaskStateHandler(
            ITaskService taskService,
            IVehicleStatusPublisher statusPublisher,
            OkapiVehicleIdentityMapper identityMapper,
            OkapiProtocolLogger logger,
            IVehicleStateStore vehicleStateStore,
            IAlarmService? alarmService = null)
        {
            _taskService = taskService;
            _statusPublisher = statusPublisher;
            _identityMapper = identityMapper;
            _logger = logger;
            _vehicleStateStore = vehicleStateStore;
            _alarmService = alarmService;
        }

        public async Task<VehicleStatusSnapshot?> HandleTaskStateAsync(ReturnTaskStateRequest request, CancellationToken token = default)
        {
            var vehicleId = await _identityMapper.GetVehicleIdAsync(request.AgvId, token);
            if (vehicleId == null)
            {
                _logger.LogError($"AgvId:{request.AgvId}", "HandleTaskState", $"Could not find VehicleId for AgvId {request.AgvId}. Ignoring callback.");
                return null;
            }

            var taskState = ConvertOkapiTaskState(request.State);
            var robotState = DetermineRobotState(request.State, request.FaultCode);

            try
            {
                if (!string.IsNullOrWhiteSpace(request.TaskId))
                {
                    _taskService.UpdateTaskState(request.TaskId, taskState, $"Updated by Okapi callback (state={request.State}, fault={request.FaultCode})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(vehicleId, "UpdateTaskState", $"Failed to update task state for {request.TaskId}: {ex.Message}");
            }

            if (request.FaultCode != 0 && _alarmService != null)
            {
                _alarmService.RaiseAlarm(new AlarmEvent
                {
                    AlarmId = Guid.NewGuid().ToString("N"),
                    VehicleId = vehicleId,
                    AlarmCode = request.FaultCode.ToString(),
                    Description = $"Okapi reported fault code {request.FaultCode}",
                    Severity = AlarmSeverity.Warning,
                    OccurredAt = DateTime.Now
                });
            }

            var current = _vehicleStateStore.GetVehicle(vehicleId);

            var snapshot = new VehicleStatusSnapshot
            {
                VehicleId = vehicleId,
                Brand = current?.Brand ?? "Okapi",
                State = robotState,
                CurrentTaskId = string.IsNullOrWhiteSpace(request.TaskId) ? null : request.TaskId,
                BatteryLevel = current?.BatteryLevel ?? 100,
                Location = current?.Location ?? "Unassigned",
                ReportedAt = DateTime.Now
            };

            _logger.LogReceive(vehicleId, "TaskStateUpdate", $"Status: {robotState}, Task: {request.TaskId}, Fault: {request.FaultCode}");
            
            return snapshot;
        }

        private TaskState ConvertOkapiTaskState(int state)
        {
            return state switch
            {
                0 => TaskState.Pending,
                1 => TaskState.Running,
                2 => TaskState.Completed,
                3 => TaskState.Failed,
                4 => TaskState.Cancelled,
                _ => TaskState.Pending
            };
        }

        private RobotState DetermineRobotState(int state, int faultCode)
        {
            if (faultCode != 0) return RobotState.Fault;
            if (state == 1) return RobotState.Running;
            return RobotState.Idle;
        }
    }
}
