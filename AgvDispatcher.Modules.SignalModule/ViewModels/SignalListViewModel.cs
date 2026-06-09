using System;
using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.SignalModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SignalModule.ViewModels
{
    public class SignalListViewModel : BindableBase
    {
        private ObservableCollection<SignalListModel> _signalList = new();
        public ObservableCollection<SignalListModel> SignalList
        {
            get => _signalList;
            set => SetProperty(ref _signalList, value);
        }

        public SignalListViewModel(ISignalService signalService)
        {
            SignalList = new ObservableCollection<SignalListModel>(
                signalService.GetSignals().Select(ToSignalListModel));
        }

        private static SignalListModel ToSignalListModel(SignalPoint signal)
        {
            return new SignalListModel
            {
                Id = signal.SignalId,
                Name = signal.Name,
                Type = FormatPointType(signal.PointType),
                State = FormatState(signal.State),
                CurrentValue = signal.CurrentValue,
                Device = string.IsNullOrWhiteSpace(signal.Remark) ? signal.SignalCode : signal.Remark,
                LastUpdatedTime = (signal.LastChangedAt ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        private static string FormatPointType(SignalPointType type)
        {
            return type switch
            {
                SignalPointType.Sensor => "传感器",
                SignalPointType.Button => "按钮",
                SignalPointType.Door => "安全信号",
                SignalPointType.Elevator => "设备信号",
                SignalPointType.Conveyor => "设备信号",
                SignalPointType.TrafficLight => "交通信号",
                SignalPointType.SafetyInterlock => "安全联锁",
                _ => "虚拟信号"
            };
        }

        private static string FormatState(SignalState state)
        {
            return state switch
            {
                SignalState.Normal => "正常",
                SignalState.Active => "激活",
                SignalState.Abnormal => "异常",
                SignalState.Offline => "离线",
                _ => state.ToString()
            };
        }
    }
}
