using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IVehicleAdapter
    {
        string Brand { get; }

        VehicleStatusSnapshot ConvertStatus(object rawStatus);
    }
}
