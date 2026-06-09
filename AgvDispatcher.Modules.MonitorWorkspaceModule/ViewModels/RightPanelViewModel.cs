using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    public class AlarmMessage
    {
        public string Time { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SeverityColor { get; set; } = "Red";
    }

    public class RightPanelViewModel : BindableBase
    {
        public double TaskCompletionRate { get; }
        public int TodayCompletedTasks { get; }
        public double TodayDistance { get; } = 128.6;
        public double AvgSpeed { get; }

        public double HealthScore { get; }
        public int HealthyDeviceCount { get; }
        public int AbnormalDeviceCount { get; }
        public int OfflineDeviceCount { get; }

        public double Temperature { get; } = 24.5;
        public int Humidity { get; } = 45;
        public string SignalStrength { get; } = "优";

        public ObservableCollection<AlarmMessage> RealtimeAlarms { get; }

        public RightPanelViewModel(
            IVehicleService vehicleService,
            ITaskService taskService,
            IAlarmService alarmService)
        {
            var statuses = vehicleService.GetVehicleStatuses();
            var tasks = taskService.GetTasks();
            var completedTasks = tasks.Count(item => item.State == TaskState.Completed);

            TodayCompletedTasks = completedTasks;
            TaskCompletionRate = tasks.Count == 0 ? 0 : Math.Round(completedTasks * 100.0 / tasks.Count, 1);
            AvgSpeed = statuses.Count == 0 ? 0 : Math.Round(statuses.Average(item => item.Speed), 2);

            HealthyDeviceCount = statuses.Count(item => (item.State is RobotState.Idle or RobotState.Running) && !item.HasAlarm);
            AbnormalDeviceCount = statuses.Count(item => item.State == RobotState.Fault || item.HasAlarm);
            OfflineDeviceCount = statuses.Count(item => item.State == RobotState.Offline || !item.IsOnline);
            HealthScore = statuses.Count == 0 ? 0 : Math.Round(HealthyDeviceCount * 100.0 / statuses.Count, 1);

            RealtimeAlarms = new ObservableCollection<AlarmMessage>(
                alarmService.GetActiveAlarms()
                    .OrderByDescending(item => item.OccurredAt)
                    .Take(5)
                    .Select(ToAlarmMessage));
        }

        private static AlarmMessage ToAlarmMessage(AlarmEvent alarm)
        {
            return new AlarmMessage
            {
                Device = alarm.VehicleId ?? alarm.SourceId,
                Message = string.IsNullOrWhiteSpace(alarm.Description) ? alarm.Name : alarm.Description,
                Time = alarm.OccurredAt.ToString("HH:mm:ss"),
                SeverityColor = alarm.Severity switch
                {
                    AlarmSeverity.Critical => "#FF4500",
                    AlarmSeverity.Major => "#FF8C00",
                    AlarmSeverity.Warning => "#FFD700",
                    _ => "#1E90FF"
                }
            };
        }
    }
}
