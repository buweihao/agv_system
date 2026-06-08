using System.Collections.ObjectModel;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.DashboardModule.ViewModels
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
        public double TaskCompletionRate { get; } = 85.6;
        public int TodayCompletedTasks { get; } = 156;
        public double TodayDistance { get; } = 128.6;
        public double AvgSpeed { get; } = 1.36;

        public double HealthScore { get; } = 92.0;
        public int HealthyDeviceCount { get; } = 55;
        public int AbnormalDeviceCount { get; } = 3;
        public int OfflineDeviceCount { get; } = 2;

        public double Temperature { get; } = 24.5;
        public int Humidity { get; } = 45;
        public string SignalStrength { get; } = "优";

        public ObservableCollection<AlarmMessage> RealtimeAlarms { get; } = new ObservableCollection<AlarmMessage>
        {
            new AlarmMessage { Device = "AGV-008", Message = "激光避障异常", Time = "11:28:32", SeverityColor = "#FF4500" },
            new AlarmMessage { Device = "AGV-014", Message = "电量低于20%", Time = "11:27:15", SeverityColor = "#FFD700" },
            new AlarmMessage { Device = "AGV-002", Message = "通讯延迟过高", Time = "11:25:00", SeverityColor = "#FFD700" }
        };
    }
}
