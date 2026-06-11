using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class ProfileBasedMockVehicleAdapterFactory : IVehicleAdapterFactory
    {
        public bool CanCreate(Vehicle vehicle)
        {
            return vehicle.IsEnabled && (vehicle.AdapterType?.StartsWith("Mock") ?? true);
        }

        public IVehicleAdapter Create(Vehicle vehicle)
        {
            var brand = vehicle.Brand?.ToUpperInvariant();
            if (brand == "BRAND B" || brand == "RGV-B")
            {
                return new MockBrandBVehicleAdapter(vehicle);
            }
            if (brand == "BRAND C" || brand == "RGV-C")
            {
                return new MockBrandCVehicleAdapter(vehicle);
            }
            
            // Default is Brand A or fallback
            return new MockBrandAVehicleAdapter(vehicle);
        }
    }
}
