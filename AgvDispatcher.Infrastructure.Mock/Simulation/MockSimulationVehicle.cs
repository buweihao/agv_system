namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public sealed class MockSimulationVehicle
    {
        public string VehicleId { get; init; } = string.Empty;

        public string Brand { get; init; } = "Mock";

        public string StartNodeId { get; init; } = string.Empty;

        public double BatteryLevel { get; init; } = 100;

        public bool IsOnline { get; init; } = true;

        public MockSimulationVehicle Clone() => new()
        {
            VehicleId = VehicleId,
            Brand = Brand,
            StartNodeId = StartNodeId,
            BatteryLevel = BatteryLevel,
            IsOnline = IsOnline
        };
    }
}
