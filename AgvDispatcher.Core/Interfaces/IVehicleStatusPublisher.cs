using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IVehicleStatusPublisher
    {
        VehicleStatusIngestionResult PublishStatus(VehicleStatusSnapshot snapshot);
    }
}
