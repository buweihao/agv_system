using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Modules.ChargeModule.Models;
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

        public ChargeListViewModel()
        {
            StationList.Add(new ChargeStationModel { Id = "C-01", State = RobotState.Running, LinkedAgv = "AGV-002", BatteryLevel = 45, ChargeMode = "快充", ChargePower = "30kW", ChargedTime = "00:15:30", EstimatedCompletion = "00:20:00" });
            StationList.Add(new ChargeStationModel { Id = "C-02", State = RobotState.Idle, LinkedAgv = "AGV-015", BatteryLevel = 12, ChargeMode = "慢充", ChargePower = "10kW", ChargedTime = "00:00:00", EstimatedCompletion = "01:30:00" });
            StationList.Add(new ChargeStationModel { Id = "C-03", State = RobotState.Idle, LinkedAgv = "-", BatteryLevel = 0, ChargeMode = "-", ChargePower = "-", ChargedTime = "-", EstimatedCompletion = "-" });
            StationList.Add(new ChargeStationModel { Id = "C-04", State = RobotState.Fault, LinkedAgv = "-", BatteryLevel = 0, ChargeMode = "-", ChargePower = "-", ChargedTime = "-", EstimatedCompletion = "-" });
            StationList.Add(new ChargeStationModel { Id = "C-05", State = RobotState.Offline, LinkedAgv = "-", BatteryLevel = 0, ChargeMode = "-", ChargePower = "-", ChargedTime = "-", EstimatedCompletion = "-" });
        }
    }
}
