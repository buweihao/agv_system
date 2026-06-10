using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class AdapterDispatchService : IDispatchService
    {
        private readonly IVehicleAdapterManager _vehicleAdapterManager;

        public AdapterDispatchService(IVehicleAdapterManager vehicleAdapterManager)
        {
            _vehicleAdapterManager = vehicleAdapterManager;
        }

        public DispatchResult AssignTask(string taskId, string? preferredVehicleId = null)
        {
            if (string.IsNullOrWhiteSpace(preferredVehicleId))
            {
                return DispatchResult.Failure("VehicleRequired", "AssignTask requires a preferred vehicle until dispatch selection is implemented.", taskId);
            }

            return SendCommand(new DispatchCommand
            {
                CommandType = DispatchCommandType.AssignTask,
                TaskId = taskId,
                VehicleId = preferredVehicleId,
                IssuedBy = "DispatchService"
            });
        }

        public DispatchResult SendCommand(DispatchCommand command)
        {
            return _vehicleAdapterManager.SendCommandAsync(command, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        public DispatchResult PauseVehicle(string vehicleId)
        {
            return SendCommand(new DispatchCommand
            {
                CommandType = DispatchCommandType.Pause,
                VehicleId = vehicleId,
                IssuedBy = "DispatchService"
            });
        }

        public DispatchResult ResumeVehicle(string vehicleId)
        {
            return SendCommand(new DispatchCommand
            {
                CommandType = DispatchCommandType.Resume,
                VehicleId = vehicleId,
                IssuedBy = "DispatchService"
            });
        }

        public DispatchResult CancelTask(string taskId, string? reason = null)
        {
            return DispatchResult.Failure("VehicleRequired", "CancelTask requires a target vehicle in the stage-five adapter layer.", taskId);
        }
    }
}
