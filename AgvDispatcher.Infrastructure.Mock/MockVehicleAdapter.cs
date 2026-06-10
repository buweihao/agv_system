using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockVehicleAdapter : IVehicleAdapter
    {
        private readonly Vehicle _vehicle;
        private readonly object _syncRoot = new();
        private CancellationTokenSource? _runCancellation;
        private Task? _runTask;
        private double _batteryLevel;
        private RobotState _state = RobotState.Idle;
        private string _location;
        private string? _currentTaskId;

        public MockVehicleAdapter(Vehicle vehicle)
        {
            _vehicle = vehicle;
            _batteryLevel = 70 + Math.Abs(vehicle.VehicleId.GetHashCode()) % 25;
            _location = string.IsNullOrWhiteSpace(vehicle.AreaCode) ? "A01-01" : $"{vehicle.AreaCode}01-01";
        }

        public string VehicleId => _vehicle.VehicleId;

        public string Brand => _vehicle.Brand;

        public event EventHandler<VehicleStatusSnapshot>? StatusReceived;

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

        public Task SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            lock (_syncRoot)
            {
                _currentTaskId = string.IsNullOrWhiteSpace(command.TaskId) ? _currentTaskId : command.TaskId;

                switch (command.CommandType)
                {
                    case DispatchCommandType.Pause:
                        _state = RobotState.Idle;
                        break;
                    case DispatchCommandType.Resume:
                    case DispatchCommandType.AssignTask:
                    case DispatchCommandType.MoveToNode:
                    case DispatchCommandType.ReturnHome:
                    case DispatchCommandType.GoCharge:
                        _state = RobotState.Running;
                        if (!string.IsNullOrWhiteSpace(command.TargetNodeId))
                        {
                            _location = command.TargetNodeId;
                        }
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

        public VehicleStatusSnapshot ConvertStatus(object rawStatus)
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

        private async Task RunStatusLoopAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                lock (_syncRoot)
                {
                    if (_state == RobotState.Running)
                    {
                        _batteryLevel = Math.Max(0, _batteryLevel - 0.2);
                    }
                }

                PublishCurrentStatus();
            }
        }

        private void PublishCurrentStatus()
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

        private VehicleStatusSnapshot Normalize(VehicleStatusSnapshot snapshot)
        {
            snapshot.VehicleId = string.IsNullOrWhiteSpace(snapshot.VehicleId) ? VehicleId : snapshot.VehicleId;
            snapshot.Brand = string.IsNullOrWhiteSpace(snapshot.Brand) ? Brand : snapshot.Brand;
            snapshot.ReportedAt = snapshot.ReportedAt == default ? DateTime.Now : snapshot.ReportedAt;
            return snapshot;
        }
    }
}
