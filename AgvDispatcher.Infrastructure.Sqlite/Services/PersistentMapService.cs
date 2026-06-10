using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentMapService : IMapService
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
    }
}
