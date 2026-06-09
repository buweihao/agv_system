using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockDataQueryService : IDataQueryService
    {
        public IReadOnlyList<TaskRunRecord> GetTaskRunRecords()
        {
            return new[]
            {
                new TaskRunRecord { Seq = 1, QueryTime = "2024-01-12 10:20:30", AgvId = "AGV-001", TaskId = "TSK-001201", TaskType = "搬运任务", StartPoint = "A01-01", EndPoint = "C03-05", Status = "已完成", Duration = "00:15:20", Distance = "1.2", AvgSpeed = "1.3" },
                new TaskRunRecord { Seq = 2, QueryTime = "2024-01-12 10:25:10", AgvId = "AGV-002", TaskId = "TSK-001202", TaskType = "充电任务", StartPoint = "B02-01", EndPoint = "CHG-01", Status = "执行中", Duration = "00:05:10", Distance = "0.5", AvgSpeed = "1.6" },
                new TaskRunRecord { Seq = 3, QueryTime = "2024-01-12 10:30:00", AgvId = "AGV-003", TaskId = "TSK-001203", TaskType = "巡检任务", StartPoint = "C01-01", EndPoint = "A01-01", Status = "已完成", Duration = "00:45:00", Distance = "3.5", AvgSpeed = "1.3" },
                new TaskRunRecord { Seq = 4, QueryTime = "2024-01-12 10:45:30", AgvId = "AGV-001", TaskId = "TSK-001204", TaskType = "搬运任务", StartPoint = "C03-05", EndPoint = "B01-02", Status = "已完成", Duration = "00:12:40", Distance = "1.0", AvgSpeed = "1.3" },
                new TaskRunRecord { Seq = 5, QueryTime = "2024-01-12 11:00:20", AgvId = "AGV-004", TaskId = "TSK-001205", TaskType = "其他任务", StartPoint = "D01-01", EndPoint = "D01-10", Status = "执行中", Duration = "00:08:15", Distance = "0.8", AvgSpeed = "1.6" }
            };
        }

        public IReadOnlyList<ChargeRecord> GetChargeRecords()
        {
            return new[]
            {
                new ChargeRecord { Seq = 1, Time = "2024-01-12 10:20:00", AgvId = "AGV-001", ChargeStation = "CHG-01", StartBattery = "20%", EndBattery = "100%", ChargeDuration = "01:20:00", ChargeAmount = "4.5 kWh" },
                new ChargeRecord { Seq = 2, Time = "2024-01-12 11:45:00", AgvId = "AGV-002", ChargeStation = "CHG-02", StartBattery = "15%", EndBattery = "100%", ChargeDuration = "01:30:00", ChargeAmount = "5.0 kWh" },
                new ChargeRecord { Seq = 3, Time = "2024-01-12 13:10:00", AgvId = "AGV-003", ChargeStation = "CHG-01", StartBattery = "25%", EndBattery = "80%", ChargeDuration = "00:55:00", ChargeAmount = "3.2 kWh" }
            };
        }

        public IReadOnlyList<AlarmRecord> GetAlarmRecords()
        {
            return new[]
            {
                new AlarmRecord { Seq = 1, Time = "2024-01-12 09:15:22", AgvId = "AGV-001", AlarmLevel = "严重", AlarmCode = "ERR-001", AlarmDesc = "驱动器通信故障", Status = "已处理" },
                new AlarmRecord { Seq = 2, Time = "2024-01-12 10:32:11", AgvId = "AGV-003", AlarmLevel = "警告", AlarmCode = "WARN-045", AlarmDesc = "激光雷达视野被遮挡", Status = "已恢复" },
                new AlarmRecord { Seq = 3, Time = "2024-01-12 11:20:05", AgvId = "AGV-002", AlarmLevel = "一般", AlarmCode = "INFO-012", AlarmDesc = "电量低于20%", Status = "未处理" }
            };
        }

        public IReadOnlyList<InteractionRecord> GetInteractionRecords()
        {
            return new[]
            {
                new InteractionRecord { Seq = 1, Time = "2024-01-12 10:05:10", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "开门请求", SignalContent = "OpenDoor", Result = "成功" },
                new InteractionRecord { Seq = 2, Time = "2024-01-12 10:05:15", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "状态查询", SignalContent = "GetStatus", Result = "Opened" },
                new InteractionRecord { Seq = 3, Time = "2024-01-12 10:06:20", AgvId = "AGV-001", DeviceId = "Door-A1", ActionType = "关门请求", SignalContent = "CloseDoor", Result = "成功" }
            };
        }

        public IReadOnlyList<EnergyRecord> GetEnergyRecords()
        {
            return new[]
            {
                new EnergyRecord { Seq = 1, Date = "2024-01-12", AgvId = "AGV-001", WorkDuration = "14.5 h", ConsumeEnergy = "12.5 kWh", ChargeEnergy = "13.0 kWh", EnergyEfficiency = "95%" },
                new EnergyRecord { Seq = 2, Date = "2024-01-12", AgvId = "AGV-002", WorkDuration = "16.2 h", ConsumeEnergy = "14.2 kWh", ChargeEnergy = "15.0 kWh", EnergyEfficiency = "92%" },
                new EnergyRecord { Seq = 3, Date = "2024-01-12", AgvId = "AGV-003", WorkDuration = "12.8 h", ConsumeEnergy = "10.5 kWh", ChargeEnergy = "11.2 kWh", EnergyEfficiency = "96%" }
            };
        }

        public IReadOnlyList<DeviceLogRecord> GetDeviceLogRecords()
        {
            return new[]
            {
                new DeviceLogRecord { Seq = 1, Time = "2024-01-12 08:00:00", DeviceType = "服务器", DeviceId = "Server-01", LogLevel = "INFO", LogContent = "调度系统启动成功" },
                new DeviceLogRecord { Seq = 2, Time = "2024-01-12 08:05:12", DeviceType = "AGV", DeviceId = "AGV-001", LogLevel = "INFO", LogContent = "AGV上线，当前电量85%" },
                new DeviceLogRecord { Seq = 3, Time = "2024-01-12 09:15:22", DeviceType = "AGV", DeviceId = "AGV-001", LogLevel = "ERROR", LogContent = "驱动器通信异常，停止运行" }
            };
        }
    }
}
