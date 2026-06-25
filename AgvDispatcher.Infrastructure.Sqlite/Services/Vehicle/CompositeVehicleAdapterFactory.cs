using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Mock;
using AgvDispatcher.Infrastructure.Okapi;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class CompositeVehicleAdapterFactory : IVehicleAdapterFactory
    {
        private readonly IEnumerable<IVehicleAdapterFactory> _factories;

        public CompositeVehicleAdapterFactory(
            ProfileBasedMockVehicleAdapterFactory mockFactory,
            OkapiVehicleAdapterFactory okapiFactory)
        {
            _factories = new IVehicleAdapterFactory[] { mockFactory, okapiFactory };
        }

        public bool CanCreate(Vehicle vehicle)
        {
            return _factories.Any(f => f.CanCreate(vehicle));
        }

        public IVehicleAdapter Create(Vehicle vehicle)
        {
            var factory = _factories.FirstOrDefault(f => f.CanCreate(vehicle));
            if (factory == null)
            {
                throw new NotSupportedException($"No adapter factory found for vehicle {vehicle.VehicleId} with AdapterType {vehicle.AdapterType}");
            }
            return factory.Create(vehicle);
        }
    }
}
