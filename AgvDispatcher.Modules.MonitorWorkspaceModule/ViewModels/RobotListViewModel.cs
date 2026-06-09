using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;
using AgvDispatcher.Modules.MonitorWorkspaceModule.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    public class RobotListViewModel : BindableBase
    {
        private readonly IVehicleStateStore _vehicleStateStore;
        private readonly IVehicleStatusPublisher _vehicleStatusPublisher;

        private ObservableCollection<RobotModel> _robotList = new();
        private string _vehicleId = "AGV-001";
        private string _brand = "RGV-A";
        private double _batteryLevel = 76;
        private string _location = "A01-01";
        private RobotState _state = RobotState.Running;
        private string _lastPublishMessage = "Ready";

        public ObservableCollection<RobotModel> RobotList
        {
            get => _robotList;
            set => SetProperty(ref _robotList, value);
        }

        public ObservableCollection<string> VehicleIdOptions { get; } = new()
        {
            "AGV-001",
            "AGV-003",
            "AGV-008",
            "AGV-010",
            "AGV-017"
        };

        public ObservableCollection<string> BrandOptions { get; } = new()
        {
            "RGV-A",
            "RGV-B",
            "RGV-C"
        };

        public IEnumerable<RobotState> StateOptions { get; } = Enum.GetValues<RobotState>();

        public string VehicleId
        {
            get => _vehicleId;
            set => SetProperty(ref _vehicleId, value);
        }

        public string Brand
        {
            get => _brand;
            set => SetProperty(ref _brand, value);
        }

        public double BatteryLevel
        {
            get => _batteryLevel;
            set => SetProperty(ref _batteryLevel, Math.Clamp(value, 0, 100));
        }

        public string Location
        {
            get => _location;
            set => SetProperty(ref _location, value);
        }

        public RobotState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        public string LastPublishMessage
        {
            get => _lastPublishMessage;
            set => SetProperty(ref _lastPublishMessage, value);
        }

        public DelegateCommand PublishStatusCommand { get; }

        public RobotListViewModel(
            IEventAggregator eventAggregator,
            IVehicleStateStore vehicleStateStore,
            IVehicleStatusPublisher vehicleStatusPublisher)
        {
            _vehicleStateStore = vehicleStateStore;
            _vehicleStatusPublisher = vehicleStatusPublisher;
            PublishStatusCommand = new DelegateCommand(PublishStatus, CanPublishStatus)
                .ObservesProperty(() => VehicleId)
                .ObservesProperty(() => Brand)
                .ObservesProperty(() => Location);

            RobotList = new ObservableCollection<RobotModel>(
                _vehicleStateStore.GetAllVehicles().Select(ToRobotModel));

            foreach (var snapshot in _vehicleStateStore.GetAllVehicles())
            {
                AddVehicleIdOption(snapshot.VehicleId);
            }

            eventAggregator.GetEvent<VehicleStateChangedEvent>().Subscribe(ApplyVehicleStateChange, ThreadOption.UIThread);
        }

        private bool CanPublishStatus()
        {
            return !string.IsNullOrWhiteSpace(VehicleId)
                && !string.IsNullOrWhiteSpace(Brand)
                && !string.IsNullOrWhiteSpace(Location);
        }

        private void PublishStatus()
        {
            var snapshot = new VehicleStatusSnapshot
            {
                VehicleId = VehicleId.Trim(),
                Brand = Brand.Trim(),
                BatteryLevel = Math.Clamp(BatteryLevel, 0, 100),
                Location = Location.Trim(),
                State = State,
                ReportedAt = DateTime.Now
            };

            var result = _vehicleStatusPublisher.PublishStatus(snapshot);

            LastPublishMessage = result.LowBatteryDetected
                ? $"{result.Snapshot.VehicleId} low battery alert published"
                : $"{result.Snapshot.VehicleId} status updated";
        }

        private void ApplyVehicleStateChange(VehicleStateChangedMessage message)
        {
            switch (message.ChangeType)
            {
                case VehicleStateChangeType.Added:
                case VehicleStateChangeType.Updated:
                    if (message.Snapshot is not null)
                    {
                        UpsertRobot(message.Snapshot);
                    }
                    break;

                case VehicleStateChangeType.Removed:
                    RemoveRobot(message.RemovedVehicleId);
                    break;
            }
        }

        private void UpsertRobot(VehicleStatusSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.VehicleId))
            {
                return;
            }

            var robot = ToRobotModel(snapshot);
            var existingIndex = RobotList
                .Select((item, index) => new { item, index })
                .FirstOrDefault(x => string.Equals(x.item.Id, snapshot.VehicleId, StringComparison.OrdinalIgnoreCase))
                ?.index;

            if (existingIndex.HasValue)
            {
                RobotList[existingIndex.Value] = robot;
                return;
            }

            RobotList.Add(robot);
            AddVehicleIdOption(snapshot.VehicleId);
        }

        private void RemoveRobot(string? vehicleId)
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                return;
            }

            var robot = RobotList.FirstOrDefault(item =>
                string.Equals(item.Id, vehicleId, StringComparison.OrdinalIgnoreCase));

            if (robot is not null)
            {
                RobotList.Remove(robot);
            }
        }

        private void AddVehicleIdOption(string vehicleId)
        {
            if (!string.IsNullOrWhiteSpace(vehicleId) && !VehicleIdOptions.Contains(vehicleId))
            {
                VehicleIdOptions.Add(vehicleId);
            }
        }

        private static RobotModel ToRobotModel(VehicleStatusSnapshot snapshot)
        {
            return new RobotModel
            {
                Id = snapshot.VehicleId,
                Brand = snapshot.Brand,
                State = snapshot.State,
                TaskId = string.IsNullOrWhiteSpace(snapshot.CurrentTaskId) ? "-" : snapshot.CurrentTaskId,
                CurrentPosition = snapshot.Location,
                TargetPosition = GetMockTargetPosition(snapshot.VehicleId),
                BatteryLevel = (int)Math.Round(snapshot.BatteryLevel),
                Speed = GetMockSpeed(snapshot.State, snapshot.VehicleId),
                RunningTime = GetMockRunningTime(snapshot.VehicleId)
            };
        }

        private static string GetMockTargetPosition(string vehicleId)
        {
            return vehicleId switch
            {
                "AGV-001" => "B03-05",
                "AGV-003" => "C02-03",
                "AGV-010" => "D01-02",
                _ => "-"
            };
        }

        private static double GetMockSpeed(RobotState state, string vehicleId)
        {
            if (state != RobotState.Running)
            {
                return 0.00;
            }

            return vehicleId switch
            {
                "AGV-001" => 1.25,
                "AGV-003" => 1.10,
                "AGV-010" => 1.48,
                _ => 0.00
            };
        }

        private static string GetMockRunningTime(string vehicleId)
        {
            return vehicleId switch
            {
                "AGV-001" => "02:35:23",
                "AGV-003" => "01:45:11",
                "AGV-008" => "00:12:08",
                "AGV-010" => "02:12:56",
                "AGV-017" => "01:10:34",
                _ => "00:00:00"
            };
        }
    }
}
