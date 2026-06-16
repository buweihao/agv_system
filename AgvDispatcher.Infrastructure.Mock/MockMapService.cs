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

        public MapEdge? GetEdge(string edgeId)
        {
            return _edges.FirstOrDefault(edge =>
                string.Equals(edge.EdgeId, edgeId, StringComparison.OrdinalIgnoreCase));
        }

        public bool NodeExists(string nodeId)
        {
            return GetNode(nodeId) != null;
        }

        public PlannedPath FindPlannedPath(string startNodeId, string endNodeId)
        {
            return _pathPlanningService.PlanPath(_nodes, _edges, startNodeId, endNodeId);
        }

        public PlannedPath FindPlannedPath(PathPlanningRequest request)
        {
            // Simple mock implementation
            return FindPlannedPath(request.StartNodeId, request.EndNodeId);
        }

        public bool IsPathAvailable(string startNodeId, string endNodeId)
        {
            return FindPlannedPath(startNodeId, endNodeId).IsAvailable;
        }

        public double GetPathDistance(string startNodeId, string endNodeId)
        {
            var path = FindPlannedPath(startNodeId, endNodeId);
            return path.IsAvailable ? path.TotalLength : double.PositiveInfinity;
        }

        public IReadOnlyList<MapNode> FindReachableNodes(string startNodeId)
        {
            // Simple mock implementation: return all nodes except startNodeId
            return _nodes.Where(n => n.NodeId != startNodeId).ToList();
        }

        public IReadOnlyList<MapNode> FindNodesByType(AgvDispatcher.Core.Enums.MapNodeType nodeType)
        {
            return _nodes.Where(n => n.NodeType == nodeType).ToList();
        }
    }
}
