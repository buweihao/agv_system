using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IVehicleAdapterFactory
    {
        bool CanCreate(Vehicle vehicle);

        IVehicleAdapter Create(Vehicle vehicle);
    }
}
