namespace AgvDispatcher.Core.Models
{
    public class OperationLogQuery
    {
        public string? Category { get; set; }

        public string? Action { get; set; }

        public string? Operator { get; set; }

        public string? VehicleId { get; set; }

        public string? TaskId { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; } = 100;
    }
}
