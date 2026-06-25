using AgvDispatcher.Infrastructure.Okapi.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public enum OkapiRequestControlType
    {
        Unknown = 0,
        Type1 = 1,
        Type2 = 2,
        Type3 = 3
    }

    public class OkapiAreaControlHandler
    {
        private readonly OkapiProtocolLogger _logger;
        private readonly OkapiVehicleIdentityMapper _identityMapper;

        public OkapiAreaControlHandler(OkapiProtocolLogger logger, OkapiVehicleIdentityMapper identityMapper)
        {
            _logger = logger;
            _identityMapper = identityMapper;
        }

        public async Task HandleAreaControlAsync(RequestControlRequest request, CancellationToken token = default)
        {
            var vehicleId = await _identityMapper.GetVehicleIdAsync(request.AgvId, token);
            if (vehicleId == null)
            {
                _logger.LogError($"AgvId:{request.AgvId}", "HandleAreaControl", $"Could not find VehicleId for AgvId {request.AgvId}. Ignoring callback.", null);
                return;
            }

            _logger.LogReceive(vehicleId, "AreaControlCallback", $"Received Area Control: areaId={request.AreaId}, type={request.RequestType}, time={request.RequestTime}", null);
            
            // First version: we just log it and don't do full traffic control yet.
        }
    }
}
