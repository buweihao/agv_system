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
                MapName = "SQLite 当前运行地图",
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
            if (nodes.Any(node => node.NodeId == "PICK-A1"))
            {
                static MapPointDto P(double x, double y) => new() { X = x, Y = y };
                static Dictionary<string, string> Props(params (string Key, string Value)[] values)
                    => values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
                static MapAreaDto Area(string id, string name, MapAreaType type, string color, IReadOnlyList<MapPointDto> points, params (string Key, string Value)[] properties) => new()
                {
                    AreaId = id,
                    AreaName = name,
                    AreaType = type,
                    BoundaryPoints = points,
                    Enabled = true,
                    Properties = Props(properties.Prepend(("Color", color)).ToArray())
                };

                return new[]
                {
                    Area("CAP-A", "\u6210\u54c1\u5e93\u9650\u6d41\u533a", MapAreaType.WorkArea, "#00BFA6", new[] { P(40, 40), P(300, 40), P(300, 210), P(40, 210) }, ("ZoneKind", "CapacityLimited"), ("Capacity", "3"), ("Label", "\u6210\u54c1\u5e93A: 0/3")),
                    Area("RAW-B", "\u539f\u6750\u6599\u9650\u6d41\u533a", MapAreaType.WorkArea, "#32D583", new[] { P(40, 255), P(300, 255), P(300, 420), P(40, 420) }, ("ZoneKind", "CapacityLimited"), ("Capacity", "4"), ("Label", "\u539f\u6750\u6599B: 0/4")),
                    Area("QR-A", "\u4e8c\u7ef4\u7801\u5bfc\u822a\u533a", MapAreaType.Normal, "#8EA8C3", new[] { P(330, 55), P(540, 55), P(540, 260), P(330, 260) }, ("ZoneKind", "NavigationMedium"), ("NavigationMedium", "\u4e8c\u7ef4\u7801\u5bfc\u822a")),
                    Area("SLAM-B", "\u6fc0\u5149 SLAM \u5bfc\u822a\u533a", MapAreaType.Normal, "#5A7FA6", new[] { P(610, 45), P(840, 45), P(840, 210), P(610, 210) }, ("ZoneKind", "NavigationMedium"), ("NavigationMedium", "\u6fc0\u5149SLAM")),
                    Area("INT-01", "\u4ea4\u901a\u4e92\u65a5\u533a", MapAreaType.IntersectionArea, "#FFB020", new[] { P(350, 225), P(535, 240), P(530, 385), P(350, 395) }, ("ZoneKind", "Interlocking"), ("Capacity", "1"), ("Label", "\u4e92\u65a5\u533a: 0/1")),
                    Area("FIRE-01", "\u6d88\u9632\u5b89\u5168\u8054\u52a8\u533a", MapAreaType.BlockedArea, "#FF4D4F", new[] { P(610, 250), P(835, 250), P(835, 340), P(610, 340) }, ("ZoneKind", "FireSafety"), ("AlarmSource", "PLC-FIRE-01"), ("Label", "\u6d88\u9632\u5b89\u5168\u533a")),
                    Area("SPD-01", "\u9650\u901f\u5de5\u827a\u533a", MapAreaType.NarrowArea, "#9B6DFF", new[] { P(840, 295), P(1130, 310), P(1130, 535), P(840, 535) }, ("ZoneKind", "SpeedRestricted"), ("SpeedLimit", "0.3"), ("Process", "Weighing"), ("Label", "\u9650\u901f: 300mm/s")),
                    Area("STBY-CHG", "\u5f85\u673a\u5145\u7535\u533a", MapAreaType.ChargingArea, "#FFD700", new[] { P(560, 395), P(850, 395), P(850, 545), P(560, 545) }, ("ZoneKind", "StandbyCharging"), ("Capacity", "5"), ("Label", "\u5f85\u673a/\u5145\u7535\u533a")),
                    Area("MAINT-01", "\u7ef4\u62a4\u963b\u65ad\u9884\u7559\u533a", MapAreaType.BlockedArea, "#777777", new[] { P(335, 425), P(540, 425), P(540, 535), P(335, 535) }, ("ZoneKind", "StaticRestricted"), ("Label", "\u7ef4\u62a4\u9884\u7559\u533a"))
                };
            }
            // 鏃у簱娌℃湁鐙珛鍖哄煙琛紝杩囨浮鏈熶粠鐐逛綅/璺嚎 AreaCode 娲剧敓闈欐€佸尯鍩熷揩鐓с€?
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

