using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class ChargeStation
    {
        public string StationId { get; set; } = string.Empty;

        public string StationCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string AreaCode { get; set; } = string.Empty;

        public string NodeId { get; set; } = string.Empty;

        public MapPosition Position { get; set; } = new();

        public ChargeStationState State { get; set; } = ChargeStationState.Available;

        public bool IsEnabled { get; set; } = true;

        public string? BoundVehicleId { get; set; }

        public string? ReservedVehicleId { get; set; }

        public double RatedPowerKw { get; set; }

        public double OutputVoltage { get; set; }

        public double OutputCurrent { get; set; }

        public double ConnectorTemperature { get; set; }

        public double QueueWeight { get; set; }

        public DateTime? LastHeartbeatAt { get; set; }

        public DateTime? LastMaintenanceAt { get; set; }

        public string AllowedBrands { get; set; } = string.Empty;

        public string ProtocolType { get; set; } = string.Empty;

        public string Endpoint { get; set; } = string.Empty;

        public int Port { get; set; }

        public string Remark { get; set; } = string.Empty;

        public string ConnectorType { get; set; } = string.Empty;

        public string SupportedVehicleTypes { get; set; } = string.Empty;

        public bool SupportAutoCharge { get; set; }

        public int MaxQueueCount { get; set; } = 1;

        public bool IsExclusive { get; set; }
    }
}
