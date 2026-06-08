using System.Collections.ObjectModel;
using AgvDispatcher.Modules.DataQueryModule.Services;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.DataQueryModule.ViewModels
{
    public class DataQueryTaskDataViewModel : DataQueryCenterPanelViewModel
    {
    }

    public class ChargeDataModel
    {
        public int Seq { get; set; }
        public string Time { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string ChargeStation { get; set; } = string.Empty;
        public string StartBattery { get; set; } = string.Empty;
        public string EndBattery { get; set; } = string.Empty;
        public string ChargeDuration { get; set; } = string.Empty;
        public string ChargeAmount { get; set; } = string.Empty;
    }

    public class AlarmDataModel
    {
        public int Seq { get; set; }
        public string Time { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string AlarmLevel { get; set; } = string.Empty;
        public string AlarmCode { get; set; } = string.Empty;
        public string AlarmDesc { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class InteractionDataModel
    {
        public int Seq { get; set; }
        public string Time { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string SignalContent { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
    }

    public class EnergyDataModel
    {
        public int Seq { get; set; }
        public string Date { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string WorkDuration { get; set; } = string.Empty;
        public string ConsumeEnergy { get; set; } = string.Empty;
        public string ChargeEnergy { get; set; } = string.Empty;
        public string EnergyEfficiency { get; set; } = string.Empty;
    }

    public class DeviceLogModel
    {
        public int Seq { get; set; }
        public string Time { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty;
        public string LogContent { get; set; } = string.Empty;
    }

    public class DataQueryChargeDataViewModel : BindableBase
    {
        public ObservableCollection<ChargeDataModel> DataList { get; set; } = new();

        public DataQueryChargeDataViewModel()
        {
            DataList.Add(new ChargeDataModel { Seq = 1, Time = "2024-01-12 10:20:00", AgvId = "AGV-001", ChargeStation = "CHG-01", StartBattery = "20%", EndBattery = "100%", ChargeDuration = "01:20:00", ChargeAmount = "4.5 kWh" });
            DataList.Add(new ChargeDataModel { Seq = 2, Time = "2024-01-12 11:45:00", AgvId = "AGV-002", ChargeStation = "CHG-02", StartBattery = "15%", EndBattery = "100%", ChargeDuration = "01:30:00", ChargeAmount = "5.0 kWh" });
            DataList.Add(new ChargeDataModel { Seq = 3, Time = "2024-01-12 13:10:00", AgvId = "AGV-003", ChargeStation = "CHG-01", StartBattery = "25%", EndBattery = "80%", ChargeDuration = "00:55:00", ChargeAmount = "3.2 kWh" });
            DataList.Add(new ChargeDataModel { Seq = 4, Time = "2024-01-12 14:05:00", AgvId = "AGV-004", ChargeStation = "CHG-03", StartBattery = "10%", EndBattery = "90%", ChargeDuration = "01:15:00", ChargeAmount = "4.8 kWh" });
        }
    }

    public class DataQueryAlarmDataViewModel : BindableBase
    {
        public ObservableCollection<AlarmDataModel> DataList { get; set; } = new();

        public DataQueryAlarmDataViewModel()
        {
            LowBatteryEventRecordStore.EnsureSeeded();
            DataList = LowBatteryEventRecordStore.Records;
        }
    }

    public class DataQueryInteractionDataViewModel : BindableBase
    {
        public ObservableCollection<InteractionDataModel> DataList { get; set; } = new();

        public DataQueryInteractionDataViewModel()
        {
            DataList.Add(new InteractionDataModel { Seq = 1, Time = "2024-01-12 10:05:10", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "开门请求", SignalContent = "OpenDoor", Result = "成功" });
            DataList.Add(new InteractionDataModel { Seq = 2, Time = "2024-01-12 10:05:15", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "状态查询", SignalContent = "GetStatus", Result = "Opened" });
            DataList.Add(new InteractionDataModel { Seq = 3, Time = "2024-01-12 10:06:20", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "关门请求", SignalContent = "CloseDoor", Result = "成功" });
        }
    }

    public class DataQueryEnergyDataViewModel : BindableBase
    {
        public ObservableCollection<EnergyDataModel> DataList { get; set; } = new();

        public DataQueryEnergyDataViewModel()
        {
            DataList.Add(new EnergyDataModel { Seq = 1, Date = "2024-01-12", AgvId = "AGV-001", WorkDuration = "14.5 h", ConsumeEnergy = "12.5 kWh", ChargeEnergy = "13.0 kWh", EnergyEfficiency = "95%" });
            DataList.Add(new EnergyDataModel { Seq = 2, Date = "2024-01-12", AgvId = "AGV-002", WorkDuration = "16.2 h", ConsumeEnergy = "14.2 kWh", ChargeEnergy = "15.0 kWh", EnergyEfficiency = "92%" });
            DataList.Add(new EnergyDataModel { Seq = 3, Date = "2024-01-12", AgvId = "AGV-003", WorkDuration = "12.8 h", ConsumeEnergy = "10.5 kWh", ChargeEnergy = "11.2 kWh", EnergyEfficiency = "96%" });
        }
    }

    public class DataQueryDeviceLogViewModel : BindableBase
    {
        public ObservableCollection<DeviceLogModel> DataList { get; set; } = new();

        public DataQueryDeviceLogViewModel()
        {
            DataList.Add(new DeviceLogModel { Seq = 1, Time = "2024-01-12 08:00:00", DeviceType = "服务器", DeviceId = "Server-01", LogLevel = "INFO", LogContent = "调度系统启动成功" });
            DataList.Add(new DeviceLogModel { Seq = 2, Time = "2024-01-12 08:05:12", DeviceType = "AGV", DeviceId = "AGV-001", LogLevel = "INFO", LogContent = "AGV上线，当前电量85%" });
            DataList.Add(new DeviceLogModel { Seq = 3, Time = "2024-01-12 09:15:22", DeviceType = "AGV", DeviceId = "AGV-001", LogLevel = "ERROR", LogContent = "驱动器通信异常，停止运行" });
        }
    }
}
