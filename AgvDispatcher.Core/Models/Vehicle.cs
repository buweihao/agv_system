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

        public string Remark { get; set; } = string.Empty;
    }
}
