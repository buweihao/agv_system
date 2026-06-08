using System;
using System.Collections.ObjectModel;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.DataQueryModule.ViewModels;
using Prism.Events;

namespace AgvDispatcher.Modules.DataQueryModule.Services
{
    public class LowBatteryEventRecordStore
    {
        private static bool _seeded;

        public static ObservableCollection<AlarmDataModel> Records { get; } = new();

        public LowBatteryEventRecordStore(IEventAggregator eventAggregator)
        {
            EnsureSeeded();

            eventAggregator
                .GetEvent<RobotLowBatteryEvent>()
                .Subscribe(AddLowBatteryRecord, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
        }

        public static void EnsureSeeded()
        {
            if (_seeded)
            {
                return;
            }

            _seeded = true;

            Records.Add(new AlarmDataModel { Seq = 1, Time = "2024-01-12 09:15:22", AgvId = "AGV-001", AlarmLevel = "严重", AlarmCode = "ERR-001", AlarmDesc = "驱动器通信故障", Status = "已处理" });
            Records.Add(new AlarmDataModel { Seq = 2, Time = "2024-01-12 10:32:11", AgvId = "AGV-003", AlarmLevel = "警告", AlarmCode = "WARN-045", AlarmDesc = "激光雷达视野被遮挡", Status = "已恢复" });
            Records.Add(new AlarmDataModel { Seq = 3, Time = "2024-01-12 11:20:05", AgvId = "AGV-002", AlarmLevel = "一般", AlarmCode = "INFO-012", AlarmDesc = "电量低于20%", Status = "未处理" });
        }

        private static void AddLowBatteryRecord(RobotBatteryAlert alert)
        {
            EnsureSeeded();

            Records.Insert(0, new AlarmDataModel
            {
                Seq = Records.Count + 1,
                Time = alert.OccurredAt == default ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : alert.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                AgvId = alert.VehicleId,
                AlarmLevel = "警告",
                AlarmCode = "LOW-BATTERY",
                AlarmDesc = $"电量 {alert.BatteryLevel:0.#}% 低于阈值 {alert.Threshold:0.#}%，位置：{alert.Location}",
                Status = "未处理"
            });
        }
    }
}
