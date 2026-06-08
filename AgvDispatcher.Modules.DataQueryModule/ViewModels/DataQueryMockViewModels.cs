using System.Collections.ObjectModel;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.DataQueryModule.ViewModels
{
    // Task Data ViewModel (reuses CenterPanelViewModel data)
    public class DataQueryTaskDataViewModel : DataQueryCenterPanelViewModel
    {
    }

    // 充电数据模型
    public class ChargeDataModel
    {
        public int Seq { get; set; }
        public string Time { get; set; }
        public string AgvId { get; set; }
        public string ChargeStation { get; set; }
        public string StartBattery { get; set; }
        public string EndBattery { get; set; }
        public string ChargeDuration { get; set; }
        public string ChargeAmount { get; set; }
    }

    // 告警数据模型
    public class AlarmDataModel
    {
        public int Seq { get; set; }
        public string Time { get; set; }
        public string AgvId { get; set; }
        public string AlarmLevel { get; set; }
        public string AlarmCode { get; set; }
        public string AlarmDesc { get; set; }
        public string Status { get; set; }
    }

    // 交互数据模型
    public class InteractionDataModel
    {
        public int Seq { get; set; }
        public string Time { get; set; }
        public string AgvId { get; set; }
        public string DeviceId { get; set; }
        public string ActionType { get; set; }
        public string SignalContent { get; set; }
        public string Result { get; set; }
    }

    // 能耗数据模型
    public class EnergyDataModel
    {
        public int Seq { get; set; }
        public string Date { get; set; }
        public string AgvId { get; set; }
        public string WorkDuration { get; set; }
        public string ConsumeEnergy { get; set; }
        public string ChargeEnergy { get; set; }
        public string EnergyEfficiency { get; set; }
    }

    // 设备日志模型
    public class DeviceLogModel
    {
        public int Seq { get; set; }
        public string Time { get; set; }
        public string DeviceType { get; set; }
        public string DeviceId { get; set; }
        public string LogLevel { get; set; }
        public string LogContent { get; set; }
    }

    // 各种ViewModel
    public class DataQueryChargeDataViewModel : BindableBase
    {
        public ObservableCollection<ChargeDataModel> DataList { get; set; } = new();
        public DataQueryChargeDataViewModel() {
            DataList.Add(new ChargeDataModel { Seq = 1, Time = "2024-01-12 10:20:00", AgvId = "AGV-001", ChargeStation = "CHG-01", StartBattery = "20%", EndBattery = "100%", ChargeDuration = "01:20:00", ChargeAmount = "4.5 kWh" });
            DataList.Add(new ChargeDataModel { Seq = 2, Time = "2024-01-12 11:45:00", AgvId = "AGV-002", ChargeStation = "CHG-02", StartBattery = "15%", EndBattery = "100%", ChargeDuration = "01:30:00", ChargeAmount = "5.0 kWh" });
            DataList.Add(new ChargeDataModel { Seq = 3, Time = "2024-01-12 13:10:00", AgvId = "AGV-003", ChargeStation = "CHG-01", StartBattery = "25%", EndBattery = "80%", ChargeDuration = "00:55:00", ChargeAmount = "3.2 kWh" });
            DataList.Add(new ChargeDataModel { Seq = 4, Time = "2024-01-12 14:05:00", AgvId = "AGV-004", ChargeStation = "CHG-03", StartBattery = "10%", EndBattery = "90%", ChargeDuration = "01:15:00", ChargeAmount = "4.8 kWh" });
        }
    }

    public class DataQueryAlarmDataViewModel : BindableBase
    {
        public ObservableCollection<AlarmDataModel> DataList { get; set; } = new();
        public DataQueryAlarmDataViewModel() {
            DataList.Add(new AlarmDataModel { Seq = 1, Time = "2024-01-12 09:15:22", AgvId = "AGV-001", AlarmLevel = "严重", AlarmCode = "ERR-001", AlarmDesc = "驱动器通信故障", Status = "已处理" });
            DataList.Add(new AlarmDataModel { Seq = 2, Time = "2024-01-12 10:32:11", AgvId = "AGV-003", AlarmLevel = "警告", AlarmCode = "WARN-045", AlarmDesc = "激光雷达视野被遮挡", Status = "已恢复" });
            DataList.Add(new AlarmDataModel { Seq = 3, Time = "2024-01-12 11:20:05", AgvId = "AGV-002", AlarmLevel = "一般", AlarmCode = "INFO-012", AlarmDesc = "电量低于20%", Status = "未处理" });
        }
    }

    public class DataQueryInteractionDataViewModel : BindableBase
    {
        public ObservableCollection<InteractionDataModel> DataList { get; set; } = new();
        public DataQueryInteractionDataViewModel() {
            DataList.Add(new InteractionDataModel { Seq = 1, Time = "2024-01-12 10:05:10", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "开门请求", SignalContent = "OpenDoor", Result = "成功" });
            DataList.Add(new InteractionDataModel { Seq = 2, Time = "2024-01-12 10:05:15", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "状态查询", SignalContent = "GetStatus", Result = "Opened" });
            DataList.Add(new InteractionDataModel { Seq = 3, Time = "2024-01-12 10:06:20", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "关门请求", SignalContent = "CloseDoor", Result = "成功" });
        }
    }

    public class DataQueryEnergyDataViewModel : BindableBase
    {
        public ObservableCollection<EnergyDataModel> DataList { get; set; } = new();
        public DataQueryEnergyDataViewModel() {
            DataList.Add(new EnergyDataModel { Seq = 1, Date = "2024-01-12", AgvId = "AGV-001", WorkDuration = "14.5 h", ConsumeEnergy = "12.5 kWh", ChargeEnergy = "13.0 kWh", EnergyEfficiency = "95%" });
            DataList.Add(new EnergyDataModel { Seq = 2, Date = "2024-01-12", AgvId = "AGV-002", WorkDuration = "16.2 h", ConsumeEnergy = "14.2 kWh", ChargeEnergy = "15.0 kWh", EnergyEfficiency = "92%" });
            DataList.Add(new EnergyDataModel { Seq = 3, Date = "2024-01-12", AgvId = "AGV-003", WorkDuration = "12.8 h", ConsumeEnergy = "10.5 kWh", ChargeEnergy = "11.2 kWh", EnergyEfficiency = "96%" });
        }
    }

    public class DataQueryDeviceLogViewModel : BindableBase
    {
        public ObservableCollection<DeviceLogModel> DataList { get; set; } = new();
        public DataQueryDeviceLogViewModel() {
            DataList.Add(new DeviceLogModel { Seq = 1, Time = "2024-01-12 08:00:00", DeviceType = "服务器", DeviceId = "Server-01", LogLevel = "INFO", LogContent = "调度系统启动成功" });
            DataList.Add(new DeviceLogModel { Seq = 2, Time = "2024-01-12 08:05:12", DeviceType = "AGV", DeviceId = "AGV-001", LogLevel = "INFO", LogContent = "AGV上线，当前电量85%" });
            DataList.Add(new DeviceLogModel { Seq = 3, Time = "2024-01-12 09:15:22", DeviceType = "AGV", DeviceId = "AGV-001", LogLevel = "ERROR", LogContent = "驱动器通信异常，停止运行" });
        }
    }
}
