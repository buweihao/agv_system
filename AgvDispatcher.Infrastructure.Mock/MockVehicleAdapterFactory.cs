using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockVehicleAdapterFactory : IVehicleAdapterFactory
    {
        public bool CanCreate(Vehicle vehicle)
        {
            return vehicle.IsEnabled;
        }

        public IVehicleAdapter Create(Vehicle vehicle)
        {
            return new MockVehicleAdapter(vehicle);
        }
    }
}
