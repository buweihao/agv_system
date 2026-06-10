using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    public class LeftPanelViewModel : BindableBase
    {
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly IAlarmService _alarmService;

        private int _totalAgvCount;
        private int _runningCount;
        private int _idleCount;
        private int _chargingCount;
        private int _taskCount;
        private int _faultCount;
        private int _criticalAlarmCount;
        private int _majorAlarmCount;
        private int _minorAlarmCount;
        private int _runningPercent;
        private int _idlePercent;
        private int _chargingPercent;
        private int _taskPercent;
        private int _faultPercent;
        private int _speedBucket0;
        private int _speedBucket1;
        private int _speedBucket2;
        private int _speedBucket3;
        private int _speedBucket4;

        public int TotalAgvCount
        {
            get => _totalAgvCount;
            set => SetProperty(ref _totalAgvCount, value);
        }

        public int RunningCount
        {
            get => _runningCount;
            set => SetProperty(ref _runningCount, value);
        }

        public int IdleCount
        {
            get => _idleCount;
            set => SetProperty(ref _idleCount, value);
        }

        public int ChargingCount
        {
            get => _chargingCount;
            set => SetProperty(ref _chargingCount, value);
        }

        public int TaskCount
        {
            get => _taskCount;
            set => SetProperty(ref _taskCount, value);
        }

        public int FaultCount
        {
            get => _faultCount;
            set => SetProperty(ref _faultCount, value);
        }

        public int CriticalAlarmCount
        {
            get => _criticalAlarmCount;
            set => SetProperty(ref _criticalAlarmCount, value);
        }

        public int MajorAlarmCount
        {
            get => _majorAlarmCount;
            set => SetProperty(ref _majorAlarmCount, value);
        }

        public int MinorAlarmCount
        {
            get => _minorAlarmCount;
            set => SetProperty(ref _minorAlarmCount, value);
        }

        public int RunningPercent
        {
            get => _runningPercent;
            set => SetProperty(ref _runningPercent, value);
        }

        public int IdlePercent
        {
            get => _idlePercent;
            set => SetProperty(ref _idlePercent, value);
        }

        public int ChargingPercent
        {
            get => _chargingPercent;
            set => SetProperty(ref _chargingPercent, value);
        }

        public int TaskPercent
        {
            get => _taskPercent;
            set => SetProperty(ref _taskPercent, value);
        }

        public int FaultPercent
        {
            get => _faultPercent;
            set => SetProperty(ref _faultPercent, value);
        }

        public int SpeedBucket0
        {
            get => _speedBucket0;
            set => SetProperty(ref _speedBucket0, value);
        }

        public int SpeedBucket1
        {
            get => _speedBucket1;
            set => SetProperty(ref _speedBucket1, value);
        }

        public int SpeedBucket2
        {
            get => _speedBucket2;
            set => SetProperty(ref _speedBucket2, value);
        }

        public int SpeedBucket3
        {
            get => _speedBucket3;
            set => SetProperty(ref _speedBucket3, value);
        }

        public int SpeedBucket4
        {
            get => _speedBucket4;
            set => SetProperty(ref _speedBucket4, value);
        }

        public LeftPanelViewModel(
            IEventAggregator eventAggregator,
            IVehicleStateStore vehicleStateStore,
            IAlarmService alarmService)
        {
            _vehicleStateStore = vehicleStateStore;
            _alarmService = alarmService;

            Refresh();
            eventAggregator.GetEvent<VehicleStateChangedEvent>().Subscribe(_ => Refresh(), ThreadOption.UIThread);
        }

        private void Refresh()
        {
            var snapshots = _vehicleStateStore.GetAllVehicles();
            var alarms = _alarmService.GetActiveAlarms();

            TotalAgvCount = snapshots.Count;
            RunningCount = snapshots.Count(item => item.State == RobotState.Running);
            IdleCount = snapshots.Count(item => item.State == RobotState.Idle);
            ChargingCount = snapshots.Count(item => item.Location.Contains("Charge", StringComparison.OrdinalIgnoreCase));
            TaskCount = snapshots.Count(item => !string.IsNullOrWhiteSpace(item.CurrentTaskId));
            FaultCount = snapshots.Count(item => item.State == RobotState.Fault);

            CriticalAlarmCount = alarms.Count(item => item.Severity == AlarmSeverity.Critical);
            MajorAlarmCount = alarms.Count(item => item.Severity is AlarmSeverity.Major or AlarmSeverity.Warning);
            MinorAlarmCount = alarms.Count(item => item.Severity == AlarmSeverity.Info);

            RunningPercent = ToPercent(RunningCount, TotalAgvCount);
            IdlePercent = ToPercent(IdleCount, TotalAgvCount);
            ChargingPercent = ToPercent(ChargingCount, TotalAgvCount);
            TaskPercent = ToPercent(TaskCount, TotalAgvCount);
            FaultPercent = ToPercent(FaultCount, TotalAgvCount);

            var speeds = snapshots.Select(GetEstimatedSpeed).ToArray();
            SpeedBucket0 = speeds.Count(item => item >= 0 && item < 0.5);
            SpeedBucket1 = speeds.Count(item => item >= 0.5 && item < 1.0);
            SpeedBucket2 = speeds.Count(item => item >= 1.0 && item < 1.5);
            SpeedBucket3 = speeds.Count(item => item >= 1.5 && item < 2.0);
            SpeedBucket4 = speeds.Count(item => item >= 2.0);
        }

        private static int ToPercent(int value, int total)
        {
            return total == 0 ? 0 : (int)Math.Round(value * 100.0 / total);
        }

        private static double GetEstimatedSpeed(VehicleStatusSnapshot snapshot)
        {
            if (snapshot.State != RobotState.Running)
            {
                return 0;
            }

            return snapshot.VehicleId switch
            {
                "AGV-002" => 1.20,
                "AGV-003" => 1.10,
                "AGV-010" => 1.48,
                _ => 1.00
            };
        }
    }
}
