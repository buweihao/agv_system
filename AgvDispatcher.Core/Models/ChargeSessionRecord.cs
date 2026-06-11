using System;

namespace AgvDispatcher.Core.Models
{
    public class ChargeSessionRecord
    {
        public string SessionId { get; set; } = string.Empty;

        public string StationId { get; set; } = string.Empty;

        public string VehicleId { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public double StartBatteryLevel { get; set; }

        public double EndBatteryLevel { get; set; }

        public double EnergyConsumedKwh { get; set; }

        public string Status { get; set; } = string.Empty; // e.g. "Charging", "Completed", "Faulted"

        public string FaultReason { get; set; } = string.Empty;

        public string Remark { get; set; } = string.Empty;
    }
}
