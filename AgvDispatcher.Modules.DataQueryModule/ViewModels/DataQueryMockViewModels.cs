using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.DataQueryModule.ViewModels
{
    public class DataQueryTaskDataViewModel : DataQueryCenterPanelViewModel
    {
        public DataQueryTaskDataViewModel(IDataQueryService dataQueryService)
            : base(dataQueryService)
        {
        }
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
        public ObservableCollection<ChargeDataModel> DataList { get; set; }

        public DataQueryChargeDataViewModel(IDataQueryService dataQueryService)
        {
            DataList = new ObservableCollection<ChargeDataModel>(
                dataQueryService.GetChargeRecords().Select(ToChargeDataModel));
        }

        private static ChargeDataModel ToChargeDataModel(ChargeRecord record)
        {
            return new ChargeDataModel
            {
                Seq = record.Seq,
                Time = record.Time,
                AgvId = record.AgvId,
                ChargeStation = record.ChargeStation,
                StartBattery = record.StartBattery,
                EndBattery = record.EndBattery,
                ChargeDuration = record.ChargeDuration,
                ChargeAmount = record.ChargeAmount
            };
        }
    }

    public class DataQueryAlarmDataViewModel : BindableBase
    {
        public ObservableCollection<AlarmDataModel> DataList { get; set; }

        public DataQueryAlarmDataViewModel(IDataQueryService dataQueryService)
        {
            DataList = new ObservableCollection<AlarmDataModel>(
                dataQueryService.GetAlarmRecords().Select(ToAlarmDataModel));
        }

        private static AlarmDataModel ToAlarmDataModel(AlarmRecord record)
        {
            return new AlarmDataModel
            {
                Seq = record.Seq,
                Time = record.Time,
                AgvId = record.AgvId,
                AlarmLevel = record.AlarmLevel,
                AlarmCode = record.AlarmCode,
                AlarmDesc = record.AlarmDesc,
                Status = record.Status
            };
        }
    }

    public class DataQueryInteractionDataViewModel : BindableBase
    {
        public ObservableCollection<InteractionDataModel> DataList { get; set; }

        public DataQueryInteractionDataViewModel(IDataQueryService dataQueryService)
        {
            DataList = new ObservableCollection<InteractionDataModel>(
                dataQueryService.GetInteractionRecords().Select(ToInteractionDataModel));
        }

        private static InteractionDataModel ToInteractionDataModel(InteractionRecord record)
        {
            return new InteractionDataModel
            {
                Seq = record.Seq,
                Time = record.Time,
                AgvId = record.AgvId,
                DeviceId = record.DeviceId,
                ActionType = record.ActionType,
                SignalContent = record.SignalContent,
                Result = record.Result
            };
        }
    }

    public class DataQueryEnergyDataViewModel : BindableBase
    {
        public ObservableCollection<EnergyDataModel> DataList { get; set; }

        public DataQueryEnergyDataViewModel(IDataQueryService dataQueryService)
        {
            DataList = new ObservableCollection<EnergyDataModel>(
                dataQueryService.GetEnergyRecords().Select(ToEnergyDataModel));
        }

        private static EnergyDataModel ToEnergyDataModel(EnergyRecord record)
        {
            return new EnergyDataModel
            {
                Seq = record.Seq,
                Date = record.Date,
                AgvId = record.AgvId,
                WorkDuration = record.WorkDuration,
                ConsumeEnergy = record.ConsumeEnergy,
                ChargeEnergy = record.ChargeEnergy,
                EnergyEfficiency = record.EnergyEfficiency
            };
        }
    }

    public class DataQueryDeviceLogViewModel : BindableBase
    {
        public ObservableCollection<DeviceLogModel> DataList { get; set; }

        public DataQueryDeviceLogViewModel(IDataQueryService dataQueryService)
        {
            DataList = new ObservableCollection<DeviceLogModel>(
                dataQueryService.GetDeviceLogRecords().Select(ToDeviceLogModel));
        }

        private static DeviceLogModel ToDeviceLogModel(DeviceLogRecord record)
        {
            return new DeviceLogModel
            {
                Seq = record.Seq,
                Time = record.Time,
                DeviceType = record.DeviceType,
                DeviceId = record.DeviceId,
                LogLevel = record.LogLevel,
                LogContent = record.LogContent
            };
        }
    }
}
