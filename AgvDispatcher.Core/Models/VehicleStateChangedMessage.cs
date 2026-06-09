using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class VehicleStateChangedMessage
    {
        public VehicleStateChangeType ChangeType { get; set; }

        public VehicleStatusSnapshot? Snapshot { get; set; }

        public string? RemovedVehicleId { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.Now;
    }
}
