using System.Collections.ObjectModel;
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
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IEventAggregator _eventAggregator;
        private Vehicle? _selectedVehicle;
        private EditableVehicle _currentVehicle = new();
        private string _statusMessage = "请选择车辆或新增车辆档案";

        public ObservableCollection<Vehicle> Vehicles { get; } = new();

        public IReadOnlyList<VehicleType> VehicleTypes { get; } = Enum.GetValues<VehicleType>();

        public DelegateCommand RefreshCommand { get; }

        public DelegateCommand AddCommand { get; }

        public DelegateCommand SaveCommand { get; }

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

        public VehicleConfigViewModel(IVehicleRepository vehicleRepository, IEventAggregator eventAggregator)
        {
            _vehicleRepository = vehicleRepository;
            _eventAggregator = eventAggregator;

            RefreshCommand = new DelegateCommand(LoadVehicles);
            AddCommand = new DelegateCommand(AddVehicle);
            SaveCommand = new DelegateCommand(SaveVehicle);

            LoadVehicles();
        }

        private void LoadVehicles()
        {
            Vehicles.Clear();

            foreach (var vehicle in _vehicleRepository.GetAllAsync().GetAwaiter().GetResult())
            {
                Vehicles.Add(vehicle);
            }

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
    }

    public class EditableVehicle : BindableBase
    {
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
                Remark = vehicle.Remark
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
                Remark = Remark.Trim()
            };
        }
    }
}
