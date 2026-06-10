using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class AlarmQuery
    {
        public AlarmSeverity? Severity { get; set; }

        public AlarmState? State { get; set; }

        public string? AlarmCode { get; set; }

        public string? SourceType { get; set; }

        public string? SourceId { get; set; }

        public string? VehicleId { get; set; }

        public string? TaskId { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; } = 100;
    }
}
