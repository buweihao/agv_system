using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentMapService
    {
        private readonly IMapRepository _maps;
        private readonly IPathPlanningService _pathPlanningService;

        public PersistentMapService(IMapRepository maps, IPathPlanningService pathPlanningService)
        {
            _maps = maps;
            _pathPlanningService = pathPlanningService;
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

        public MapEdge? GetEdge(string edgeId)
        {
            return GetEdges().FirstOrDefault(edge =>
                string.Equals(edge.EdgeId, edgeId, StringComparison.OrdinalIgnoreCase));
        }

        public bool NodeExists(string nodeId)
        {
            return GetNode(nodeId) != null;
        }

        public PlannedPath FindPlannedPath(string startNodeId, string endNodeId)
        {
            return _pathPlanningService.PlanPath(GetNodes(), GetEdges(), startNodeId, endNodeId);
        }

        public PlannedPath FindPlannedPath(PathPlanningRequest request)
        {
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
            var nodes = GetNodes();
            return nodes.Where(n => n.NodeId != startNodeId).ToList();
        }

        public IReadOnlyList<MapNode> FindNodesByType(AgvDispatcher.Core.Enums.MapNodeType nodeType)
        {
            return GetNodes().Where(n => n.NodeType == nodeType).ToList();
        }
    }
}
