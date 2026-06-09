using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class AlarmEvent
    {
        public string AlarmId { get; set; } = string.Empty;

        public string AlarmCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;

        public AlarmState State { get; set; } = AlarmState.Active;

        public string SourceType { get; set; } = string.Empty;

        public string SourceId { get; set; } = string.Empty;

        public string? VehicleId { get; set; }

        public string? TaskId { get; set; }

        public string? LocationNodeId { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.Now;

        public DateTime? AcknowledgedAt { get; set; }

        public string? AcknowledgedBy { get; set; }

        public DateTime? ClearedAt { get; set; }

        public string? ClearReason { get; set; }

        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
