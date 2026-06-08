using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Modules.MonitoringModule.Models
{
    public class RobotModel
    {
        public string Id { get; set; }
        public RobotState State { get; set; }
        public string TaskId { get; set; }
        public string CurrentPosition { get; set; }
        public string TargetPosition { get; set; }
        public int BatteryLevel { get; set; }
        public double Speed { get; set; }
        public string RunningTime { get; set; }
    }
}
