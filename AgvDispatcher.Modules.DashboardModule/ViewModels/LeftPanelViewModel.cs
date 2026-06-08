using Prism.Mvvm;

namespace AgvDispatcher.Modules.DashboardModule.ViewModels
{
    public class LeftPanelViewModel : BindableBase
    {
        public int TotalAgvCount { get; } = 60;
        public int RunningCount { get; } = 18;
        public int IdleCount { get; } = 12;
        public int ChargingCount { get; } = 6;
        public int TaskCount { get; } = 22;
        public int FaultCount { get; } = 2;

        public int CriticalAlarmCount { get; } = 1;
        public int MajorAlarmCount { get; } = 3;
        public int MinorAlarmCount { get; } = 8;
    }
}
