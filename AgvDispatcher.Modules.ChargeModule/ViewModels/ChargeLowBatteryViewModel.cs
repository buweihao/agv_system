using System.Collections.ObjectModel;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.ChargeModule.Models;
using AgvDispatcher.Modules.ChargeModule.Services;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.ChargeModule.ViewModels
{
    public class ChargeLowBatteryViewModel : BindableBase
    {
        private readonly ChargeStationRecommendationService _stationService;

        public ObservableCollection<LowBatteryChargeRecommendation> LowBatteryAgvs { get; } = new();

        public ChargeLowBatteryViewModel(
            IEventAggregator eventAggregator,
            ChargeStationRecommendationService stationService)
        {
            _stationService = stationService;

            eventAggregator.GetEvent<RobotLowBatteryEvent>()
                .Subscribe(OnRobotLowBattery, ThreadOption.UIThread);
        }

        private void OnRobotLowBattery(RobotBatteryAlert alert)
        {
            var recommendation = new LowBatteryChargeRecommendation
            {
                Alert = alert,
                RecommendedStation = _stationService.RecommendAvailableStation(),
            };

            var existing = LowBatteryAgvs.FirstOrDefault(item => item.VehicleId == alert.VehicleId);
            if (existing is null)
            {
                LowBatteryAgvs.Insert(0, recommendation);
                return;
            }

            var index = LowBatteryAgvs.IndexOf(existing);
            LowBatteryAgvs[index] = recommendation;
        }
    }
}
