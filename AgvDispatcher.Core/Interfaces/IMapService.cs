using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IMapService
    {
        IReadOnlyList<MapNode> GetNodes();

        IReadOnlyList<MapEdge> GetEdges();

        MapNode? GetNode(string nodeId);

        IReadOnlyList<MapNode> FindPath(string startNodeId, string endNodeId);

        PlannedPath FindPlannedPath(string startNodeId, string endNodeId);

        bool IsPathAvailable(string startNodeId, string endNodeId);
    }
}
