using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockMapService : IMapService
    {
        private readonly IReadOnlyList<MapNode> _nodes = MockData.CreateMapNodes();
        private readonly IReadOnlyList<MapEdge> _edges = MockData.CreateMapEdges();

        public IReadOnlyList<MapNode> GetNodes()
        {
            return _nodes;
        }

        public IReadOnlyList<MapEdge> GetEdges()
        {
            return _edges;
        }

        public MapNode? GetNode(string nodeId)
        {
            return _nodes.FirstOrDefault(node =>
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
            return GetNode(startNodeId) is not null
                && GetNode(endNodeId) is not null
                && _edges.Any(edge =>
                    !edge.IsLocked
                    && edge.IsEnabled
                    && ((edge.FromNodeId == startNodeId && edge.ToNodeId == endNodeId)
                        || (edge.FromNodeId == endNodeId && edge.ToNodeId == startNodeId)));
        }
    }
}
