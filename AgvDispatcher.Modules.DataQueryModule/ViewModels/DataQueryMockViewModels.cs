using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Commands;
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
        private readonly IDataQueryService _dataQueryService;
        public ObservableCollection<ChargeDataModel> DataList { get; set; } = new();

        public DelegateCommand RefreshCommand { get; }

        public DataQueryChargeDataViewModel(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
            RefreshCommand = new DelegateCommand(LoadData);
            LoadData();
        }

        private void LoadData()
        {
            DataList.Clear();
            var records = _dataQueryService.GetChargeRecords();
            foreach (var record in records)
            {
                DataList.Add(new ChargeDataModel
                {
                    Seq = record.Seq,
                    Time = record.Time,
                    AgvId = record.AgvId,
                    ChargeStation = record.ChargeStation,
                    StartBattery = record.StartBattery,
                    EndBattery = record.EndBattery,
                    ChargeDuration = record.ChargeDuration,
                    ChargeAmount = record.ChargeAmount
                });
            }
        }
    }

    public class DataQueryAlarmDataViewModel : BindableBase
    {
        public ObservableCollection<AlarmDataModel> DataList { get; set; }

        private readonly IDataQueryService _dataQueryService;
        public DelegateCommand RefreshCommand { get; }

        public DataQueryAlarmDataViewModel(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
            DataList = new ObservableCollection<AlarmDataModel>();
            RefreshCommand = new DelegateCommand(LoadData);
            LoadData();
        }

        private void LoadData()
        {
            DataList.Clear();
            var records = _dataQueryService.GetAlarmRecords();
            foreach (var record in records)
            {
                DataList.Add(ToAlarmDataModel(record));
            }
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

        private readonly IDataQueryService _dataQueryService;
        public DelegateCommand RefreshCommand { get; }

        public DataQueryInteractionDataViewModel(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
            DataList = new ObservableCollection<InteractionDataModel>();
            RefreshCommand = new DelegateCommand(LoadData);
            LoadData();
        }

        private void LoadData()
        {
            DataList.Clear();
            var records = _dataQueryService.GetInteractionRecords();
            foreach (var record in records)
            {
                DataList.Add(ToInteractionDataModel(record));
            }
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

        private readonly IDataQueryService _dataQueryService;
        public DelegateCommand RefreshCommand { get; }

        public DataQueryEnergyDataViewModel(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
            DataList = new ObservableCollection<EnergyDataModel>();
            RefreshCommand = new DelegateCommand(LoadData);
            LoadData();
        }

        private void LoadData()
        {
            DataList.Clear();
            var records = _dataQueryService.GetEnergyRecords();
            foreach (var record in records)
            {
                DataList.Add(ToEnergyDataModel(record));
            }
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

        private readonly IDataQueryService _dataQueryService;
        public DelegateCommand RefreshCommand { get; }

        public DataQueryDeviceLogViewModel(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
            DataList = new ObservableCollection<DeviceLogModel>();
            RefreshCommand = new DelegateCommand(LoadData);
            LoadData();
        }

        private void LoadData()
        {
            DataList.Clear();
            var records = _dataQueryService.GetDeviceLogRecords();
            foreach (var record in records)
            {
                DataList.Add(ToDeviceLogModel(record));
            }
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
