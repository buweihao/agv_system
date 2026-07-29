using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public abstract class MockVehicleAdapterBase : IVehicleAdapter
    {
        protected readonly Vehicle _vehicle;
        protected readonly object _syncRoot = new();
        private bool _started;
        protected double _batteryLevel;
        protected RobotState _state = RobotState.Idle;
        protected string _location;
        protected string? _currentTaskId;
        protected readonly IChargeStationRepository? _chargeRepo;
        protected ChargeSessionRecord? _currentChargeSession;
        protected bool _isCharging;

        protected MockVehicleAdapterBase(Vehicle vehicle, IChargeStationRepository? chargeRepo = null)
        {
            _vehicle = vehicle;
            _chargeRepo = chargeRepo;
            _batteryLevel = 70 + Math.Abs(vehicle.VehicleId.GetHashCode()) % 25;
            _state = vehicle.AdapterType?.ToUpperInvariant() switch
            {
                "MOCKOFFLINE" => RobotState.Offline,
                "MOCKFAULT" => RobotState.Fault,
                _ => RobotState.Idle
            };
            _location = !string.IsNullOrWhiteSpace(vehicle.HomeNodeId)
                ? vehicle.HomeNodeId
                : string.IsNullOrWhiteSpace(vehicle.AreaCode) ? "Unassigned" : vehicle.AreaCode;
        }

        public string VehicleId => _vehicle.VehicleId;

        public string Brand => _vehicle.Brand;

        public event EventHandler<VehicleStatusSnapshot>? StatusReceived;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                if (_started)
                {
                    return Task.CompletedTask;
                }

                _started = true;
            }

            // Mock 状态只在启动和明确命令时发布。周期发布会用适配器内部旧值
            // 覆盖调试面板、真实回调或其他业务模块刚写入的车辆状态。
            PublishCurrentStatus();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            lock (_syncRoot)
            {
                _started = false;
            }

            return Task.CompletedTask;
        }

        public virtual Task SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            // 1. Check SupportedCommandFlags
            if (_vehicle.SupportedCommandFlags != VehicleCommandCapability.None)
            {
                var reqCap = MapCommandToCapability(command.CommandType);
                if (reqCap != VehicleCommandCapability.None && (_vehicle.SupportedCommandFlags & reqCap) != reqCap)
                {
                    return Task.FromException(new NotSupportedException($"Command {command.CommandType} is not supported by vehicle {VehicleId} (Requires {reqCap})"));
                }
            }

            // 2. MockUnstable: Randomly drop commands
            if (string.Equals(_vehicle.AdapterType, "MockUnstable", StringComparison.OrdinalIgnoreCase))
            {
                if (Random.Shared.NextDouble() < 0.2)
                {
                    return Task.FromException(new Exception("MockUnstable: Network timeout or command dropped."));
                }
            }

            lock (_syncRoot)
            {
                _currentTaskId = string.IsNullOrWhiteSpace(command.TaskId) ? _currentTaskId : command.TaskId;

                switch (command.CommandType)
                {
                    case DispatchCommandType.Pause:
                        _state = RobotState.Idle;
                        break;
                    case DispatchCommandType.Resume:
                        _state = _currentTaskId != null ? RobotState.Running : RobotState.Idle;
                        break;
                    case DispatchCommandType.MoveToNode:
                        _state = RobotState.Running;
                        if (!string.IsNullOrWhiteSpace(command.TargetNodeId))
                        {
                            _location = command.TargetNodeId;
                        }
                        break;
                    case DispatchCommandType.ReturnHome:
                        _state = RobotState.Running;
                        if (!string.IsNullOrWhiteSpace(command.TargetNodeId))
                        {
                            _location = command.TargetNodeId;
                        }
                        break;
                    case DispatchCommandType.GoCharge:
                        _state = RobotState.Idle; // usually stays idle while charging
                        _isCharging = true;
                        if (!string.IsNullOrWhiteSpace(command.TargetNodeId))
                        {
                            _location = command.TargetNodeId;
                        }
                        if (_chargeRepo != null)
                        {
                            _currentChargeSession = new ChargeSessionRecord
                            {
                                SessionId = Guid.NewGuid().ToString("N"),
                                StationId = command.TargetNodeId ?? "Unknown",
                                VehicleId = _vehicle.VehicleId,
                                StartTime = DateTime.Now,
                                StartBatteryLevel = _batteryLevel,
                                Status = "Charging"
                            };
                            _chargeRepo.AddSessionAsync(_currentChargeSession).GetAwaiter().GetResult();
                        }
                        break;
                    case DispatchCommandType.AssignTask:
                        _state = RobotState.Running;
                        break;
                    case DispatchCommandType.CompleteTask:
                        _state = RobotState.Idle;
                        if (!string.IsNullOrWhiteSpace(command.TargetNodeId))
                        {
                            _location = command.TargetNodeId;
                        }
                        _currentTaskId = null;
                        break;
                    case DispatchCommandType.CancelTask:
                        _state = RobotState.Idle;
                        _currentTaskId = null;
                        break;
                    case DispatchCommandType.StopCharge:
                        _state = RobotState.Idle;
                        _isCharging = false;
                        _currentTaskId = null;
                        if (_chargeRepo != null && _currentChargeSession != null)
                        {
                            _currentChargeSession.EndTime = DateTime.Now;
                            _currentChargeSession.EndBatteryLevel = _batteryLevel;
                            _currentChargeSession.Status = "Completed";
                            _chargeRepo.UpdateSessionAsync(_currentChargeSession).GetAwaiter().GetResult();
                            _currentChargeSession = null;
                        }
                        break;
                    case DispatchCommandType.EmergencyStop:
                        _state = RobotState.Fault;
                        break;
                    case DispatchCommandType.ResetFault:
                        _state = RobotState.Idle;
                        break;
                }
            }

            PublishCurrentStatus();
            return Task.CompletedTask;
        }

        private VehicleCommandCapability MapCommandToCapability(DispatchCommandType cmd)
        {
            return cmd switch
            {
                DispatchCommandType.AssignTask => VehicleCommandCapability.AssignTask,
                DispatchCommandType.MoveToNode => VehicleCommandCapability.MoveToNode,
                DispatchCommandType.Pause => VehicleCommandCapability.Pause,
                DispatchCommandType.Resume => VehicleCommandCapability.Resume,
                DispatchCommandType.CancelTask => VehicleCommandCapability.CancelTask,
                DispatchCommandType.EmergencyStop => VehicleCommandCapability.EmergencyStop,
                DispatchCommandType.ResetFault => VehicleCommandCapability.ResetFault,
                DispatchCommandType.GoCharge => VehicleCommandCapability.GoCharge,
                DispatchCommandType.StopCharge => VehicleCommandCapability.StopCharge,
                _ => VehicleCommandCapability.None
            };
        }

        public virtual VehicleStatusSnapshot ConvertStatus(object rawStatus)
        {
            return rawStatus switch
            {
                VehicleStatusSnapshot snapshot => Normalize(snapshot),
                VehicleStatus status => new VehicleStatusSnapshot
                {
                    VehicleId = status.VehicleId,
                    Brand = Brand,
                    BatteryLevel = status.BatteryLevel,
                    Location = status.LocationText,
                    State = status.State,
                    CurrentTaskId = status.CurrentTaskId,
                    ReportedAt = status.ReportedAt == default ? DateTime.Now : status.ReportedAt
                },
                _ => throw new ArgumentException("Unsupported mock vehicle status payload.", nameof(rawStatus))
            };
        }

        protected void PublishCurrentStatus()
        {
            VehicleStatusSnapshot snapshot;
            lock (_syncRoot)
            {
                snapshot = new VehicleStatusSnapshot
                {
                    VehicleId = VehicleId,
                    Brand = Brand,
                    BatteryLevel = Math.Round(_batteryLevel, 1),
                    Location = _location,
                    State = _state,
                    CurrentTaskId = _currentTaskId,
                    ReportedAt = DateTime.Now
                };
            }

            StatusReceived?.Invoke(this, snapshot);
        }

        protected VehicleStatusSnapshot Normalize(VehicleStatusSnapshot snapshot)
        {
            snapshot.VehicleId = string.IsNullOrWhiteSpace(snapshot.VehicleId) ? VehicleId : snapshot.VehicleId;
            snapshot.Brand = string.IsNullOrWhiteSpace(snapshot.Brand) ? Brand : snapshot.Brand;
            snapshot.ReportedAt = snapshot.ReportedAt == default ? DateTime.Now : snapshot.ReportedAt;
            return snapshot;
        }
    }
}
