using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IVehicleStateStore
    {
        bool CreateVehicle(VehicleStatusSnapshot snapshot);

        void UpsertStatus(VehicleStatusSnapshot snapshot);

        bool RemoveVehicle(string vehicleId);

        VehicleStatusSnapshot? GetVehicle(string vehicleId);

        IReadOnlyCollection<VehicleStatusSnapshot> GetAllVehicles();
    }
}
