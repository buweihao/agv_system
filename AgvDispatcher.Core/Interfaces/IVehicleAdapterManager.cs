using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IVehicleAdapterManager
    {
        Task StartAsync(CancellationToken cancellationToken);

        Task StopAsync(CancellationToken cancellationToken);

        Task<DispatchResult> SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken);
    }
}
