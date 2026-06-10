using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentMapService : IMapService
    {
        private readonly IMapRepository _maps;

        public PersistentMapService(IMapRepository maps)
        {
            _maps = maps;
        }

        public IReadOnlyList<MapNode> GetNodes()
        {
            return _maps.GetNodesAsync().GetAwaiter().GetResult();
        }

        public IReadOnlyList<MapEdge> GetEdges()
        {
            return _maps.GetEdgesAsync().GetAwaiter().GetResult();
        }

        public MapNode? GetNode(string nodeId)
        {
            return GetNodes().FirstOrDefault(node =>
                string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<MapNode> FindPath(string startNodeId, string endNodeId)
        {
            var start = GetNode(startNodeId);
            var end = GetNode(endNodeId);
            if (start is null || end is null)
            {
                return Array.Empty<MapNode>();
            }

            return new[] { start, end };
        }

        public bool IsPathAvailable(string startNodeId, string endNodeId)
        {
            return GetEdges().Any(edge =>
                !edge.IsLocked
                && edge.IsEnabled
                && ((string.Equals(edge.FromNodeId, startNodeId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(edge.ToNodeId, endNodeId, StringComparison.OrdinalIgnoreCase))
                    || (string.Equals(edge.FromNodeId, endNodeId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(edge.ToNodeId, startNodeId, StringComparison.OrdinalIgnoreCase))));
        }
    }
}
