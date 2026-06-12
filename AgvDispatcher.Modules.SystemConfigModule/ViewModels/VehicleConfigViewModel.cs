using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System.ComponentModel;
using System.Windows.Data;

namespace AgvDispatcher.Modules.SystemConfigModule.ViewModels
{
    public class VehicleConfigViewModel : BindableBase
    {
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly IMapRepository _mapRepository;
        private Vehicle? _selectedVehicle;
        private EditableVehicle _currentVehicle = new();
        private string _statusMessage = "请选择车辆或新增车辆档案";
        private string _filterBrand = "全部";
        private string _filterStatus = "全部";
        private string _filterCapability = "全部";

        public ObservableCollection<Vehicle> Vehicles { get; } = new();
        public ICollectionView VehiclesView { get; }

        public IReadOnlyList<VehicleType> VehicleTypes { get; } = Enum.GetValues<VehicleType>();

        public IReadOnlyList<string> AdapterTypes { get; } = new[] { "MockBrandA", "MockBrandB", "MockBrandC", "MockUnstable", "MockFault", "MockOffline", "HttpAdapter", "TcpAdapter" };
        public IReadOnlyList<string> ProtocolTypes { get; } = new[] { "None", "HTTP", "TCP", "UDP", "Modbus", "MQTT" };
        public IReadOnlyList<string> NavigationTypes { get; } = new[] { "Laser", "QR_Code", "Magnetic", "SLAM", "Unknown" };
        public IReadOnlyList<string> LoadModes { get; } = new[] { "Lifting", "Forklift", "Roller", "Towing", "None" };

        public IReadOnlyList<VehicleCapability> CapabilityEnumValues { get; } = Enum.GetValues<VehicleCapability>().Where(e => e != VehicleCapability.None).ToList();
        public IReadOnlyList<VehicleCommandCapability> CommandEnumValues { get; } = Enum.GetValues<VehicleCommandCapability>().Where(e => e != VehicleCommandCapability.None).ToList();

        public ObservableCollection<string> AvailableBrands { get; } = new() { "全部" };
        public ObservableCollection<string> StatusOptions { get; } = new() { "全部", "已启用", "已停用" };
        public ObservableCollection<string> CapabilityOptions { get; } = new() { "全部", "搬运", "顶升", "叉取", "牵引", "滚筒", "充电", "自动充电" };
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
                    StatusMessage = $"正在编辑 {value.VehicleCode}";
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

        public VehicleConfigViewModel(IVehicleRepository vehicleRepository, IEventAggregator eventAggregator, IMapRepository mapRepository)
        {
            _vehicleRepository = vehicleRepository;
            _eventAggregator = eventAggregator;
            _mapRepository = mapRepository;

            VehiclesView = CollectionViewSource.GetDefaultView(Vehicles);
            VehiclesView.Filter = FilterVehicle;

            RefreshCommand = new DelegateCommand(LoadVehicles);
            AddCommand = new DelegateCommand(AddVehicle);
            SaveCommand = new DelegateCommand(SaveVehicle);
            DeleteCommand = new DelegateCommand(DeleteVehicle, CanOperateVehicle).ObservesProperty(() => SelectedVehicle);
            CopyCommand = new DelegateCommand(CopyVehicle, CanOperateVehicle).ObservesProperty(() => SelectedVehicle);

            LoadNodesAsync();
            LoadVehicles();
        }

        private async void LoadNodesAsync()
        {
            var nodes = await _mapRepository.GetNodesAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableNodeIds.Clear();
                AvailableChargeNodeIds.Clear();
                foreach (var node in nodes.Where(n => n.IsEnabled))
                {
                    AvailableNodeIds.Add(node.NodeId);
                    if (node.NodeType == MapNodeType.Charge)
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

            var brands = _vehicleRepository.GetAllAsync().GetAwaiter().GetResult();
            
            AvailableBrands.Clear();
            AvailableBrands.Add("全部");
            foreach (var b in brands.Select(v => v.Brand).Distinct().Where(b => !string.IsNullOrEmpty(b)))
            {
                if (!AvailableBrands.Contains(b))
                    AvailableBrands.Add(b);
            }

            // Restore filter brand to prevent UI items from disappearing due to null filter
            if (oldFilterBrand != null && AvailableBrands.Contains(oldFilterBrand))
            {
                FilterBrand = oldFilterBrand;
            }
            else
            {
                FilterBrand = "全部";
            }

            foreach (var vehicle in brands)
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
                AreaCode = "A",
                MaxSpeed = 1.2,
                RatedLoad = 500,
                BatteryCapacityAh = 100,
                IsEnabled = true
            };

            SelectedVehicle = null;
            StatusMessage = "正在新增车辆档案";
        }

