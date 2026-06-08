using System.Collections.ObjectModel;
using AgvDispatcher.Modules.ChargeModule.Models;
using AgvDispatcher.Modules.ChargeModule.Services;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.ChargeModule.ViewModels
{
    public class ChargeListViewModel : BindableBase
    {
        private ObservableCollection<ChargeStationModel> _stationList = new();

        public ObservableCollection<ChargeStationModel> StationList
        {
            get => _stationList;
            set => SetProperty(ref _stationList, value);
        }

        public ChargeListViewModel(ChargeStationRecommendationService stationService)
        {
            StationList = stationService.Stations;
        }
    }
}
