using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IPathPlanningService
    {
        PlannedPath PlanPath(
            IReadOnlyList<MapNode> nodes,
            IReadOnlyList<MapEdge> edges,
            string startNodeId,
            string endNodeId);
    }
}
