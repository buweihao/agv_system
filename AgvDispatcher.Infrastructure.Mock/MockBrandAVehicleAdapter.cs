using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockBrandAVehicleAdapter : MockVehicleAdapterBase
    {
        public MockBrandAVehicleAdapter(Vehicle vehicle, AgvDispatcher.Core.Interfaces.IChargeStationRepository? chargeRepo = null) : base(vehicle, chargeRepo)
        {
        }
    }
}
