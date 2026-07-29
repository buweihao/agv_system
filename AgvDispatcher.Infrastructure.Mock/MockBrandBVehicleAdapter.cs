using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockBrandBVehicleAdapter : MockVehicleAdapterBase
    {
        public MockBrandBVehicleAdapter(Vehicle vehicle, AgvDispatcher.Core.Interfaces.IChargeStationRepository? chargeRepo = null) : base(vehicle, chargeRepo)
        {
        }
    }
}
