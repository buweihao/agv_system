using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class ProfileBasedMockVehicleAdapterFactory : IVehicleAdapterFactory
    {
        private readonly IChargeStationRepository? _chargeStationRepository;

        public ProfileBasedMockVehicleAdapterFactory(IChargeStationRepository? chargeStationRepository = null)
        {
            _chargeStationRepository = chargeStationRepository;
        }
        public bool CanCreate(Vehicle vehicle)
        {
            return vehicle.IsEnabled && (vehicle.AdapterType?.StartsWith("Mock") ?? true);
        }

        public IVehicleAdapter Create(Vehicle vehicle)
        {
            var brand = vehicle.Brand?.ToUpperInvariant();
            if (brand == "BRAND B" || brand == "RGV-B")
            {
                return new MockBrandBVehicleAdapter(vehicle, _chargeStationRepository);
            }
            if (brand == "BRAND C" || brand == "RGV-C")
            {
                return new MockBrandCVehicleAdapter(vehicle, _chargeStationRepository);
            }
            
            // Default is Brand A or fallback
            return new MockBrandAVehicleAdapter(vehicle, _chargeStationRepository);
        }
    }
}
