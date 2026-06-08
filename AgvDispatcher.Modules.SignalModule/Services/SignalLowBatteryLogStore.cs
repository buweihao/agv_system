using System;
using System.Collections.ObjectModel;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.SignalModule.Models;
using Prism.Events;

namespace AgvDispatcher.Modules.SignalModule.Services
{
    public class SignalLowBatteryLogStore
    {
        private static bool _seeded;

        public static ObservableCollection<SignalLogModel> Logs { get; } = new();

        public SignalLowBatteryLogStore(IEventAggregator eventAggregator)
        {
            EnsureSeeded();

            eventAggregator
                .GetEvent<RobotLowBatteryEvent>()
                .Subscribe(AddLowBatteryLog, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
        }

        public static void EnsureSeeded()
        {
            if (_seeded)
            {
                return;
            }

            _seeded = true;
            string now = DateTime.Now.ToString("HH:mm:ss.fff");

            Logs.Add(new SignalLogModel { LogTime = now, SignalName = "提升机_上限位", SignalId = "SIG_LIFT_TOP", SignalType = "设备信号", StateChange = "正常 -> 异常", TriggerValue = "1", Device = "LIFT-001", ResponseTime = "24ms", Result = "成功" });
            Logs.Add(new SignalLogModel { LogTime = now, SignalName = "安全门_01", SignalId = "SIG_DOOR_01", SignalType = "安全信号", StateChange = "正常 -> 激活", TriggerValue = "0", Device = "DOOR-01", ResponseTime = "12ms", Result = "成功" });
            Logs.Add(new SignalLogModel { LogTime = now, SignalName = "光电开关_01", SignalId = "SIG_OPTIC_01", SignalType = "传感器", StateChange = "激活 -> 正常", TriggerValue = "1", Device = "SENSOR-01", ResponseTime = "32ms", Result = "成功" });
            Logs.Add(new SignalLogModel { LogTime = now, SignalName = "心跳检测_AGV05", SignalId = "SIG_HB_05", SignalType = "虚拟信号", StateChange = "正常 -> 离线", TriggerValue = "Timeout", Device = "AGV-005", ResponseTime = "5000ms", Result = "失败" });
            Logs.Add(new SignalLogModel { LogTime = now, SignalName = "急停按钮_A区", SignalId = "SIG_ESTOP_A", SignalType = "安全信号", StateChange = "正常 -> 激活", TriggerValue = "1", Device = "PANEL-A", ResponseTime = "8ms", Result = "成功" });
            Logs.Add(new SignalLogModel { LogTime = now, SignalName = "称重传感器", SignalId = "SIG_WEIGHT_02", SignalType = "设备信号", StateChange = "异常 -> 正常", TriggerValue = "500kg", Device = "SCALE-02", ResponseTime = "45ms", Result = "成功" });
        }

        private static void AddLowBatteryLog(RobotBatteryAlert alert)
        {
            EnsureSeeded();

            Logs.Insert(0, new SignalLogModel
            {
                LogTime = alert.OccurredAt == default ? DateTime.Now.ToString("HH:mm:ss.fff") : alert.OccurredAt.ToString("HH:mm:ss.fff"),
                SignalName = "AGV低电量联动",
                SignalId = $"LOW_BATTERY_{alert.VehicleId}",
                SignalType = "联动事件",
                StateChange = "正常 -> 告警",
                TriggerValue = $"{alert.BatteryLevel:0.#}% <= {alert.Threshold:0.#}%",
                Device = string.IsNullOrWhiteSpace(alert.VehicleId) ? alert.Brand : alert.VehicleId,
                ResponseTime = "0ms",
                Result = "已记录"
            });
        }
    }
}
