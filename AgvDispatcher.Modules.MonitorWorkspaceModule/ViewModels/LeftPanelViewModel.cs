using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    public class LeftPanelViewModel : BindableBase
    {
        public int TotalAgvCount { get; }
        public int RunningCount { get; }
        public int IdleCount { get; }
        public int ChargingCount { get; }
        public int TaskCount { get; }
        public int FaultCount { get; }

        public int CriticalAlarmCount { get; }
        public int MajorAlarmCount { get; }
        public int MinorAlarmCount { get; }

        public LeftPanelViewModel(
            IVehicleService vehicleService,
            ITaskService taskService,
            IAlarmService alarmService)
        {
            var statuses = vehicleService.GetVehicleStatuses();
            var alarms = alarmService.GetActiveAlarms();

            TotalAgvCount = statuses.Count;
            RunningCount = statuses.Count(item => item.State == RobotState.Running);
            IdleCount = statuses.Count(item => item.State == RobotState.Idle);
            ChargingCount = statuses.Count(item => item.IsCharging || item.LocationText.Contains("Charge", StringComparison.OrdinalIgnoreCase));
            TaskCount = taskService.GetTasks().Count(item => item.State is TaskState.Pending or TaskState.Running);
            FaultCount = statuses.Count(item => item.State == RobotState.Fault);

            CriticalAlarmCount = alarms.Count(item => item.Severity == AlarmSeverity.Critical);
            MajorAlarmCount = alarms.Count(item => item.Severity == AlarmSeverity.Major || item.Severity == AlarmSeverity.Warning);
            MinorAlarmCount = alarms.Count(item => item.Severity == AlarmSeverity.Info);
        }
    }
}
