using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Map
{
    public interface IMapService
    {
        AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request);

        AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request);

        AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request);

        AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request);

        AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request);

        AgvResult<bool> NodeExists(GetMapNodeRequest request);

        AgvResult<bool> EdgeExists(GetMapEdgeRequest request);

        AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request);

        AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request);

        AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request);

        AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request);
    }
}
