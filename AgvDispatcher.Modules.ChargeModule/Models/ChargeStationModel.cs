using AgvDispatcher.Core.Enums;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.ChargeModule.Models
{
    public class ChargeStationModel : BindableBase
    {
        public string Id { get; set; } = string.Empty;
        public RobotState State { get; set; }
        public string LinkedAgv { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
        public string ChargeMode { get; set; } = string.Empty;
        public string ChargePower { get; set; } = string.Empty;
        public string ChargedTime { get; set; } = string.Empty;
        public string EstimatedCompletion { get; set; } = string.Empty;
    }
}
