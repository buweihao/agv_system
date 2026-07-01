using System;
using System.Collections.Generic;
using System.Linq;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Modules.MapModule.Services
{
    public sealed class MockMapService : IMapService
    {
        private readonly MockMapStore _store;

        public MockMapService()
            : this(new MockMapStore())
        {
        }

        public MockMapService(MockMapStore store)
        {
            _store = store;
        }

        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request)
        {
            var snapshot = _store.GetCurrentMap();
            if (string.IsNullOrWhiteSpace(snapshot.MapId))
            {
                return AgvResult<MapSnapshotDto>.Fail(FailureCode.MapNotLoaded, "Map was not loaded.");
            }

            if (!string.IsNullOrWhiteSpace(request.ExpectedMapVersion)
                && !string.Equals(request.ExpectedMapVersion, snapshot.Version, StringComparison.OrdinalIgnoreCase))
            {
                return AgvResult<MapSnapshotDto>.Fail(FailureCode.MapVersionMismatch, "Map version mismatch.");
            }

            return AgvResult<MapSnapshotDto>.Ok(snapshot);
        }

        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request)
        {
            var snapshot = _store.GetCurrentMap();
            return AgvResult<IReadOnlyList<MapNodeDto>>.Ok(snapshot.Nodes);
        }

        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request)
        {
            var snapshot = _store.GetCurrentMap();
            return AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(snapshot.Edges);
        }

        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request)
        {
            var node = _store.GetCurrentMap().Nodes.FirstOrDefault(item =>
                string.Equals(item.NodeId, request.NodeId, StringComparison.OrdinalIgnoreCase));
            return node is null
                ? AgvResult<MapNodeDto>.Fail(FailureCode.MapNodeNotFound, $"Node {request.NodeId} not found.")
                : AgvResult<MapNodeDto>.Ok(node);
        }

        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request)
        {
            var edge = _store.GetCurrentMap().Edges.FirstOrDefault(item =>
                string.Equals(item.EdgeId, request.EdgeId, StringComparison.OrdinalIgnoreCase));
            return edge is null
                ? AgvResult<MapEdgeDto>.Fail(FailureCode.MapEdgeNotFound, $"Edge {request.EdgeId} not found.")
                : AgvResult<MapEdgeDto>.Ok(edge);
        }

        public AgvResult<bool> NodeExists(GetMapNodeRequest request)
        {
            return AgvResult<bool>.Ok(_store.GetCurrentMap().Nodes.Any(item =>
                string.Equals(item.NodeId, request.NodeId, StringComparison.OrdinalIgnoreCase)));
        }

        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request)
        {
            return AgvResult<bool>.Ok(_store.GetCurrentMap().Edges.Any(item =>
                string.Equals(item.EdgeId, request.EdgeId, StringComparison.OrdinalIgnoreCase)));
        }

        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request)
        {
            var edges = _store.GetCurrentMap().Edges
                .Where(edge => edge.Enabled)
                .Where(edge =>
                    string.Equals(edge.FromNodeId, request.NodeId, StringComparison.OrdinalIgnoreCase)
                    || (edge.Direction == MapEdgeDirection.Bidirectional
                        && string.Equals(edge.ToNodeId, request.NodeId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            return AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(edges);
        }

        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request)
        {
            var nodes = _store.GetCurrentMap().Nodes
                .Where(node => node.NodeType == request.NodeType)
                .ToList();
            return AgvResult<IReadOnlyList<MapNodeDto>>.Ok(nodes);
        }

        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request)
        {
            var mapping = _store.GetCurrentMap().VendorNodeMappings.FirstOrDefault(item =>
                string.Equals(item.SystemNodeId, request.SystemNodeId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.VendorCode, request.VendorCode, StringComparison.OrdinalIgnoreCase));
            return mapping is null
                ? AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Mapping not found.")
                : AgvResult<string>.Ok(mapping.VendorNodeCode);
        }

        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request)
        {
            var mapping = _store.GetCurrentMap().VendorNodeMappings.FirstOrDefault(item =>
                string.Equals(item.VendorNodeCode, request.VendorNodeCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.VendorCode, request.VendorCode, StringComparison.OrdinalIgnoreCase));
            return mapping is null
                ? AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Mapping not found.")
                : AgvResult<string>.Ok(mapping.SystemNodeId);
        }
    }
}
