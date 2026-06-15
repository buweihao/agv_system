using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiVehicleAdapterFactory : IVehicleAdapterFactory
    {
        private readonly OkapiClient _client;
        private readonly OkapiCallbackServer _server;
        private readonly OkapiPointMapper _pointMapper;
        private readonly OkapiVehicleIdentityMapper _identityMapper;
        private readonly OkapiTaskStateHandler _taskStateHandler;
        private readonly OkapiAreaControlHandler _areaControlHandler;
        private readonly OkapiProtocolLogger _logger;
        private readonly OkapiOptions _options;
        private readonly OkapiStatusSyncService _statusSyncService;

        public OkapiVehicleAdapterFactory(
            OkapiClient client,
            OkapiCallbackServer server,
            OkapiPointMapper pointMapper,
            OkapiVehicleIdentityMapper identityMapper,
            OkapiTaskStateHandler taskStateHandler,
            OkapiAreaControlHandler areaControlHandler,
            OkapiProtocolLogger logger,
            OkapiOptions options,
            OkapiStatusSyncService statusSyncService)
        {
            _client = client;
            _server = server;
            _pointMapper = pointMapper;
            _identityMapper = identityMapper;
            _taskStateHandler = taskStateHandler;
            _areaControlHandler = areaControlHandler;
            _logger = logger;
            _options = options;
            _statusSyncService = statusSyncService;
        }

        public bool CanCreate(Vehicle vehicle)
        {
            return vehicle.IsEnabled && string.Equals(vehicle.AdapterType, "Okapi", StringComparison.OrdinalIgnoreCase);
        }

        public IVehicleAdapter Create(Vehicle vehicle)
        {
            return new OkapiVehicleAdapter(
                vehicle,
                _client,
                _server,
                _pointMapper,
                _identityMapper,
                _taskStateHandler,
                _areaControlHandler,
                _logger,
                _options,
                _statusSyncService);
        }
    }
}
