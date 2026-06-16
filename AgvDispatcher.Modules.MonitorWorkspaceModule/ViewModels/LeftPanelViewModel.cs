using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    /// <summary>
    /// 运行监控页面"左侧统计面板"的 ViewModel。
    /// <para>
    /// 汇总当前所有 AGV 的整体运行态势：车辆总数、各状态数量与占比（运行/空闲/充电/任务中/故障）、
    /// 告警分级统计（紧急/重要/次要）以及速度区间分布。数据来源于车辆状态存储
    /// <see cref="IVehicleStateStore"/> 与告警服务 <see cref="IAlarmService"/>，
    /// 并订阅 <see cref="VehicleStateChangedEvent"/> 在车辆状态变化时实时刷新。
    /// </para>
    /// </summary>
    public class LeftPanelViewModel : BindableBase
    {
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly IAlarmService _alarmService;

        // ===== 以下为绑定字段：数量统计、占比(百分比)与速度区间桶计数 =====

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

        /// <summary>
        /// 构造函数，注入依赖并完成首次统计刷新，同时订阅车辆状态变化事件。
        /// </summary>
        /// <param name="eventAggregator">事件聚合器，用于订阅车辆状态变化。</param>
        /// <param name="vehicleStateStore">车辆状态存储，提供全量车辆快照。</param>
        /// <param name="alarmService">告警服务，提供当前活动告警。</param>
        public LeftPanelViewModel(
            IEventAggregator eventAggregator,
            IVehicleStateStore vehicleStateStore,
            IAlarmService alarmService)
        {
            _vehicleStateStore = vehicleStateStore;
            _alarmService = alarmService;

            Refresh();
            // 车辆状态变化时在 UI 线程重新统计，保证面板实时
            eventAggregator.GetEvent<VehicleStateChangedEvent>().Subscribe(_ => Refresh(), ThreadOption.UIThread);
        }

        /// <summary>
        /// 重新计算全部统计指标：状态计数、占比、告警分级与速度区间分布。
        /// </summary>
        private void Refresh()
        {
            var snapshots = _vehicleStateStore.GetAllVehicles();
            var alarms = _alarmService.GetActiveAlarms();

            // 各状态车辆计数（充电通过位置是否含 "Charge" 粗略判定，任务中通过是否有当前任务判定）
            TotalAgvCount = snapshots.Count;
            RunningCount = snapshots.Count(item => item.State == RobotState.Running);
            IdleCount = snapshots.Count(item => item.State == RobotState.Idle);
            ChargingCount = snapshots.Count(item => item.Location.Contains("Charge", StringComparison.OrdinalIgnoreCase));
            TaskCount = snapshots.Count(item => !string.IsNullOrWhiteSpace(item.CurrentTaskId));
            FaultCount = snapshots.Count(item => item.State == RobotState.Fault);

            // 告警按严重程度归并到三档
            CriticalAlarmCount = alarms.Count(item => item.Severity == AlarmSeverity.Critical);
            MajorAlarmCount = alarms.Count(item => item.Severity is AlarmSeverity.Major or AlarmSeverity.Warning);
            MinorAlarmCount = alarms.Count(item => item.Severity == AlarmSeverity.Info);

            // 各状态占总数的百分比
            RunningPercent = ToPercent(RunningCount, TotalAgvCount);
            IdlePercent = ToPercent(IdleCount, TotalAgvCount);
            ChargingPercent = ToPercent(ChargingCount, TotalAgvCount);
            TaskPercent = ToPercent(TaskCount, TotalAgvCount);
            FaultPercent = ToPercent(FaultCount, TotalAgvCount);

            // 速度区间分布（每 0.5 m/s 一档，2.0 以上归入最后一档）
            var speeds = snapshots.Select(GetEstimatedSpeed).ToArray();
            SpeedBucket0 = speeds.Count(item => item >= 0 && item < 0.5);
            SpeedBucket1 = speeds.Count(item => item >= 0.5 && item < 1.0);
            SpeedBucket2 = speeds.Count(item => item >= 1.0 && item < 1.5);
            SpeedBucket3 = speeds.Count(item => item >= 1.5 && item < 2.0);
            SpeedBucket4 = speeds.Count(item => item >= 2.0);
        }

        /// <summary>将数量换算为占总数的整数百分比；总数为 0 时返回 0。</summary>
        private static int ToPercent(int value, int total)
        {
            return total == 0 ? 0 : (int)Math.Round(value * 100.0 / total);
        }

        /// <summary>
        /// 估算单车速度（当前为演示用的占位映射）。
        /// 非运行状态恒为 0，运行状态按车辆 ID 返回预设速度，其余返回 1.0 m/s。
        /// </summary>
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
