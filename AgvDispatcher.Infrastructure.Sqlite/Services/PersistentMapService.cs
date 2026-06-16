using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Services;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentMapService : IMapService
    {
        private readonly IMapRepository _maps;
        private readonly IPathPlanningService _pathPlanningService;
        private readonly IMapLocationAliasRepository _aliases;

        public PersistentMapService(
            IMapRepository maps,
            IPathPlanningService pathPlanningService,
            IMapLocationAliasRepository aliases)
        {
            _maps = maps;
            _pathPlanningService = pathPlanningService;
            _aliases = aliases;
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
            return FindPlannedPath(startNodeId, endNodeId).Nodes;
        }

        public PlannedPath FindPlannedPath(string startNodeId, string endNodeId)
        {
            return _pathPlanningService.PlanPath(GetNodes(), GetEdges(), startNodeId, endNodeId);
        }

        public bool IsPathAvailable(string startNodeId, string endNodeId)
        {
            return FindPlannedPath(startNodeId, endNodeId).IsAvailable;
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

        public double GetPathDistance(string startNodeId, string endNodeId)
        {
            var path = FindPlannedPath(startNodeId, endNodeId);
            return path.IsAvailable ? path.TotalLength : -1;
        }

        public IReadOnlyList<MapNode> FindReachableNodes(string startNodeId)
        {
            return MapGraphHelper.FindReachableNodes(GetNodes(), GetEdges(), startNodeId);
        }

        public IReadOnlyList<MapNode> FindNodesByType(MapNodeType nodeType)
        {
            return GetNodes().Where(node => node.NodeType == nodeType).ToList();
        }

        public PlannedPath FindPlannedPath(PathPlanningRequest request)
        {
            return _pathPlanningService.PlanPath(GetNodes(), GetEdges(), request);
        }

        public string? ResolveNodeIdByAlias(string aliasValue, string? brand = null)
        {
            var aliases = _aliases.GetAllAsync().GetAwaiter().GetResult();
            return MapGraphHelper.ResolveNodeIdByAlias(aliases, aliasValue, brand);
        }

        public IReadOnlyList<MapLocationAlias> GetAliasesForNode(string nodeId)
        {
            var aliases = _aliases.GetAllAsync().GetAwaiter().GetResult();
            return aliases
                .Where(a => a.IsEnabled && string.Equals(a.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
