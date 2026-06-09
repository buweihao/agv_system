namespace AgvDispatcher.Core.Models
{
    public class ChargeRecommendation
    {
        public string VehicleId { get; set; } = string.Empty;

        public string? StationId { get; set; }

        public string? StationName { get; set; }

        public bool HasAvailableStation { get; set; }

        public double BatteryLevel { get; set; }

        public double EstimatedDistance { get; set; }

        public int QueueLength { get; set; }

        public string Reason { get; set; } = string.Empty;

        public DateTime RecommendedAt { get; set; } = DateTime.Now;
    }
}
