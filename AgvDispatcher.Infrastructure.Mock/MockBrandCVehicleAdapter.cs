using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockBrandCVehicleAdapter : MockVehicleAdapterBase
    {
        public MockBrandCVehicleAdapter(Vehicle vehicle, AgvDispatcher.Core.Interfaces.IChargeStationRepository? chargeRepo = null) : base(vehicle, chargeRepo)
        {
        }
    }
}
