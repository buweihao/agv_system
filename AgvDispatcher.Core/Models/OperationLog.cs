namespace AgvDispatcher.Core.Models
{
    public class OperationLog
    {
        public string LogId { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Operator { get; set; }

        public string? VehicleId { get; set; }

        public string? TaskId { get; set; }

        public string? SourceId { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.Now;

        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
