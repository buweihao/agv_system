using AgvDispatcher.Core.Models;
using System;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockBrandCVehicleAdapter : MockVehicleAdapterBase
    {
        public MockBrandCVehicleAdapter(Vehicle vehicle) : base(vehicle)
        {
        }

        // Brand C is heavy duty, 8 seconds tick, 0.5% battery drain per tick
        protected override double BatteryDrainPerTick => 0.5;
        protected override TimeSpan TickInterval => TimeSpan.FromSeconds(8);
    }
}
