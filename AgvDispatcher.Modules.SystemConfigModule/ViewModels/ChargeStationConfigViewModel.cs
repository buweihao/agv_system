using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SystemConfigModule.ViewModels
{
    public class ChargeStationConfigViewModel : BindableBase
    {
        private readonly IChargeStationRepository _chargeStationRepository;
        
        public ObservableCollection<ChargeStation> Stations { get; } = new();

        public DelegateCommand RefreshCommand { get; }
        
        public int TotalStations => Stations.Count;

        public ChargeStationConfigViewModel(IChargeStationRepository chargeStationRepository)
        {
            _chargeStationRepository = chargeStationRepository;

            RefreshCommand = new DelegateCommand(LoadData);

            LoadData();
        }

        private void LoadData()
        {
            Stations.Clear();
            var stations = _chargeStationRepository.GetAllAsync().GetAwaiter().GetResult();
            foreach (var s in stations) Stations.Add(s);

            RaisePropertyChanged(nameof(TotalStations));
        }
    }
}
