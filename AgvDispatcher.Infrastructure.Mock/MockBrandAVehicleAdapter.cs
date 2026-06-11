using AgvDispatcher.Core.Models;
using System;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockBrandAVehicleAdapter : MockVehicleAdapterBase
    {
        public MockBrandAVehicleAdapter(Vehicle vehicle) : base(vehicle)
        {
        }

        // Brand A is standard, 5 seconds tick, 0.2% battery drain per tick
        protected override double BatteryDrainPerTick => 0.2;
        protected override TimeSpan TickInterval => TimeSpan.FromSeconds(5);
    }
}
