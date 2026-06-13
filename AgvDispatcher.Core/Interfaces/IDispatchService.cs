using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IDispatchService
    {
        DispatchResult AssignTask(string taskId, string? preferredVehicleId = null);
        Task<DispatchResult> AssignTaskAsync(string taskId, string? preferredVehicleId = null, CancellationToken cancellationToken = default);

        DispatchResult SendCommand(DispatchCommand command);
        Task<DispatchResult> SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken = default);

        DispatchResult PauseVehicle(string vehicleId);

        DispatchResult ResumeVehicle(string vehicleId);

        DispatchResult CancelTask(string taskId, string? reason = null);
    }
}
