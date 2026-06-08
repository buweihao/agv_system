using System;
using System.Collections.ObjectModel;
using AgvDispatcher.Modules.SignalModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SignalModule.ViewModels
{
    public class SignalLogViewModel : BindableBase
    {
        private ObservableCollection<SignalLogModel> _logList = new();
        public ObservableCollection<SignalLogModel> LogList
        {
            get => _logList;
            set => SetProperty(ref _logList, value);
        }

        public SignalLogViewModel()
        {
            string now = DateTime.Now.ToString("HH:mm:ss.fff");
            
            LogList.Add(new SignalLogModel { LogTime = now, SignalName = "提升机_上限位", SignalId = "SIG_LIFT_TOP", SignalType = "设备信号", StateChange = "正常 -> 异常", TriggerValue = "1", Device = "LIFT-001", ResponseTime = "24ms", Result = "成功" });
            LogList.Add(new SignalLogModel { LogTime = now, SignalName = "安全门_01", SignalId = "SIG_DOOR_01", SignalType = "安全信号", StateChange = "正常 -> 激活", TriggerValue = "0", Device = "DOOR-01", ResponseTime = "12ms", Result = "成功" });
            LogList.Add(new SignalLogModel { LogTime = now, SignalName = "光电开关_01", SignalId = "SIG_OPTIC_01", SignalType = "传感器", StateChange = "激活 -> 正常", TriggerValue = "1", Device = "SENSOR-01", ResponseTime = "32ms", Result = "成功" });
            LogList.Add(new SignalLogModel { LogTime = now, SignalName = "心跳检测_AGV05", SignalId = "SIG_HB_05", SignalType = "虚拟信号", StateChange = "正常 -> 离线", TriggerValue = "Timeout", Device = "AGV-005", ResponseTime = "5000ms", Result = "失败" });
            LogList.Add(new SignalLogModel { LogTime = now, SignalName = "急停按钮_A区", SignalId = "SIG_ESTOP_A", SignalType = "安全信号", StateChange = "正常 -> 激活", TriggerValue = "1", Device = "PANEL-A", ResponseTime = "8ms", Result = "成功" });
            LogList.Add(new SignalLogModel { LogTime = now, SignalName = "称重传感器", SignalId = "SIG_WEIGHT_02", SignalType = "设备信号", StateChange = "异常 -> 正常", TriggerValue = "500kg", Device = "SCALE-02", ResponseTime = "45ms", Result = "成功" });
        }
    }
}
