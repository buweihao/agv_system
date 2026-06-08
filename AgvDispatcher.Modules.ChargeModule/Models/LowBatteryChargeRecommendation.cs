using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.ChargeModule.Models
{
    public class LowBatteryChargeRecommendation : BindableBase
    {
        public RobotBatteryAlert Alert { get; set; } = new();

        public ChargeStationModel? RecommendedStation { get; set; }

        public string VehicleId => Alert.VehicleId;

        public double BatteryLevel => Alert.BatteryLevel;

        public string Location => string.IsNullOrWhiteSpace(Alert.Location) ? "-" : Alert.Location;

        public string OccurredAtText => Alert.OccurredAt == default
            ? DateTime.Now.ToString("HH:mm:ss")
            : Alert.OccurredAt.ToString("HH:mm:ss");

        public string RecommendedStationText => RecommendedStation is null
            ? "No available station"
            : RecommendedStation.Id;
    }
}
