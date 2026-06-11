using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public abstract class MockVehicleAdapterBase : IVehicleAdapter
    {
        protected readonly Vehicle _vehicle;
        protected readonly object _syncRoot = new();
        protected CancellationTokenSource? _runCancellation;
        protected Task? _runTask;
        protected double _batteryLevel;
        protected RobotState _state = RobotState.Idle;
        protected string _location;
        protected string? _currentTaskId;

        protected MockVehicleAdapterBase(Vehicle vehicle)
        {
            _vehicle = vehicle;
            _batteryLevel = 70 + Math.Abs(vehicle.VehicleId.GetHashCode()) % 25;
            _location = string.IsNullOrWhiteSpace(vehicle.AreaCode) ? "A01-01" : $"{vehicle.AreaCode}01-01";
        }

        public string VehicleId => _vehicle.VehicleId;

        public string Brand => _vehicle.Brand;

        public event EventHandler<VehicleStatusSnapshot>? StatusReceived;

        protected abstract double BatteryDrainPerTick { get; }
        protected abstract TimeSpan TickInterval { get; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_runTask is not null)
            {
                return Task.CompletedTask;
            }

            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            PublishCurrentStatus();
            _runTask = RunStatusLoopAsync(_runCancellation.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_runCancellation is null || _runTask is null)
            {
                return;
            }

            await _runCancellation.CancelAsync();

            try
            {
                await _runTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _runCancellation.Dispose();
                _runCancellation = null;
                _runTask = null;
            }
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
                    case DispatchCommandType.MoveToNode:
                    case DispatchCommandType.ReturnHome:
                    case DispatchCommandType.GoCharge:
                        _state = RobotState.Running;
                        if (!string.IsNullOrWhiteSpace(command.TargetNodeId))
                        {
                            _location = command.TargetNodeId;
                        }
                        break;
                    case DispatchCommandType.AssignTask:
                        _state = RobotState.Running;
                        if (!string.IsNullOrWhiteSpace(command.SourceNodeId))
                        {
                            _location = command.SourceNodeId;
                        }
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
                    case DispatchCommandType.StopCharge:
                        _state = RobotState.Idle;
                        _currentTaskId = null;
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

        protected virtual async Task RunStatusLoopAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TickInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                lock (_syncRoot)
                {
                    if (string.Equals(_vehicle.AdapterType, "MockOffline", StringComparison.OrdinalIgnoreCase))
                    {
                        _state = RobotState.Offline;
                    }
                    else if (string.Equals(_vehicle.AdapterType, "MockFault", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_state != RobotState.Fault && Random.Shared.NextDouble() < 0.05)
                        {
                            _state = RobotState.Fault;
                        }
                    }

                    if (_state == RobotState.Running)
                    {
                        _batteryLevel = Math.Max(0, _batteryLevel - BatteryDrainPerTick);
                    }
                }

                PublishCurrentStatus();
            }
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
