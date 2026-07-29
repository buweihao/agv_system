using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Rules;

namespace AgvDispatcher.Modules.MonitoringModule.Models
{
    public class RobotModel
    {
        public string Id { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public RobotState State { get; set; }
        public string TaskId { get; set; } = string.Empty;
        public string CurrentPosition { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
        public double Speed { get; set; }
        public string RunningTime { get; set; } = string.Empty;
        public bool IsLowBattery => VehicleStatusRules.IsLowBattery(BatteryLevel);
        public string BatteryStatusText => IsLowBattery ? "LOW" : "OK";
    }
}
