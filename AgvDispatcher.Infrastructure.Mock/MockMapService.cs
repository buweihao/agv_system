using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Services;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockMapService : IMapService
    {
        private readonly IReadOnlyList<MapNode> _nodes = MockData.CreateMapNodes();
        private readonly IReadOnlyList<MapEdge> _edges = MockData.CreateMapEdges();
        private readonly IReadOnlyList<MapLocationAlias> _aliases = MockData.CreateMapAliases();
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

        public MapEdge? GetEdge(string edgeId)
        {
            return _edges.FirstOrDefault(edge =>
                string.Equals(edge.EdgeId, edgeId, StringComparison.OrdinalIgnoreCase));
        }

        public bool NodeExists(string nodeId)
        {
            return GetNode(nodeId) != null;
        }

        public double GetPathDistance(string startNodeId, string endNodeId)
        {
            var path = FindPlannedPath(startNodeId, endNodeId);
            return path.IsAvailable ? path.TotalLength : -1;
        }

        public IReadOnlyList<MapNode> FindReachableNodes(string startNodeId)
        {
            return MapGraphHelper.FindReachableNodes(_nodes, _edges, startNodeId);
        }

        public IReadOnlyList<MapNode> FindNodesByType(MapNodeType nodeType)
        {
            return _nodes.Where(node => node.NodeType == nodeType).ToList();
        }

        public PlannedPath FindPlannedPath(PathPlanningRequest request)
        {
            return _pathPlanningService.PlanPath(_nodes, _edges, request);
        }

        public string? ResolveNodeIdByAlias(string aliasValue, string? brand = null)
        {
            return MapGraphHelper.ResolveNodeIdByAlias(_aliases, aliasValue, brand);
        }

        public IReadOnlyList<MapLocationAlias> GetAliasesForNode(string nodeId)
        {
            return _aliases
                .Where(a => a.IsEnabled && string.Equals(a.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
