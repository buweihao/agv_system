using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class VehicleAdapterManager : IVehicleAdapterManager
    {
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IEnumerable<IVehicleAdapterFactory> _adapterFactories;
        private readonly IVehicleStatusPublisher _statusPublisher;
        private readonly Dictionary<string, IVehicleAdapter> _adapters = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
        private bool _started;

        public VehicleAdapterManager(
            IVehicleRepository vehicleRepository,
            IEnumerable<IVehicleAdapterFactory> adapterFactories,
            IVehicleStatusPublisher statusPublisher)
        {
            _vehicleRepository = vehicleRepository;
            _adapterFactories = adapterFactories;
            _statusPublisher = statusPublisher;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _lifecycleLock.WaitAsync(cancellationToken);
            try
            {
                if (_started)
                {
                    return;
                }

                var vehicles = await _vehicleRepository.GetAllAsync();
                foreach (var vehicle in vehicles.Where(item => item.IsEnabled))
                {
                    var factory = _adapterFactories.FirstOrDefault(item => item.CanCreate(vehicle));
                    if (factory is null)
                    {
                        continue;
                    }

                    var adapter = factory.Create(vehicle);
                    adapter.StatusReceived += OnStatusReceived;
                    _adapters[adapter.VehicleId] = adapter;
                    await adapter.StartAsync(cancellationToken);
                }

                _started = true;
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _lifecycleLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var adapter in _adapters.Values)
                {
                    adapter.StatusReceived -= OnStatusReceived;
                    await adapter.StopAsync(cancellationToken);
                }

                _adapters.Clear();
                _started = false;
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        public async Task<DispatchResult> SendCommandAsync(DispatchCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (string.IsNullOrWhiteSpace(command.VehicleId))
            {
                return DispatchResult.Failure("VehicleRequired", "Command must specify a vehicle id.", command.TaskId);
            }

            if (!_adapters.TryGetValue(command.VehicleId, out var adapter))
            {
                return DispatchResult.Failure("VehicleAdapterNotFound", $"No adapter is registered for vehicle {command.VehicleId}.", command.TaskId, command.VehicleId);
            }

            command.CommandId = string.IsNullOrWhiteSpace(command.CommandId)
                ? $"CMD-{DateTime.Now:yyyyMMddHHmmssfff}"
                : command.CommandId;
            command.SentAt = DateTime.Now;

            try
            {
                await adapter.SendCommandAsync(command, cancellationToken);
                return DispatchResult.Success("Command sent to vehicle adapter.", command.TaskId, command.VehicleId, command.CommandId);
            }
            catch (NotSupportedException ex)
            {
                return DispatchResult.Failure("CommandNotSupported", ex.Message, command.TaskId, command.VehicleId);
            }
            catch (Exception ex)
            {
                return DispatchResult.Failure("CommandFailed", ex.Message, command.TaskId, command.VehicleId);
            }
        }

        private void OnStatusReceived(object? sender, VehicleStatusSnapshot snapshot)
        {
            _statusPublisher.PublishStatus(snapshot);
        }
    }
}
