using System;
using System.Collections.Generic;
using System.Linq;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Modules.MapModule.Services
{
    public class MockMapService : IMapService
    {
        private readonly MapSnapshotDto _snapshot;

        public MockMapService()
        {
            var nodes = new List<MapNodeDto>
            {
                new MapNodeDto { NodeId = "N1", NodeCode = "Point_A", NodeType = MapNodeType.PickPoint, X = 0, Y = 0 },
                new MapNodeDto { NodeId = "N2", NodeCode = "Point_B", NodeType = MapNodeType.Normal, X = 10, Y = 0 },
                new MapNodeDto { NodeId = "N3", NodeCode = "Point_C", NodeType = MapNodeType.ChargeStation, X = 10, Y = 10 }
            };

            var edges = new List<MapEdgeDto>
            {
                new MapEdgeDto { EdgeId = "E1", FromNodeId = "N1", ToNodeId = "N2", Distance = 10, Direction = MapEdgeDirection.Bidirectional },
                new MapEdgeDto { EdgeId = "E2", FromNodeId = "N2", ToNodeId = "N3", Distance = 10, Direction = MapEdgeDirection.OneWay }
            };

            var areas = new List<MapAreaDto>
            {
                new MapAreaDto
                {
                    AreaId = "A1",
                    AreaName = "Zone_1",
                    AreaType = MapAreaType.WorkArea,
                    BoundaryPoints = new List<MapPointDto>
                    {
                        new MapPointDto { X = -5, Y = -5 },
                        new MapPointDto { X = 15, Y = -5 },
                        new MapPointDto { X = 15, Y = 15 },
                        new MapPointDto { X = -5, Y = 15 }
                    }
                }
            };

            var mappings = new List<VendorNodeMappingDto>
            {
                new VendorNodeMappingDto { VendorCode = "Seer", SystemNodeId = "N1", VendorNodeCode = "SEER_A01" }
            };

            _snapshot = new MapSnapshotDto
            {
                MapId = "Map_Mock_001",
                MapName = "Mock Test Map",
                Version = "1.0",
                Nodes = nodes,
                Edges = edges,
                Areas = areas,
                VendorNodeMappings = mappings,
                UpdatedAt = DateTimeOffset.Now
            };
        }

        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request)
        {
            if (request.ExpectedMapVersion != null && request.ExpectedMapVersion != _snapshot.Version)
            {
                return AgvResult<MapSnapshotDto>.Fail(FailureCode.MapVersionMismatch, "Map version mismatch");
            }
            return AgvResult<MapSnapshotDto>.Ok(_snapshot);
        }

        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request)
        {
            return AgvResult<IReadOnlyList<MapNodeDto>>.Ok(_snapshot.Nodes);
        }

        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request)
        {
            return AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(_snapshot.Edges);
        }

        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request)
        {
            var node = _snapshot.Nodes.FirstOrDefault(n => n.NodeId == request.NodeId);
            return node != null 
                ? AgvResult<MapNodeDto>.Ok(node) 
                : AgvResult<MapNodeDto>.Fail(FailureCode.MapNodeNotFound, $"Node {request.NodeId} not found");
        }

        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request)
        {
            var edge = _snapshot.Edges.FirstOrDefault(e => e.EdgeId == request.EdgeId);
            return edge != null 
                ? AgvResult<MapEdgeDto>.Ok(edge) 
                : AgvResult<MapEdgeDto>.Fail(FailureCode.MapEdgeNotFound, $"Edge {request.EdgeId} not found");
        }

        public AgvResult<bool> NodeExists(GetMapNodeRequest request)
        {
            var exists = _snapshot.Nodes.Any(n => n.NodeId == request.NodeId);
            return AgvResult<bool>.Ok(exists);
        }

        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request)
        {
            var exists = _snapshot.Edges.Any(e => e.EdgeId == request.EdgeId);
            return AgvResult<bool>.Ok(exists);
        }

        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request)
        {
            var edges = _snapshot.Edges.Where(e => e.FromNodeId == request.NodeId || (e.ToNodeId == request.NodeId && e.Direction == MapEdgeDirection.Bidirectional)).ToList();
            return AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(edges);
        }

        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request)
        {
            var nodes = _snapshot.Nodes.Where(n => n.NodeType == request.NodeType).ToList();
            return AgvResult<IReadOnlyList<MapNodeDto>>.Ok(nodes);
        }

        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request)
        {
            var mapping = _snapshot.VendorNodeMappings.FirstOrDefault(m => m.SystemNodeId == request.SystemNodeId && m.VendorCode == request.VendorCode);
            return mapping != null
                ? AgvResult<string>.Ok(mapping.VendorNodeCode)
                : AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Mapping not found");
        }

        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request)
        {
            var mapping = _snapshot.VendorNodeMappings.FirstOrDefault(m => m.VendorNodeCode == request.VendorNodeCode && m.VendorCode == request.VendorCode);
            return mapping != null
                ? AgvResult<string>.Ok(mapping.SystemNodeId)
                : AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Mapping not found");
        }
    }
}
