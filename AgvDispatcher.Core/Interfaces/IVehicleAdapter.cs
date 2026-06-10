using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IVehicleAdapter
    {
        string VehicleId { get; }

        string Brand { get; }

        event EventHandler<VehicleStatusSnapshot>? StatusReceived;

        Task StartAsync(CancellationToken cancellationToken);

        Task StopAsync(CancellationToken cancellationToken);

        Task SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken);

        VehicleStatusSnapshot ConvertStatus(object rawStatus);
    }
}