        private void SaveVehicle()
        {
            if (string.IsNullOrWhiteSpace(CurrentVehicle.VehicleId) || string.IsNullOrWhiteSpace(CurrentVehicle.VehicleCode))
            {
                StatusMessage = "车辆ID和车辆编号不能为空";
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentVehicle.HomeNodeId) && !AvailableNodeIds.Contains(CurrentVehicle.HomeNodeId))
            {
                System.Windows.MessageBox.Show($"待机点 '{CurrentVehicle.HomeNodeId}' 不存在于可用节点中，请重新选择", "验证失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                StatusMessage = "保存失败：待机点无效";
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentVehicle.ChargeNodeId) && !AvailableChargeNodeIds.Contains(CurrentVehicle.ChargeNodeId))
            {
                System.Windows.MessageBox.Show($"充电点 '{CurrentVehicle.ChargeNodeId}' 不存在于可用充电节点中，请重新选择", "验证失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                StatusMessage = "保存失败：充电点无效";
                return;
            }

            var vehicle = CurrentVehicle.ToVehicle();
            _vehicleRepository.SaveAsync(vehicle).GetAwaiter().GetResult();
            _eventAggregator.GetEvent<VehicleConfigurationChangedEvent>().Publish(new VehicleConfigurationChangedMessage
            {
                Vehicle = vehicle
            });

            StatusMessage = $"{vehicle.VehicleCode} 已保存";
            LoadVehicles();
            SelectedVehicle = Vehicles.FirstOrDefault(item => item.VehicleId == vehicle.VehicleId);
        }

        private bool CanOperateVehicle() => SelectedVehicle is not null;

        private void DeleteVehicle()
        {
            if (SelectedVehicle is null) return;
            var vehicleCode = SelectedVehicle.VehicleCode;
            _vehicleRepository.DeleteAsync(SelectedVehicle.VehicleId).GetAwaiter().GetResult();
            StatusMessage = $"{vehicleCode} 已删除";
            LoadVehicles();
        }

        private void CopyVehicle()
        {
            if (SelectedVehicle is null) return;
            var nextNumber = Vehicles.Count + 1;
            
            CurrentVehicle = EditableVehicle.FromVehicle(SelectedVehicle);
            CurrentVehicle.VehicleId = $"AGV-{nextNumber:000}";
            CurrentVehicle.VehicleCode = $"AGV-{nextNumber:000}";
            CurrentVehicle.Name = $"{SelectedVehicle.Name} (Copy)";
            
            SelectedVehicle = null;
            StatusMessage = $"正在复制 {SelectedVehicle?.VehicleCode ?? "车辆"} 到 {CurrentVehicle.VehicleCode}";
        }

        private bool FilterVehicle(object obj)
        {
            if (obj is not Vehicle vehicle) return false;

            if (FilterBrand != "全部" && vehicle.Brand != FilterBrand)
                return false;

            if (FilterStatus == "已启用" && !vehicle.IsEnabled)
                return false;
            
            if (FilterStatus == "已停用" && vehicle.IsEnabled)
                return false;

            if (FilterCapability != "全部")
            {
                var targetCap = FilterCapability switch
                {
                    "搬运" => VehicleCapability.Transfer,
                    "顶升" => VehicleCapability.Lift,
                    "叉取" => VehicleCapability.Fork,
                    "牵引" => VehicleCapability.Tow,
                    "滚筒" => VehicleCapability.Roller,
                    "充电" => VehicleCapability.Charge,
                    "自动充电" => VehicleCapability.AutoCharge,
                    _ => VehicleCapability.None
                };

                if (targetCap != VehicleCapability.None && !vehicle.CapabilityFlags.HasFlag(targetCap))
                    return false;
            }

            return true;
        }
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
