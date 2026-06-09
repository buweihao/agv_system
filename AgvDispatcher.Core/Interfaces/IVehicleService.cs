using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IVehicleService
    {
        IReadOnlyList<Vehicle> GetVehicles();

        Vehicle? GetVehicle(string vehicleId);

        VehicleStatus? GetVehicleStatus(string vehicleId);

        IReadOnlyList<VehicleStatus> GetVehicleStatuses();

        void UpdateVehicleStatus(VehicleStatus status);

        bool IsVehicleAvailable(string vehicleId);
    }
}
