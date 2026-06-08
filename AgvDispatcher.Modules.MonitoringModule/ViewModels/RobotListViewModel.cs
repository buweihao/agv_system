using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Modules.MonitoringModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitoringModule.ViewModels
{
    public class RobotListViewModel : BindableBase
    {
        private ObservableCollection<RobotModel> _robotList;
        public ObservableCollection<RobotModel> RobotList
        {
            get => _robotList;
            set => SetProperty(ref _robotList, value);
        }

        public RobotListViewModel()
        {
            RobotList = new ObservableCollection<RobotModel>
            {
                new RobotModel { Id = "001", State = RobotState.Running, TaskId = "TASK20240112001", CurrentPosition = "A01-02", TargetPosition = "B03-05", BatteryLevel = 78, Speed = 1.25, RunningTime = "02:35:23" },
                new RobotModel { Id = "003", State = RobotState.Running, TaskId = "TASK20240112002", CurrentPosition = "A02-08", TargetPosition = "C02-03", BatteryLevel = 65, Speed = 1.10, RunningTime = "01:45:11" },
                new RobotModel { Id = "008", State = RobotState.Fault, TaskId = "-", CurrentPosition = "B01-05", TargetPosition = "-", BatteryLevel = 12, Speed = 0.00, RunningTime = "00:12:08" },
                new RobotModel { Id = "010", State = RobotState.Running, TaskId = "TASK20240112003", CurrentPosition = "A03-01", TargetPosition = "D01-02", BatteryLevel = 80, Speed = 1.48, RunningTime = "02:12:56" },
                new RobotModel { Id = "017", State = RobotState.Idle, TaskId = "-", CurrentPosition = "充电桩03", TargetPosition = "-", BatteryLevel = 92, Speed = 0.00, RunningTime = "01:10:34" }
            };
        }
    }
}
