using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.MonitoringModule.Models;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitoringModule.ViewModels
{
    public class RobotListViewModel : BindableBase
    {
        private const double LowBatteryThreshold = 20;

        private ObservableCollection<RobotModel> _robotList = new();
        public ObservableCollection<RobotModel> RobotList
        {
            get => _robotList;
            set => SetProperty(ref _robotList, value);
        }

        public RobotListViewModel(IEventAggregator eventAggregator)
        {
            var snapshots = CreateMockStatusSnapshots();

            RobotList = new ObservableCollection<RobotModel>(
                snapshots.Select(ToRobotModel));

            PublishLowBatteryAlerts(eventAggregator, snapshots);
        }

        private static IEnumerable<VehicleStatusSnapshot> CreateMockStatusSnapshots()
        {
            var reportedAt = DateTime.Now;

            return new[]
            {
                new VehicleStatusSnapshot { VehicleId = "AGV-001", Brand = "RGV-A", State = RobotState.Running, CurrentTaskId = "TASK20240112001", Location = "A01-02", BatteryLevel = 18, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-003", Brand = "RGV-A", State = RobotState.Running, CurrentTaskId = "TASK20240112002", Location = "A02-08", BatteryLevel = 65, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-008", Brand = "RGV-B", State = RobotState.Fault, CurrentTaskId = null, Location = "B01-05", BatteryLevel = 12, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-010", Brand = "RGV-B", State = RobotState.Running, CurrentTaskId = "TASK20240112003", Location = "A03-01", BatteryLevel = 80, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-017", Brand = "RGV-C", State = RobotState.Idle, CurrentTaskId = null, Location = "Charge-03", BatteryLevel = 92, ReportedAt = reportedAt }
            };
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

        private static void PublishLowBatteryAlerts(IEventAggregator eventAggregator, IEnumerable<VehicleStatusSnapshot> snapshots)
        {
            foreach (var snapshot in snapshots.Where(snapshot => snapshot.BatteryLevel < LowBatteryThreshold))
            {
                eventAggregator.GetEvent<RobotLowBatteryEvent>().Publish(new RobotBatteryAlert
                {
                    VehicleId = snapshot.VehicleId,
                    Brand = snapshot.Brand,
                    BatteryLevel = snapshot.BatteryLevel,
                    Location = snapshot.Location,
                    Threshold = LowBatteryThreshold,
                    OccurredAt = snapshot.ReportedAt
                });
            }
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
