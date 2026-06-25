using AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiPointMapper
    {
        private readonly IMapService _mapService;
        private readonly OkapiProtocolLogger _logger;

        public OkapiPointMapper(IMapService mapService, OkapiProtocolLogger logger)
        {
            _mapService = mapService;
            _logger = logger;
        }

        public Task<string?> NodeIdToOkapiPointAsync(string nodeId, string? brand, CancellationToken token = default)
        {
            var map = _mapService.GetCurrentMap(new GetMapSnapshotRequest());
            var alias = map.Data?.VendorNodeMappings.FirstOrDefault(a =>
                string.Equals(a.SystemNodeId, nodeId, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(a.VendorCode) ||
                 string.IsNullOrWhiteSpace(brand) ||
                 string.Equals(a.VendorCode, brand, StringComparison.OrdinalIgnoreCase)));

            if (alias != null)
            {
                return Task.FromResult<string?>(alias.VendorNodeCode);
            }

            _logger.LogError("System", "PointMapper", $"Failed to map NodeId '{nodeId}' to Okapi point (Brand: {brand})");
            return Task.FromResult<string?>(null);
        }

        public Task<string> OkapiPointToNodeIdAsync(string okapiPoint, string? brand, CancellationToken token = default)
        {
            var map = _mapService.GetCurrentMap(new GetMapSnapshotRequest());
            var alias = map.Data?.VendorNodeMappings.FirstOrDefault(a =>
                string.Equals(a.VendorNodeCode, okapiPoint, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(a.VendorCode) ||
                 string.IsNullOrWhiteSpace(brand) ||
                 string.Equals(a.VendorCode, brand, StringComparison.OrdinalIgnoreCase)));

            if (alias != null)
            {
                return Task.FromResult(alias.SystemNodeId);
            }

            _logger.LogError("System", "PointMapper", $"Failed to map Okapi point '{okapiPoint}' to NodeId (Brand: {brand}). Falling back to original value.");
            return Task.FromResult(okapiPoint);
        }
    }
}
