using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IDispatchService
    {
        DispatchResult AssignTask(string taskId, string? preferredVehicleId = null);

        DispatchResult SendCommand(DispatchCommand command);

        DispatchResult PauseVehicle(string vehicleId);

        DispatchResult ResumeVehicle(string vehicleId);

        DispatchResult CancelTask(string taskId, string? reason = null);
    }
}
