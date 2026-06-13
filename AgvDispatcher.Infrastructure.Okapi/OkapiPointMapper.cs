using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Okapi
{
    public class OkapiPointMapper
    {
        private readonly IMapLocationAliasRepository _aliasRepository;
        private readonly OkapiProtocolLogger _logger;

        public OkapiPointMapper(IMapLocationAliasRepository aliasRepository, OkapiProtocolLogger logger)
        {
            _aliasRepository = aliasRepository;
            _logger = logger;
        }

        public async Task<string?> NodeIdToOkapiPointAsync(string nodeId, string? brand, CancellationToken token = default)
        {
            var aliases = await _aliasRepository.GetAllAsync();
            var alias = aliases.FirstOrDefault(a => 
                a.NodeId == nodeId && 
                a.IsEnabled && 
                (string.IsNullOrWhiteSpace(a.Brand) || string.IsNullOrWhiteSpace(brand) || string.Equals(a.Brand, brand, StringComparison.OrdinalIgnoreCase)));

            if (alias != null)
            {
                return alias.AliasValue;
            }

            _logger.LogError("System", "PointMapper", $"Failed to map NodeId '{nodeId}' to Okapi point (Brand: {brand})");
            return null;
        }

        public async Task<string> OkapiPointToNodeIdAsync(string okapiPoint, string? brand, CancellationToken token = default)
        {
            var aliases = await _aliasRepository.GetAllAsync();
            var alias = aliases.FirstOrDefault(a => 
                a.AliasValue == okapiPoint && 
                a.IsEnabled && 
                (string.IsNullOrWhiteSpace(a.Brand) || string.IsNullOrWhiteSpace(brand) || string.Equals(a.Brand, brand, StringComparison.OrdinalIgnoreCase)));

            if (alias != null)
            {
                return alias.NodeId;
            }

            _logger.LogError("System", "PointMapper", $"Failed to map Okapi point '{okapiPoint}' to NodeId (Brand: {brand}). Falling back to original value.");
            return okapiPoint;
        }
    }
}
