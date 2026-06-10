using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockMapService : IMapService
    {
        private readonly IReadOnlyList<MapNode> _nodes = MockData.CreateMapNodes();
        private readonly IReadOnlyList<MapEdge> _edges = MockData.CreateMapEdges();
        private readonly IPathPlanningService _pathPlanningService;

        public MockMapService(IPathPlanningService pathPlanningService)
        {
            _pathPlanningService = pathPlanningService;
        }

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
            return FindPlannedPath(startNodeId, endNodeId).Nodes;
        }

        public PlannedPath FindPlannedPath(string startNodeId, string endNodeId)
        {
            return _pathPlanningService.PlanPath(_nodes, _edges, startNodeId, endNodeId);
        }

        public bool IsPathAvailable(string startNodeId, string endNodeId)
        {
            return FindPlannedPath(startNodeId, endNodeId).IsAvailable;
        }
    }
}
