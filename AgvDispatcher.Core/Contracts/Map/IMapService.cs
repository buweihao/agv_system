using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Map
{
    /// <summary>
    /// Provides read-only static queries for the current published runtime map.
    /// </summary>
    /// <remarks>
    /// This contract must not perform path planning, traffic control, map editing, draft saving, or map publishing.
    /// </remarks>
    public interface IMapService
    {
        /// <summary>
        /// Gets the current published runtime map snapshot.
        /// </summary>
        AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request);

        /// <summary>
        /// Gets static nodes from the current published runtime map.
        /// </summary>
        AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request);

        /// <summary>
        /// Gets static edges from the current published runtime map.
        /// </summary>
        AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request);

        /// <summary>
        /// Gets one static node from the current published runtime map.
        /// </summary>
        AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request);

        /// <summary>
        /// Gets one static edge from the current published runtime map.
        /// </summary>
        AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request);

        /// <summary>
        /// Checks whether a static node exists in the current published runtime map.
        /// </summary>
        AgvResult<bool> NodeExists(GetMapNodeRequest request);

        /// <summary>
        /// Checks whether a static edge exists in the current published runtime map.
        /// </summary>
        AgvResult<bool> EdgeExists(GetMapEdgeRequest request);

        /// <summary>
        /// Gets outgoing static edges for a node in the current published runtime map.
        /// </summary>
        AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request);

        /// <summary>
        /// Gets static nodes by node type from the current published runtime map.
        /// </summary>
        AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request);

        /// <summary>
        /// Gets a vendor-specific node code for a system node identifier.
        /// </summary>
        AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request);

        /// <summary>
        /// Gets a system node identifier from a vendor-specific node code.
        /// </summary>
        AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request);
    }
}
