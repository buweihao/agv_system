using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.ChargeModule.Models;

namespace AgvDispatcher.Modules.ChargeModule.Services
{
    public class ChargeStationRecommendationService
    {
        private readonly IChargeService _chargeService;

        public ObservableCollection<ChargeStationModel> Stations { get; }

        public ChargeStationRecommendationService(IChargeService chargeService)
        {
            _chargeService = chargeService;
            Stations = new ObservableCollection<ChargeStationModel>(
                _chargeService.GetStations().Select(ToChargeStationModel));
        }

        public ChargeStationModel? RecommendAvailableStation()
        {
            return Stations.FirstOrDefault(station => station.State == RobotState.Idle && station.LinkedAgv == "-");
        }

        private static ChargeStationModel ToChargeStationModel(ChargeStation station)
        {
            return new ChargeStationModel
            {
                Id = station.StationId,
                State = ToRobotState(station.State),
                LinkedAgv = string.IsNullOrWhiteSpace(station.BoundVehicleId) ? "-" : station.BoundVehicleId,
                BatteryLevel = station.BoundVehicleId is null ? 0 : station.State == ChargeStationState.Charging ? 45 : 12,
                ChargeMode = station.RatedPowerKw >= 20 ? "Fast" : station.RatedPowerKw > 0 ? "Slow" : "-",
                ChargePower = station.RatedPowerKw > 0 ? $"{station.RatedPowerKw:0}kW" : "-",
                ChargedTime = station.State == ChargeStationState.Charging ? "00:15:30" : "00:00:00",
                EstimatedCompletion = station.State == ChargeStationState.Charging ? "00:20:00" : station.State == ChargeStationState.Occupied ? "01:30:00" : "-"
            };
        }

        private static RobotState ToRobotState(ChargeStationState state)
        {
            return state switch
            {
                ChargeStationState.Available => RobotState.Idle,
                ChargeStationState.Occupied => RobotState.Idle,
                ChargeStationState.Charging => RobotState.Running,
                ChargeStationState.Fault => RobotState.Fault,
                ChargeStationState.Offline => RobotState.Offline,
                ChargeStationState.Disabled => RobotState.Offline,
                ChargeStationState.Reserved => RobotState.Idle,
                _ => RobotState.Offline
            };
        }
    }
}
