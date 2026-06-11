using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    public class Vehicle
    {
        public string VehicleId { get; set; } = string.Empty;

        public string VehicleCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public VehicleType Type { get; set; } = VehicleType.Agv;

        public string SerialNumber { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;

        public string AreaCode { get; set; } = string.Empty;

        public double MaxSpeed { get; set; }

        public double RatedLoad { get; set; }

        public double Length { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double BatteryCapacityAh { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime? CommissionedAt { get; set; }

        public DateTime? LastMaintenanceAt { get; set; }

        public string AdapterType { get; set; } = string.Empty;
        public string ProtocolType { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public int Port { get; set; }
        public int HeartbeatTimeoutSeconds { get; set; } = 30;
        public string NavigationType { get; set; } = string.Empty;
        public string LoadMode { get; set; } = string.Empty;
        public VehicleCapability CapabilityFlags { get; set; } = VehicleCapability.None;
        public VehicleCommandCapability SupportedCommandFlags { get; set; } = VehicleCommandCapability.None;
        public string HomeNodeId { get; set; } = string.Empty;
        public string ChargeNodeId { get; set; } = string.Empty;
        public double? MinDispatchBattery { get; set; }

        public string Remark { get; set; } = string.Empty;
    }
}
