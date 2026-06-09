namespace AgvDispatcher.Core.Models
{
    public class TaskRunRecord
    {
        public int Seq { get; set; }
        public string QueryTime { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string StartPoint { get; set; } = string.Empty;
        public string EndPoint { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Distance { get; set; } = string.Empty;
        public string AvgSpeed { get; set; } = string.Empty;
    }

    public class ChargeRecord
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

    public class AlarmRecord
    {
        public int Seq { get; set; }
        public string Time { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string AlarmLevel { get; set; } = string.Empty;
        public string AlarmCode { get; set; } = string.Empty;
        public string AlarmDesc { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class InteractionRecord
    {
        public int Seq { get; set; }
        public string Time { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string SignalContent { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
    }

    public class EnergyRecord
    {
        public int Seq { get; set; }
        public string Date { get; set; } = string.Empty;
        public string AgvId { get; set; } = string.Empty;
        public string WorkDuration { get; set; } = string.Empty;
        public string ConsumeEnergy { get; set; } = string.Empty;
        public string ChargeEnergy { get; set; } = string.Empty;
        public string EnergyEfficiency { get; set; } = string.Empty;
    }

    public class DeviceLogRecord
    {
        public int Seq { get; set; }
        public string Time { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty;
        public string LogContent { get; set; } = string.Empty;
    }
}
