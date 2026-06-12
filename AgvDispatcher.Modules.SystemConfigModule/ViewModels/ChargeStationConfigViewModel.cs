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
        private readonly IMapRepository _mapRepository;
        
        public ObservableCollection<ChargeStation> Stations { get; } = new();
        public ObservableCollection<string> AvailableNodeIds { get; } = new();

        private ChargeStation? _selectedStation;
        public ChargeStation? SelectedStation
        {
            get => _selectedStation;
            set => SetProperty(ref _selectedStation, value);
        }

        public DelegateCommand RefreshCommand { get; }
        
        public DelegateCommand AddStationCommand { get; }
        public DelegateCommand SaveStationCommand { get; }
        public DelegateCommand DeleteStationCommand { get; }

        public int TotalStations => Stations.Count;

        public ChargeStationConfigViewModel(IChargeStationRepository chargeStationRepository, IMapRepository mapRepository)
        {
            _chargeStationRepository = chargeStationRepository;
            _mapRepository = mapRepository;

            RefreshCommand = new DelegateCommand(LoadData);

            AddStationCommand = new DelegateCommand(AddStation);
            SaveStationCommand = new DelegateCommand(SaveStation, () => SelectedStation != null).ObservesProperty(() => SelectedStation);
            DeleteStationCommand = new DelegateCommand(DeleteStation, () => SelectedStation != null).ObservesProperty(() => SelectedStation);

            LoadNodesAsync();
            LoadData();
        }

        private async void LoadNodesAsync()
        {
            var nodes = await _mapRepository.GetNodesAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableNodeIds.Clear();
                foreach (var node in nodes.Where(n => n.IsEnabled && n.NodeType == AgvDispatcher.Core.Enums.MapNodeType.Charge))
                {
                    AvailableNodeIds.Add(node.NodeId);
                }
            });
        }

        private void LoadData()
        {
            Stations.Clear();
            var stations = _chargeStationRepository.GetAllAsync().GetAwaiter().GetResult();
            foreach (var s in stations) Stations.Add(s);

            RaisePropertyChanged(nameof(TotalStations));
        }

        private void AddStation()
        {
            var nextNumber = Stations.Count + 1;
            var newStation = new ChargeStation
            {
                StationId = $"CS{nextNumber:000}",
                StationCode = $"CS{nextNumber:000}",
                Name = $"Charge Station {nextNumber}",
                NodeId = $"N1{nextNumber:00}",
                Position = new MapPosition { MapId = "MAIN", NodeId = $"N1{nextNumber:00}", X = 0, Y = 0 },
                IsEnabled = true,
                RatedPowerKw = 3.3,
                OutputVoltage = 48,
                OutputCurrent = 30
            };
            Stations.Add(newStation);
            SelectedStation = newStation;
            RaisePropertyChanged(nameof(TotalStations));
        }

        private void SaveStation()
        {
            if (SelectedStation == null) return;
            if (string.IsNullOrWhiteSpace(SelectedStation.StationId))
            {
                System.Windows.MessageBox.Show("充电桩ID不能为空", "校验失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            _chargeStationRepository.SaveAsync(SelectedStation).GetAwaiter().GetResult();
            LoadData();
        }

        private void DeleteStation()
        {
            if (SelectedStation == null) return;
            _chargeStationRepository.DeleteAsync(SelectedStation.StationId).GetAwaiter().GetResult();
            LoadData();
        }
    }
}
