using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Okapi.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiDtoMapper
    {
        private readonly OkapiVehicleIdentityMapper _identityMapper;
        private readonly OkapiPointMapper _pointMapper;
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly OkapiProtocolLogger _logger;

        public OkapiDtoMapper(
            OkapiVehicleIdentityMapper identityMapper,
            OkapiPointMapper pointMapper,
            IVehicleStateStore vehicleStateStore,
            OkapiProtocolLogger logger)
        {
            _identityMapper = identityMapper;
            _pointMapper = pointMapper;
            _vehicleStateStore = vehicleStateStore;
            _logger = logger;
        }

        public async Task<VehicleStatusSnapshot?> ToVehicleStatusSnapshotAsync(OkapiAgvInfoDto dto, CancellationToken token = default)
        {
            var vehicleId = await _identityMapper.GetVehicleIdAsync(dto.AgvId, token);
            if (string.IsNullOrEmpty(vehicleId))
            {
                _logger.LogError(dto.AgvId.ToString(), "MapVehicle", $"Unknown Okapi agvId {dto.AgvId}", null);
                return null;
            }

            var current = _vehicleStateStore.GetVehicle(vehicleId);
            var brand = current?.Brand ?? "Okapi";

            RobotState mappedState = dto.State switch
            {
                0 => RobotState.Offline,
                1 => RobotState.Idle,
                2 => RobotState.Running,
                3 => RobotState.Fault,
                _ => RobotState.Offline
            };

            if (mappedState == RobotState.Offline && dto.State != 0)
            {
                _logger.LogError(vehicleId, "MapState", $"Unknown Okapi state {dto.State}", null);
            }

            var mappedNodeId = current?.Location ?? "Unassigned";
            if (dto.PointNo > 0)
            {
                mappedNodeId = await _pointMapper.OkapiPointToNodeIdAsync(dto.PointNo.ToString(), brand, token);
                if (string.IsNullOrEmpty(mappedNodeId))
                {
                    mappedNodeId = dto.PointNo.ToString();
                }
            }

            var hasAlarm = mappedState == RobotState.Fault || !string.IsNullOrWhiteSpace(dto.ErrorMsg);

            var telemetry = current?.Telemetry == null ? new Dictionary<string, string>() : new Dictionary<string, string>(current.Telemetry);
            telemetry["Vendor"] = "Okapi";
            telemetry["AgvId"] = dto.AgvId.ToString();
            telemetry["Type"] = dto.Type.ToString();
            telemetry["RawState"] = dto.State.ToString();
            telemetry["PointNo"] = dto.PointNo.ToString();
            telemetry["LineNo"] = dto.LineNo.ToString();
            telemetry["X"] = dto.X.ToString();
            telemetry["Y"] = dto.Y.ToString();
            telemetry["Angle"] = dto.Angle.ToString();
            telemetry["Loading"] = dto.Loading.ToString();
            telemetry["UpdateTime"] = dto.UpdateTime ?? "";

            return new VehicleStatusSnapshot
            {
                VehicleId = vehicleId,
                Brand = brand,
                State = mappedState,
                CurrentTaskId = string.IsNullOrWhiteSpace(dto.TaskId) ? null : dto.TaskId,
                BatteryLevel = Math.Clamp(dto.Electricity, 0, 100),
                Location = mappedNodeId,
                LoadState = dto.Loading ? VehicleLoadState.Loaded : VehicleLoadState.Empty,
                Position = new MapPosition
                {
                    X = dto.X,
                    Y = dto.Y,
                    Heading = dto.Angle,
                    NodeId = mappedNodeId
                },
                IsOnline = mappedState != RobotState.Offline,
                IsCharging = mappedNodeId.StartsWith("Charge", StringComparison.OrdinalIgnoreCase),
                HasAlarm = hasAlarm,
                ActiveAlarmCode = hasAlarm ? $"OKAPI_STATE_{dto.State}" : null,
                ActiveAlarmMessage = string.IsNullOrWhiteSpace(dto.ErrorMsg) ? null : dto.ErrorMsg,
                ReportedAt = ParseOkapiTime(dto.UpdateTime),
                Telemetry = telemetry
            };
        }

        public TaskState ConvertOkapiTaskState(int state, int faultCode)
        {
            if (faultCode == 1) return TaskState.Interrupted;
            if (faultCode == 2) return TaskState.Cancelled;

            switch (state)
            {
                case 0: return TaskState.Pending;
                case 1:
                case 2:
                case 3:
                case 4: return TaskState.Running;
                case 5: return TaskState.Completed;
                default:
                    _logger.LogError("System", "MapTaskState", $"Unknown Okapi task state {state}", null);
                    return TaskState.Pending;
            }
        }

        public int ConvertOkapiTaskProgress(int state)
        {
            return state switch
            {
                0 => 0,
                1 => 10,
                2 => 30,
                3 => 50,
                4 => 75,
                5 => 100,
                _ => 0
            };
        }

        public RobotState ConvertRobotStateFromTaskCallback(int state, int faultCode)
        {
            if (faultCode != 0) return RobotState.Fault;

            return state switch
            {
                1 or 2 or 3 or 4 => RobotState.Running,
                5 => RobotState.Idle,
                _ => RobotState.Idle
            };
        }

        public DateTime ParseOkapiTime(string? value)
        {
            if (DateTime.TryParse(value, out var t))
            {
                return t;
            }
            return DateTime.Now;
        }
    }
}
