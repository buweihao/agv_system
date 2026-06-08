using System;
using System.Collections.ObjectModel;
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

        public SignalListViewModel()
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            SignalList.Add(new SignalListModel { Id = "SIG_LIFT_TOP", Name = "提升机_上限位", Type = "设备信号", State = "正常", CurrentValue = "1", Device = "LIFT-001", LastUpdatedTime = now });
            SignalList.Add(new SignalListModel { Id = "SIG_DOOR_01", Name = "安全门_01", Type = "安全信号", State = "激活", CurrentValue = "0", Device = "DOOR-01", LastUpdatedTime = now });
            SignalList.Add(new SignalListModel { Id = "SIG_OPTIC_01", Name = "光电开关_01", Type = "传感器", State = "正常", CurrentValue = "1", Device = "SENSOR-01", LastUpdatedTime = now });
            SignalList.Add(new SignalListModel { Id = "SIG_HB_05", Name = "心跳检测_AGV05", Type = "虚拟信号", State = "离线", CurrentValue = "Timeout", Device = "AGV-005", LastUpdatedTime = now });
            SignalList.Add(new SignalListModel { Id = "SIG_ESTOP_A", Name = "急停按钮_A区", Type = "安全信号", State = "异常", CurrentValue = "1", Device = "PANEL-A", LastUpdatedTime = now });
            SignalList.Add(new SignalListModel { Id = "SIG_WEIGHT_02", Name = "称重传感器", Type = "设备信号", State = "正常", CurrentValue = "500kg", Device = "SCALE-02", LastUpdatedTime = now });
            SignalList.Add(new SignalListModel { Id = "SIG_PLC_RDY", Name = "接驳台_就绪", Type = "设备信号", State = "正常", CurrentValue = "1", Device = "CONV-03", LastUpdatedTime = now });
        }
    }
}
