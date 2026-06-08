using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Modules.ChargeModule.Models;

namespace AgvDispatcher.Modules.ChargeModule.Services
{
    public class ChargeStationRecommendationService
    {
        public ObservableCollection<ChargeStationModel> Stations { get; } = new()
        {
            new ChargeStationModel { Id = "C-01", State = RobotState.Running, LinkedAgv = "AGV-002", BatteryLevel = 45, ChargeMode = "Fast", ChargePower = "30kW", ChargedTime = "00:15:30", EstimatedCompletion = "00:20:00" },
            new ChargeStationModel { Id = "C-02", State = RobotState.Idle, LinkedAgv = "AGV-015", BatteryLevel = 12, ChargeMode = "Slow", ChargePower = "10kW", ChargedTime = "00:00:00", EstimatedCompletion = "01:30:00" },
            new ChargeStationModel { Id = "C-03", State = RobotState.Idle, LinkedAgv = "-", BatteryLevel = 0, ChargeMode = "-", ChargePower = "-", ChargedTime = "-", EstimatedCompletion = "-" },
            new ChargeStationModel { Id = "C-04", State = RobotState.Fault, LinkedAgv = "-", BatteryLevel = 0, ChargeMode = "-", ChargePower = "-", ChargedTime = "-", EstimatedCompletion = "-" },
            new ChargeStationModel { Id = "C-05", State = RobotState.Offline, LinkedAgv = "-", BatteryLevel = 0, ChargeMode = "-", ChargePower = "-", ChargedTime = "-", EstimatedCompletion = "-" },
        };

        public ChargeStationModel? RecommendAvailableStation()
        {
            return Stations.FirstOrDefault(station =>
                station.State == RobotState.Idle &&
                string.Equals(station.LinkedAgv, "-", StringComparison.Ordinal));
        }
    }
}
