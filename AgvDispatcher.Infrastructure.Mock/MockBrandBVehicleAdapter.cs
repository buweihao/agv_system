using AgvDispatcher.Core.Models;
using System;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockBrandBVehicleAdapter : MockVehicleAdapterBase
    {
        public MockBrandBVehicleAdapter(Vehicle vehicle) : base(vehicle)
        {
        }

        // Brand B is faster and more efficient, 3 seconds tick, 0.1% battery drain per tick
        protected override double BatteryDrainPerTick => 0.1;
        protected override TimeSpan TickInterval => TimeSpan.FromSeconds(3);
    }
}
