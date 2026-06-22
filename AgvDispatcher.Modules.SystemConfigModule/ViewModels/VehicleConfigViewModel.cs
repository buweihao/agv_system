using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SystemConfigModule.ViewModels
{
    public class VehicleConfigViewModel : BindableBase
    {
        private const string AllText = "\u5168\u90e8";
        private const string EnabledText = "\u5df2\u542f\u7528";
        private const string DisabledText = "\u5df2\u505c\u7528";

        private readonly IVehicleRepository _vehicleRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly IMapService _mapService;
        private Vehicle? _selectedVehicle;
        private EditableVehicle _currentVehicle = new();
        private string _statusMessage = "\u8bf7\u9009\u62e9\u8f66\u8f86\u6216\u65b0\u589e\u8f66\u8f86\u6863\u6848";
        private string _filterBrand = AllText;
        private string _filterStatus = AllText;
        private string _filterCapability = AllText;

        public ObservableCollection<Vehicle> Vehicles { get; } = new();
        public ICollectionView VehiclesView { get; }

        public IReadOnlyList<VehicleType> VehicleTypes { get; } = Enum.GetValues<VehicleType>();

        public IReadOnlyList<string> AdapterTypes { get; } = new[] { "MockBrandA", "MockBrandB", "MockBrandC", "MockUnstable", "MockFault", "MockOffline", "HttpAdapter", "TcpAdapter", "Okapi" };
        public IReadOnlyList<string> ProtocolTypes { get; } = new[] { "None", "HTTP", "TCP", "UDP", "Modbus", "MQTT" };
        public IReadOnlyList<string> NavigationTypes { get; } = new[] { "Laser", "QR_Code", "Magnetic", "SLAM", "Unknown" };
        public IReadOnlyList<string> LoadModes { get; } = new[] { "Lifting", "Forklift", "Roller", "Towing", "None" };

        public IReadOnlyList<VehicleCapability> CapabilityEnumValues { get; } = Enum.GetValues<VehicleCapability>().Where(e => e != VehicleCapability.None).ToList();
        public IReadOnlyList<VehicleCommandCapability> CommandEnumValues { get; } = Enum.GetValues<VehicleCommandCapability>().Where(e => e != VehicleCommandCapability.None).ToList();

        public ObservableCollection<string> AvailableBrands { get; } = new() { AllText };
        public ObservableCollection<string> StatusOptions { get; } = new() { AllText, EnabledText, DisabledText };
        public ObservableCollection<string> CapabilityOptions { get; } = new() { AllText, "\u642c\u8fd0", "\u9876\u5347", "\u53c9\u53d6", "\u7275\u5f15", "\u6eda\u7b52", "\u5145\u7535", "\u81ea\u52a8\u5145\u7535" };
        public ObservableCollection<MapAreaOption> AvailableAreas { get; } = new();
        public ObservableCollection<string> AvailableNodeIds { get; } = new();
        public ObservableCollection<string> AvailableChargeNodeIds { get; } = new();

        public string FilterBrand
        {
            get => _filterBrand;
            set { if (SetProperty(ref _filterBrand, value)) VehiclesView.Refresh(); }
        }

        public string FilterStatus
        {
            get => _filterStatus;
            set { if (SetProperty(ref _filterStatus, value)) VehiclesView.Refresh(); }
        }

        public string FilterCapability
        {
            get => _filterCapability;
            set { if (SetProperty(ref _filterCapability, value)) VehiclesView.Refresh(); }
        }

        public DelegateCommand RefreshCommand { get; }

        public DelegateCommand AddCommand { get; }

        public DelegateCommand SaveCommand { get; }

        public DelegateCommand DeleteCommand { get; }

        public DelegateCommand CopyCommand { get; }

        public Vehicle? SelectedVehicle
        {
            get => _selectedVehicle;
            set
            {
                if (SetProperty(ref _selectedVehicle, value) && value is not null)
                {
                    CurrentVehicle = EditableVehicle.FromVehicle(value);
                    StatusMessage = $"\u6b63\u5728\u7f16\u8f91 {value.VehicleCode}";
                }
            }
        }

        public EditableVehicle CurrentVehicle
        {
            get => _currentVehicle;
            set => SetProperty(ref _currentVehicle, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int TotalCount => Vehicles.Count;

        public int EnabledCount => Vehicles.Count(vehicle => vehicle.IsEnabled);

        public VehicleConfigViewModel(IVehicleRepository vehicleRepository, IEventAggregator eventAggregator, IMapService mapService)
        {
            _vehicleRepository = vehicleRepository;
            _eventAggregator = eventAggregator;
            _mapService = mapService;

            VehiclesView = CollectionViewSource.GetDefaultView(Vehicles);
            VehiclesView.Filter = FilterVehicle;

            RefreshCommand = new DelegateCommand(LoadVehicles);
            AddCommand = new DelegateCommand(AddVehicle);
            SaveCommand = new DelegateCommand(SaveVehicle);
            DeleteCommand = new DelegateCommand(DeleteVehicle, CanOperateVehicle).ObservesProperty(() => SelectedVehicle);
            CopyCommand = new DelegateCommand(CopyVehicle, CanOperateVehicle).ObservesProperty(() => SelectedVehicle);

            LoadMapOptions();
            LoadVehicles();
        }

        private void LoadMapOptions()
        {
            var result = _mapService.GetCurrentMap(new GetMapSnapshotRequest());
            var snapshot = result.Success ? result.Data : null;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableAreas.Clear();
                AvailableNodeIds.Clear();
                AvailableChargeNodeIds.Clear();

                if (snapshot is null)
                {
                    return;
                }

                foreach (var area in snapshot.Areas.Where(area => area.Enabled).OrderBy(area => area.AreaId))
                {
                    AvailableAreas.Add(new MapAreaOption(area.AreaId, area.AreaName));
                }

                foreach (var node in snapshot.Nodes.Where(node => node.Enabled).OrderBy(node => node.NodeId))
                {
                    AvailableNodeIds.Add(node.NodeId);
                    if (node.NodeType == AgvDispatcher.Core.Contracts.Map.MapNodeType.ChargeStation)
                    {
                        AvailableChargeNodeIds.Add(node.NodeId);
                    }
                }
            });
        }

        private void LoadVehicles()
        {
            var oldFilterBrand = _filterBrand;

            Vehicles.Clear();

            var vehicles = _vehicleRepository.GetAllAsync().GetAwaiter().GetResult();

            AvailableBrands.Clear();
            AvailableBrands.Add(AllText);
            foreach (var brand in vehicles.Select(v => v.Brand).Distinct().Where(brand => !string.IsNullOrEmpty(brand)))
            {
                if (!AvailableBrands.Contains(brand))
                {
                    AvailableBrands.Add(brand);
                }
            }

            FilterBrand = oldFilterBrand != null && AvailableBrands.Contains(oldFilterBrand)
                ? oldFilterBrand
                : AllText;

            foreach (var vehicle in vehicles)
            {
                Vehicles.Add(vehicle);
            }

            VehiclesView.Refresh();

            RaisePropertyChanged(nameof(TotalCount));
            RaisePropertyChanged(nameof(EnabledCount));

            if (Vehicles.Count > 0)
            {
                SelectedVehicle = Vehicles[0];
            }
            else
            {
                AddVehicle();
            }
        }

        private void AddVehicle()
        {
            var nextNumber = Vehicles.Count + 1;
            CurrentVehicle = new EditableVehicle
            {
                VehicleId = $"AGV-{nextNumber:000}",
                VehicleCode = $"AGV-{nextNumber:000}",
                Name = $"AGV {nextNumber:000}",
                Brand = "RGV-A",
                Model = "A100",
                Type = VehicleType.Agv,
                AreaCode = AvailableAreas.FirstOrDefault(area => area.AreaId == "STBY-CHG")?.AreaId ?? AvailableAreas.FirstOrDefault()?.AreaId ?? string.Empty,
                HomeNodeId = AvailableNodeIds.Contains("WAIT-01") ? "WAIT-01" : AvailableNodeIds.FirstOrDefault() ?? string.Empty,
                ChargeNodeId = AvailableChargeNodeIds.Contains("CHG-01") ? "CHG-01" : AvailableChargeNodeIds.FirstOrDefault() ?? string.Empty,
                MaxSpeed = 1.2,
                RatedLoad = 500,
                BatteryCapacityAh = 100,
                IsEnabled = true
            };

            SelectedVehicle = null;
            StatusMessage = "\u6b63\u5728\u65b0\u589e\u8f66\u8f86\u6863\u6848";
        }

        private void SaveVehicle()
        {
            if (string.IsNullOrWhiteSpace(CurrentVehicle.VehicleId) || string.IsNullOrWhiteSpace(CurrentVehicle.VehicleCode))
            {
                StatusMessage = "\u8f66\u8f86ID\u548c\u8f66\u8f86\u7f16\u53f7\u4e0d\u80fd\u4e3a\u7a7a";
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentVehicle.AreaCode)
                && AvailableAreas.All(area => !string.Equals(area.AreaId, CurrentVehicle.AreaCode, StringComparison.OrdinalIgnoreCase)))
            {
                System.Windows.MessageBox.Show($"\u533a\u57df '{CurrentVehicle.AreaCode}' \u4e0d\u5b58\u5728\u4e8e\u5f53\u524d\u5730\u56fe\u533a\u57df\u4e2d\uff0c\u8bf7\u91cd\u65b0\u9009\u62e9", "\u9a8c\u8bc1\u5931\u8d25", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                StatusMessage = "\u4fdd\u5b58\u5931\u8d25\uff1a\u533a\u57df\u65e0\u6548";
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentVehicle.HomeNodeId) && !AvailableNodeIds.Contains(CurrentVehicle.HomeNodeId))
            {
                System.Windows.MessageBox.Show($"\u9ed8\u8ba4\u505c\u9760\u70b9 '{CurrentVehicle.HomeNodeId}' \u4e0d\u5b58\u5728\u4e8e\u53ef\u7528\u8282\u70b9\u4e2d\uff0c\u8bf7\u91cd\u65b0\u9009\u62e9", "\u9a8c\u8bc1\u5931\u8d25", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                StatusMessage = "\u4fdd\u5b58\u5931\u8d25\uff1a\u9ed8\u8ba4\u505c\u9760\u70b9\u65e0\u6548";
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentVehicle.ChargeNodeId) && !AvailableChargeNodeIds.Contains(CurrentVehicle.ChargeNodeId))
            {
                System.Windows.MessageBox.Show($"\u9ed8\u8ba4\u5145\u7535\u70b9 '{CurrentVehicle.ChargeNodeId}' \u4e0d\u5b58\u5728\u4e8e\u53ef\u7528\u5145\u7535\u8282\u70b9\u4e2d\uff0c\u8bf7\u91cd\u65b0\u9009\u62e9", "\u9a8c\u8bc1\u5931\u8d25", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                StatusMessage = "\u4fdd\u5b58\u5931\u8d25\uff1a\u9ed8\u8ba4\u5145\u7535\u70b9\u65e0\u6548";
                return;
            }

            var vehicle = CurrentVehicle.ToVehicle();
            _vehicleRepository.SaveAsync(vehicle).GetAwaiter().GetResult();
            _eventAggregator.GetEvent<VehicleConfigurationChangedEvent>().Publish(new VehicleConfigurationChangedMessage
            {
                Vehicle = vehicle
            });

            StatusMessage = $"{vehicle.VehicleCode} \u5df2\u4fdd\u5b58";
            LoadVehicles();
            SelectedVehicle = Vehicles.FirstOrDefault(item => item.VehicleId == vehicle.VehicleId);
        }

        private bool CanOperateVehicle() => SelectedVehicle is not null;

        private void DeleteVehicle()
        {
            if (SelectedVehicle is null) return;
            var vehicleCode = SelectedVehicle.VehicleCode;
            _vehicleRepository.DeleteAsync(SelectedVehicle.VehicleId).GetAwaiter().GetResult();
            StatusMessage = $"{vehicleCode} \u5df2\u5220\u9664";
            LoadVehicles();
        }

        private void CopyVehicle()
        {
            if (SelectedVehicle is null) return;
            var copiedVehicleCode = SelectedVehicle.VehicleCode;
            var nextNumber = Vehicles.Count + 1;

            CurrentVehicle = EditableVehicle.FromVehicle(SelectedVehicle);
            CurrentVehicle.VehicleId = $"AGV-{nextNumber:000}";
            CurrentVehicle.VehicleCode = $"AGV-{nextNumber:000}";
            CurrentVehicle.Name = $"{SelectedVehicle.Name} (Copy)";

            SelectedVehicle = null;
            StatusMessage = $"\u6b63\u5728\u590d\u5236 {copiedVehicleCode} \u5230 {CurrentVehicle.VehicleCode}";
        }

        private bool FilterVehicle(object obj)
        {
            if (obj is not Vehicle vehicle) return false;

            if (FilterBrand != AllText && vehicle.Brand != FilterBrand)
                return false;

            if (FilterStatus == EnabledText && !vehicle.IsEnabled)
                return false;

            if (FilterStatus == DisabledText && vehicle.IsEnabled)
                return false;

            if (FilterCapability != AllText)
            {
                var targetCap = FilterCapability switch
                {
                    "\u642c\u8fd0" => VehicleCapability.Transfer,
                    "\u9876\u5347" => VehicleCapability.Lift,
                    "\u53c9\u53d6" => VehicleCapability.Fork,
                    "\u7275\u5f15" => VehicleCapability.Tow,
                    "\u6eda\u7b52" => VehicleCapability.Roller,
                    "\u5145\u7535" => VehicleCapability.Charge,
                    "\u81ea\u52a8\u5145\u7535" => VehicleCapability.AutoCharge,
                    _ => VehicleCapability.None
                };

                if (targetCap != VehicleCapability.None && !vehicle.CapabilityFlags.HasFlag(targetCap))
                    return false;
            }

            return true;
        }
    }

    public sealed class MapAreaOption
    {
        public MapAreaOption(string areaId, string areaName)
        {
            AreaId = areaId;
            AreaName = areaName;
        }

        public string AreaId { get; }

        public string AreaName { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(AreaName) || string.Equals(AreaId, AreaName, StringComparison.OrdinalIgnoreCase)
            ? AreaId
            : $"{AreaId} ({AreaName})";
    }

    public class EditableVehicle : BindableBase
    {
        private bool _isSyncingCapabilities = false;
        private bool _isSyncingCommands = false;

        public ObservableCollection<VehicleCapability> SelectedCapabilities { get; } = new();
        public ObservableCollection<VehicleCommandCapability> SelectedCommands { get; } = new();

        public EditableVehicle()
        {
            SelectedCapabilities.CollectionChanged += (s, e) =>
            {
                if (_isSyncingCapabilities) return;
                _isSyncingCapabilities = true;
                VehicleCapability flags = VehicleCapability.None;
                foreach (var item in SelectedCapabilities) flags |= item;
                CapabilityFlags = flags;
                _isSyncingCapabilities = false;
            };

            SelectedCommands.CollectionChanged += (s, e) =>
            {
                if (_isSyncingCommands) return;
                _isSyncingCommands = true;
                VehicleCommandCapability flags = VehicleCommandCapability.None;
                foreach (var item in SelectedCommands) flags |= item;
                SupportedCommandFlags = flags;
                _isSyncingCommands = false;
            };
        }

        private string _vehicleId = string.Empty;
        private string _vehicleCode = string.Empty;
        private string _name = string.Empty;
        private string _brand = string.Empty;
        private string _model = string.Empty;
        private VehicleType _type = VehicleType.Agv;
        private string _serialNumber = string.Empty;
        private string _ipAddress = string.Empty;
        private string _areaCode = string.Empty;
        private double _maxSpeed;
        private double _ratedLoad;
        private double _length;
        private double _width;
        private double _height;
        private double _batteryCapacityAh;
        private bool _isEnabled = true;
        private string _remark = string.Empty;

        private string _adapterType = string.Empty;
        private string _protocolType = string.Empty;
        private string _endpoint = string.Empty;
        private int _port;
        private int _heartbeatTimeoutSeconds = 30;
        private string _navigationType = string.Empty;
        private string _loadMode = string.Empty;
        private VehicleCapability _capabilityFlags = VehicleCapability.None;
        private VehicleCommandCapability _supportedCommandFlags = VehicleCommandCapability.None;
        private string _homeNodeId = string.Empty;
        private string _chargeNodeId = string.Empty;
        private double? _minDispatchBattery;

        public string VehicleId { get => _vehicleId; set => SetProperty(ref _vehicleId, value); }

        public string VehicleCode { get => _vehicleCode; set => SetProperty(ref _vehicleCode, value); }

        public string Name { get => _name; set => SetProperty(ref _name, value); }

        public string Brand { get => _brand; set => SetProperty(ref _brand, value); }

        public string Model { get => _model; set => SetProperty(ref _model, value); }

        public VehicleType Type { get => _type; set => SetProperty(ref _type, value); }

        public string SerialNumber { get => _serialNumber; set => SetProperty(ref _serialNumber, value); }

        public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }

        public string AreaCode { get => _areaCode; set => SetProperty(ref _areaCode, value); }

        public double MaxSpeed { get => _maxSpeed; set => SetProperty(ref _maxSpeed, value); }

        public double RatedLoad { get => _ratedLoad; set => SetProperty(ref _ratedLoad, value); }

        public double Length { get => _length; set => SetProperty(ref _length, value); }

        public double Width { get => _width; set => SetProperty(ref _width, value); }

        public double Height { get => _height; set => SetProperty(ref _height, value); }

        public double BatteryCapacityAh { get => _batteryCapacityAh; set => SetProperty(ref _batteryCapacityAh, value); }

        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        public string Remark { get => _remark; set => SetProperty(ref _remark, value); }

        public string AdapterType { get => _adapterType; set => SetProperty(ref _adapterType, value); }
        public string ProtocolType { get => _protocolType; set => SetProperty(ref _protocolType, value); }
        public string Endpoint { get => _endpoint; set => SetProperty(ref _endpoint, value); }
        public int Port { get => _port; set => SetProperty(ref _port, value); }
        public int HeartbeatTimeoutSeconds { get => _heartbeatTimeoutSeconds; set => SetProperty(ref _heartbeatTimeoutSeconds, value); }
        public string NavigationType { get => _navigationType; set => SetProperty(ref _navigationType, value); }
        public string LoadMode { get => _loadMode; set => SetProperty(ref _loadMode, value); }
        public VehicleCapability CapabilityFlags
        {
            get => _capabilityFlags;
            set
            {
                if (SetProperty(ref _capabilityFlags, value))
                {
                    if (_isSyncingCapabilities) return;
                    _isSyncingCapabilities = true;
                    SelectedCapabilities.Clear();
                    foreach (VehicleCapability flag in Enum.GetValues<VehicleCapability>())
                    {
                        if (flag != VehicleCapability.None && value.HasFlag(flag))
                        {
                            SelectedCapabilities.Add(flag);
                        }
                    }
                    _isSyncingCapabilities = false;
                }
            }
        }

        public VehicleCommandCapability SupportedCommandFlags
        {
            get => _supportedCommandFlags;
            set
            {
                if (SetProperty(ref _supportedCommandFlags, value))
                {
                    if (_isSyncingCommands) return;
                    _isSyncingCommands = true;
                    SelectedCommands.Clear();
                    foreach (VehicleCommandCapability flag in Enum.GetValues<VehicleCommandCapability>())
                    {
                        if (flag != VehicleCommandCapability.None && value.HasFlag(flag))
                        {
                            SelectedCommands.Add(flag);
                        }
                    }
                    _isSyncingCommands = false;
                }
            }
        }
        public string HomeNodeId { get => _homeNodeId; set => SetProperty(ref _homeNodeId, value); }
        public string ChargeNodeId { get => _chargeNodeId; set => SetProperty(ref _chargeNodeId, value); }
        public double? MinDispatchBattery { get => _minDispatchBattery; set => SetProperty(ref _minDispatchBattery, value); }

        public static EditableVehicle FromVehicle(Vehicle vehicle)
        {
            return new EditableVehicle
            {
                VehicleId = vehicle.VehicleId,
                VehicleCode = vehicle.VehicleCode,
                Name = vehicle.Name,
                Brand = vehicle.Brand,
                Model = vehicle.Model,
                Type = vehicle.Type,
                SerialNumber = vehicle.SerialNumber,
                IpAddress = vehicle.IpAddress,
                AreaCode = vehicle.AreaCode,
                MaxSpeed = vehicle.MaxSpeed,
                RatedLoad = vehicle.RatedLoad,
                Length = vehicle.Length,
                Width = vehicle.Width,
                Height = vehicle.Height,
                BatteryCapacityAh = vehicle.BatteryCapacityAh,
                IsEnabled = vehicle.IsEnabled,
                Remark = vehicle.Remark,
                AdapterType = vehicle.AdapterType,
                ProtocolType = vehicle.ProtocolType,
                Endpoint = vehicle.Endpoint,
                Port = vehicle.Port,
                HeartbeatTimeoutSeconds = vehicle.HeartbeatTimeoutSeconds,
                NavigationType = vehicle.NavigationType,
                LoadMode = vehicle.LoadMode,
                CapabilityFlags = vehicle.CapabilityFlags,
                SupportedCommandFlags = vehicle.SupportedCommandFlags,
                HomeNodeId = vehicle.HomeNodeId,
                ChargeNodeId = vehicle.ChargeNodeId,
                MinDispatchBattery = vehicle.MinDispatchBattery
            };
        }

        public Vehicle ToVehicle()
        {
            return new Vehicle
            {
                VehicleId = VehicleId.Trim(),
                VehicleCode = VehicleCode.Trim(),
                Name = Name.Trim(),
                Brand = Brand.Trim(),
                Model = Model.Trim(),
                Type = Type,
                SerialNumber = SerialNumber.Trim(),
                IpAddress = IpAddress.Trim(),
                AreaCode = AreaCode.Trim(),
                MaxSpeed = MaxSpeed,
                RatedLoad = RatedLoad,
                Length = Length,
                Width = Width,
                Height = Height,
                BatteryCapacityAh = BatteryCapacityAh,
                IsEnabled = IsEnabled,
                Remark = Remark.Trim(),
                AdapterType = AdapterType.Trim(),
                ProtocolType = ProtocolType.Trim(),
                Endpoint = Endpoint.Trim(),
                Port = Port,
                HeartbeatTimeoutSeconds = HeartbeatTimeoutSeconds,
                NavigationType = NavigationType.Trim(),
                LoadMode = LoadMode.Trim(),
                CapabilityFlags = CapabilityFlags,
                SupportedCommandFlags = SupportedCommandFlags,
                HomeNodeId = HomeNodeId.Trim(),
                ChargeNodeId = ChargeNodeId.Trim(),
                MinDispatchBattery = MinDispatchBattery
            };
        }
    }
}
