using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using ContractMapNodeType = AgvDispatcher.Core.Contracts.Map.MapNodeType;
using LegacyMapNodeType = AgvDispatcher.Core.Enums.MapNodeType;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    /// <summary>
    /// SQLite-backed read-only static map query service. It does not save, publish, roll back, plan paths, or check reachability.
    /// </summary>
    public sealed class PersistentMapService : IMapService
    {
        private const string CurrentMapVersion = "sqlite-current";
        private readonly IMapRepository _maps;
        private readonly IMapLocationAliasRepository _aliases;

        public PersistentMapService(IMapRepository maps, IMapLocationAliasRepository aliases)
        {
            _maps = maps;
            _aliases = aliases;
        }

        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request)
        {
            var snapshot = LoadSnapshot();
            if (!string.IsNullOrWhiteSpace(request.ExpectedMapVersion)
                && !string.Equals(request.ExpectedMapVersion, snapshot.Version, StringComparison.OrdinalIgnoreCase))
            {
                return AgvResult<MapSnapshotDto>.Fail(FailureCode.MapVersionMismatch, "Map version mismatch.");
            }

            return AgvResult<MapSnapshotDto>.Ok(snapshot);
        }

        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request)
        {
            return AgvResult<IReadOnlyList<MapNodeDto>>.Ok(LoadSnapshot().Nodes);
        }

        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request)
        {
            return AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(LoadSnapshot().Edges);
        }

        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request)
        {
            var node = LoadSnapshot().Nodes.FirstOrDefault(item =>
                string.Equals(item.NodeId, request.NodeId, StringComparison.OrdinalIgnoreCase));
            return node is null
                ? AgvResult<MapNodeDto>.Fail(FailureCode.MapNodeNotFound, $"Node {request.NodeId} not found.")
                : AgvResult<MapNodeDto>.Ok(node);
        }

        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request)
        {
            var edge = LoadSnapshot().Edges.FirstOrDefault(item =>
                string.Equals(item.EdgeId, request.EdgeId, StringComparison.OrdinalIgnoreCase));
            return edge is null
                ? AgvResult<MapEdgeDto>.Fail(FailureCode.MapEdgeNotFound, $"Edge {request.EdgeId} not found.")
                : AgvResult<MapEdgeDto>.Ok(edge);
        }

        public AgvResult<bool> NodeExists(GetMapNodeRequest request)
        {
            return AgvResult<bool>.Ok(LoadSnapshot().Nodes.Any(item =>
                string.Equals(item.NodeId, request.NodeId, StringComparison.OrdinalIgnoreCase)));
        }

        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request)
        {
            return AgvResult<bool>.Ok(LoadSnapshot().Edges.Any(item =>
                string.Equals(item.EdgeId, request.EdgeId, StringComparison.OrdinalIgnoreCase)));
        }

        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request)
        {
            var edges = LoadSnapshot().Edges
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
            var nodes = LoadSnapshot().Nodes
                .Where(node => node.NodeType == request.NodeType)
                .ToList();
            return AgvResult<IReadOnlyList<MapNodeDto>>.Ok(nodes);
        }

        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request)
        {
            var mapping = LoadSnapshot().VendorNodeMappings.FirstOrDefault(item =>
                string.Equals(item.SystemNodeId, request.SystemNodeId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.VendorCode, request.VendorCode, StringComparison.OrdinalIgnoreCase));
            return mapping is null
                ? AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Mapping not found.")
                : AgvResult<string>.Ok(mapping.VendorNodeCode);
        }

        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request)
        {
            var mapping = LoadSnapshot().VendorNodeMappings.FirstOrDefault(item =>
                string.Equals(item.VendorNodeCode, request.VendorNodeCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.VendorCode, request.VendorCode, StringComparison.OrdinalIgnoreCase));
            return mapping is null
                ? AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Mapping not found.")
                : AgvResult<string>.Ok(mapping.SystemNodeId);
        }

        private MapSnapshotDto LoadSnapshot()
        {
            var nodes = _maps.GetNodesAsync().GetAwaiter().GetResult().ToList();
            var edges = _maps.GetEdgesAsync().GetAwaiter().GetResult().ToList();
            var aliases = _aliases.GetAllAsync().GetAwaiter().GetResult().ToList();
            var mapId = nodes.FirstOrDefault()?.MapId
                ?? edges.FirstOrDefault()?.MapId
                ?? "MAIN";

            return new MapSnapshotDto
            {
                MapId = mapId,
                MapName = "SQLite 当前地图",
                Version = CurrentMapVersion,
                Nodes = nodes.Select(ToNodeDto).ToList(),
                Edges = edges.Select(ToEdgeDto).ToList(),
                Areas = BuildAreas(nodes, edges),
                VendorNodeMappings = aliases.Where(alias => alias.IsEnabled).Select(ToVendorMappingDto).ToList(),
                UpdatedAt = DateTimeOffset.Now
            };
        }

        private static IReadOnlyList<MapAreaDto> BuildAreas(IReadOnlyList<MapNode> nodes, IReadOnlyList<MapEdge> edges)
        {
            // 旧库没有独立区域表，过渡期从点位/路线 AreaCode 派生静态区域快照。
            return nodes.Select(node => node.AreaCode)
                .Concat(edges.Select(edge => edge.AreaCode))
                .Where(area => !string.IsNullOrWhiteSpace(area))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(area => new MapAreaDto
                {
                    AreaId = area,
                    AreaName = area,
                    AreaType = MapAreaType.Normal,
                    Properties = new Dictionary<string, string> { ["Capacity"] = "0" }
                })
                .ToList();
        }

        private static MapNodeDto ToNodeDto(MapNode node)
        {
            var properties = new Dictionary<string, string>
            {
                ["Capacity"] = node.ParkingCapacity.ToString(),
                ["AllowedBrands"] = node.AllowedBrands,
                ["RequiredCapabilities"] = ((int)node.RequiredCapabilities).ToString()
            };
            foreach (var tag in node.Tags)
            {
                properties[tag.Key] = tag.Value;
            }

            return new MapNodeDto
            {
                NodeId = node.NodeId,
                NodeCode = node.NodeCode,
                NodeName = node.Name,
                NodeType = ToContractNodeType(node.NodeType),
                X = node.Position.X,
                Y = node.Position.Y,
                Angle = node.Heading,
                AreaId = string.IsNullOrWhiteSpace(node.AreaCode) ? null : node.AreaCode,
                Enabled = node.IsEnabled,
                Properties = properties
            };
        }

        private static MapEdgeDto ToEdgeDto(MapEdge edge)
        {
            return new MapEdgeDto
            {
                EdgeId = edge.EdgeId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Distance = edge.Length,
                Direction = edge.Direction == EdgeDirection.Bidirectional
                    ? MapEdgeDirection.Bidirectional
                    : MapEdgeDirection.OneWay,
                Cost = edge.Cost,
                SpeedLimit = edge.MaxSpeed,
                AreaId = string.IsNullOrWhiteSpace(edge.AreaCode) ? null : edge.AreaCode,
                Enabled = edge.IsEnabled && edge.Direction != EdgeDirection.Closed,
                Properties = new Dictionary<string, string>
                {
                    ["AllowedBrands"] = edge.AllowedBrands,
                    ["MaxVehicleFlow"] = edge.MaxVehicleFlow.ToString(),
                    ["Remark"] = edge.Remark
                }
            };
        }

        private static VendorNodeMappingDto ToVendorMappingDto(MapLocationAlias alias)
        {
            return new VendorNodeMappingDto
            {
                VendorCode = string.IsNullOrWhiteSpace(alias.Brand) ? "GLOBAL" : alias.Brand,
                SystemNodeId = alias.NodeId,
                VendorNodeCode = alias.AliasValue
            };
        }

        private static ContractMapNodeType ToContractNodeType(LegacyMapNodeType nodeType) => nodeType switch
        {
            LegacyMapNodeType.Station => ContractMapNodeType.WorkStation,
            LegacyMapNodeType.Pickup => ContractMapNodeType.PickPoint,
            LegacyMapNodeType.Dropoff => ContractMapNodeType.PutPoint,
            LegacyMapNodeType.Charge => ContractMapNodeType.ChargeStation,
            LegacyMapNodeType.Waiting => ContractMapNodeType.WaitingPoint,
            LegacyMapNodeType.Elevator => ContractMapNodeType.Elevator,
            LegacyMapNodeType.Door => ContractMapNodeType.Door,
            _ => ContractMapNodeType.Normal
        };
    }
}
