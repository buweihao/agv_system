using System.Security.Cryptography;
using System.Text;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using LegacyEdgeDirection = AgvDispatcher.Core.Enums.EdgeDirection;
using LegacyMapNodeType = AgvDispatcher.Core.Enums.MapNodeType;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    /// <summary>
    /// Provides the runtime map contract from nodes, edges, and aliases stored in SQLite.
    /// </summary>
    public sealed class PersistentMapService : IMapService
    {
        private readonly IMapRepository _maps;
        private readonly IMapLocationAliasRepository _aliases;
        private readonly IPathPlanningService _pathPlanningService;

        /// <summary>
        /// Initializes the persistent runtime map service.
        /// </summary>
        public PersistentMapService(
            IMapRepository maps,
            IMapLocationAliasRepository aliases,
            IPathPlanningService pathPlanningService)
        {
            _maps = maps;
            _aliases = aliases;
            _pathPlanningService = pathPlanningService;
        }

        /// <inheritdoc />
        public AgvResult<MapSnapshotDto> GetCurrentMap(GetMapSnapshotRequest request)
        {
            var snapshot = LoadSnapshot();
            return snapshot.Success && snapshot.Data is not null
                ? ValidateVersion(snapshot.Data, request.ExpectedMapVersion)
                : snapshot;
        }

        /// <inheritdoc />
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodes(GetMapSnapshotRequest request)
        {
            var snapshot = GetCurrentMap(request);
            return snapshot.Success && snapshot.Data is not null
                ? AgvResult<IReadOnlyList<MapNodeDto>>.Ok(snapshot.Data.Nodes)
                : AgvResult<IReadOnlyList<MapNodeDto>>.Fail(snapshot.Code, snapshot.Message);
        }

        /// <inheritdoc />
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetEdges(GetMapSnapshotRequest request)
        {
            var snapshot = GetCurrentMap(request);
            return snapshot.Success && snapshot.Data is not null
                ? AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(snapshot.Data.Edges)
                : AgvResult<IReadOnlyList<MapEdgeDto>>.Fail(snapshot.Code, snapshot.Message);
        }

        /// <inheritdoc />
        public AgvResult<MapNodeDto> GetNode(GetMapNodeRequest request)
        {
            var snapshot = LoadSnapshot();
            if (!snapshot.Success || snapshot.Data is null)
            {
                return AgvResult<MapNodeDto>.Fail(snapshot.Code, snapshot.Message);
            }

            var node = snapshot.Data.Nodes.FirstOrDefault(item =>
                string.Equals(item.NodeId, request.NodeId, StringComparison.OrdinalIgnoreCase));
            return node is null
                ? AgvResult<MapNodeDto>.Fail(FailureCode.MapNodeNotFound, $"Node '{request.NodeId}' was not found.")
                : AgvResult<MapNodeDto>.Ok(node);
        }

        /// <inheritdoc />
        public AgvResult<MapEdgeDto> GetEdge(GetMapEdgeRequest request)
        {
            var snapshot = LoadSnapshot();
            if (!snapshot.Success || snapshot.Data is null)
            {
                return AgvResult<MapEdgeDto>.Fail(snapshot.Code, snapshot.Message);
            }

            var edge = snapshot.Data.Edges.FirstOrDefault(item =>
                string.Equals(item.EdgeId, request.EdgeId, StringComparison.OrdinalIgnoreCase));
            return edge is null
                ? AgvResult<MapEdgeDto>.Fail(FailureCode.MapEdgeNotFound, $"Edge '{request.EdgeId}' was not found.")
                : AgvResult<MapEdgeDto>.Ok(edge);
        }

        /// <inheritdoc />
        public AgvResult<bool> NodeExists(GetMapNodeRequest request)
        {
            var result = GetNode(request);
            return result.Success
                ? AgvResult<bool>.Ok(true)
                : result.Code == FailureCode.MapNodeNotFound
                    ? AgvResult<bool>.Ok(false)
                    : AgvResult<bool>.Fail(result.Code, result.Message);
        }

        /// <inheritdoc />
        public AgvResult<bool> EdgeExists(GetMapEdgeRequest request)
        {
            var result = GetEdge(request);
            return result.Success
                ? AgvResult<bool>.Ok(true)
                : result.Code == FailureCode.MapEdgeNotFound
                    ? AgvResult<bool>.Ok(false)
                    : AgvResult<bool>.Fail(result.Code, result.Message);
        }

        /// <inheritdoc />
        public AgvResult<IReadOnlyList<MapEdgeDto>> GetOutgoingEdges(GetOutgoingEdgesRequest request)
        {
            var snapshot = LoadSnapshot();
            if (!snapshot.Success || snapshot.Data is null)
            {
                return AgvResult<IReadOnlyList<MapEdgeDto>>.Fail(snapshot.Code, snapshot.Message);
            }

            IReadOnlyList<MapEdgeDto> edges = snapshot.Data.Edges.Where(edge =>
                string.Equals(edge.FromNodeId, request.NodeId, StringComparison.OrdinalIgnoreCase) ||
                edge.Direction == MapEdgeDirection.Bidirectional &&
                string.Equals(edge.ToNodeId, request.NodeId, StringComparison.OrdinalIgnoreCase)).ToArray();
            return AgvResult<IReadOnlyList<MapEdgeDto>>.Ok(edges);
        }

        /// <inheritdoc />
        public AgvResult<IReadOnlyList<MapNodeDto>> GetNodesByType(GetNodesByTypeRequest request)
        {
            var snapshot = LoadSnapshot();
            if (!snapshot.Success || snapshot.Data is null)
            {
                return AgvResult<IReadOnlyList<MapNodeDto>>.Fail(snapshot.Code, snapshot.Message);
            }

            IReadOnlyList<MapNodeDto> nodes = snapshot.Data.Nodes
                .Where(node => node.NodeType == request.NodeType)
                .ToArray();
            return AgvResult<IReadOnlyList<MapNodeDto>>.Ok(nodes);
        }

        /// <inheritdoc />
        public AgvResult<string> GetVendorNodeCode(GetVendorNodeCodeRequest request)
        {
            var snapshot = LoadSnapshot();
            if (!snapshot.Success || snapshot.Data is null)
            {
                return AgvResult<string>.Fail(snapshot.Code, snapshot.Message);
            }

            var mapping = snapshot.Data.VendorNodeMappings.FirstOrDefault(item =>
                string.Equals(item.SystemNodeId, request.SystemNodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.VendorCode, request.VendorCode, StringComparison.OrdinalIgnoreCase));
            return mapping is null
                ? AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Vendor node mapping was not found.")
                : AgvResult<string>.Ok(mapping.VendorNodeCode);
        }

        /// <inheritdoc />
        public AgvResult<string> GetSystemNodeId(GetSystemNodeIdRequest request)
        {
            var snapshot = LoadSnapshot();
            if (!snapshot.Success || snapshot.Data is null)
            {
                return AgvResult<string>.Fail(snapshot.Code, snapshot.Message);
            }

            var mapping = snapshot.Data.VendorNodeMappings.FirstOrDefault(item =>
                string.Equals(item.VendorNodeCode, request.VendorNodeCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.VendorCode, request.VendorCode, StringComparison.OrdinalIgnoreCase));
            return mapping is null
                ? AgvResult<string>.Fail(FailureCode.VendorNodeMappingNotFound, "Vendor node mapping was not found.")
                : AgvResult<string>.Ok(mapping.SystemNodeId);
        }

        // Legacy in-process API retained while older callers migrate to IMapService.
        public IReadOnlyList<MapNode> GetNodes() => _maps.GetNodesAsync().GetAwaiter().GetResult();

        public IReadOnlyList<MapEdge> GetEdges() => _maps.GetEdgesAsync().GetAwaiter().GetResult();

        public MapNode? GetNode(string nodeId) => GetNodes().FirstOrDefault(node =>
            string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));

        public MapEdge? GetEdge(string edgeId) => GetEdges().FirstOrDefault(edge =>
            string.Equals(edge.EdgeId, edgeId, StringComparison.OrdinalIgnoreCase));

        public bool NodeExists(string nodeId) => GetNode(nodeId) is not null;

        public PlannedPath FindPlannedPath(string startNodeId, string endNodeId) =>
            _pathPlanningService.PlanPath(GetNodes(), GetEdges(), startNodeId, endNodeId);

        public PlannedPath FindPlannedPath(PathPlanningRequest request) =>
            FindPlannedPath(request.StartNodeId, request.EndNodeId);

        public bool IsPathAvailable(string startNodeId, string endNodeId) =>
            FindPlannedPath(startNodeId, endNodeId).IsAvailable;

        public double GetPathDistance(string startNodeId, string endNodeId)
        {
            var path = FindPlannedPath(startNodeId, endNodeId);
            return path.IsAvailable ? path.TotalLength : double.PositiveInfinity;
        }

        public IReadOnlyList<MapNode> FindReachableNodes(string startNodeId) =>
            GetNodes().Where(node => node.NodeId != startNodeId).ToArray();

        public IReadOnlyList<MapNode> FindNodesByType(LegacyMapNodeType nodeType) =>
            GetNodes().Where(node => node.NodeType == nodeType).ToArray();

        private AgvResult<MapSnapshotDto> LoadSnapshot()
        {
            try
            {
                var allNodes = _maps.GetNodesAsync().GetAwaiter().GetResult();
                var allEdges = _maps.GetEdgesAsync().GetAwaiter().GetResult();
                var allAliases = _aliases.GetAllAsync().GetAwaiter().GetResult();
                var mapId = allNodes.Select(node => node.MapId)
                    .Concat(allEdges.Select(edge => edge.MapId))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                if (string.IsNullOrWhiteSpace(mapId))
                {
                    return AgvResult<MapSnapshotDto>.Fail(FailureCode.MapNotLoaded, "No runtime map is stored.");
                }

                var sourceNodes = allNodes.Where(node =>
                    string.Equals(node.MapId, mapId, StringComparison.OrdinalIgnoreCase)).ToArray();
                var sourceEdges = allEdges.Where(edge =>
                    string.Equals(edge.MapId, mapId, StringComparison.OrdinalIgnoreCase)).ToArray();
                var nodes = sourceNodes.Select(ToContractNode).ToArray();
                var edges = sourceEdges.Select(ToContractEdge).ToArray();
                var mappings = allAliases.Where(alias =>
                        alias.IsEnabled &&
                        string.Equals(alias.MapId, mapId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(alias.AliasValue))
                    .Select(alias => new VendorNodeMappingDto
                    {
                        VendorCode = !string.IsNullOrWhiteSpace(alias.Brand) ? alias.Brand : alias.AliasType,
                        SystemNodeId = alias.NodeId,
                        VendorNodeCode = alias.AliasValue
                    })
                    .Where(mapping => !string.IsNullOrWhiteSpace(mapping.VendorCode))
                    .ToArray();

                return AgvResult<MapSnapshotDto>.Ok(new MapSnapshotDto
                {
                    MapId = mapId,
                    MapName = mapId,
                    Version = ComputeVersion(sourceNodes, sourceEdges, mappings),
                    Nodes = nodes,
                    Edges = edges,
                    VendorNodeMappings = mappings,
                    UpdatedAt = DateTimeOffset.Now
                });
            }
            catch (Exception exception)
            {
                return AgvResult<MapSnapshotDto>.Fail(
                    FailureCode.MapNotLoaded,
                    $"Could not load the runtime map: {exception.Message}");
            }
        }

        private static AgvResult<MapSnapshotDto> ValidateVersion(MapSnapshotDto snapshot, string? expectedVersion) =>
            !string.IsNullOrWhiteSpace(expectedVersion) &&
            !string.Equals(snapshot.Version, expectedVersion, StringComparison.Ordinal)
                ? AgvResult<MapSnapshotDto>.Fail(FailureCode.MapVersionMismatch, "Map version mismatch.")
                : AgvResult<MapSnapshotDto>.Ok(snapshot);

        private static MapNodeDto ToContractNode(MapNode node)
        {
            var properties = new Dictionary<string, string>(node.Tags, StringComparer.OrdinalIgnoreCase);
            AddProperty(properties, "AreaCode", node.AreaCode);
            AddProperty(properties, "AllowedBrands", node.AllowedBrands);
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

        private static MapEdgeDto ToContractEdge(MapEdge edge)
        {
            var reverse = edge.Direction == LegacyEdgeDirection.ReverseOnly;
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddProperty(properties, "AllowedBrands", edge.AllowedBrands);
            AddProperty(properties, "Remark", edge.Remark);
            return new MapEdgeDto
            {
                EdgeId = edge.EdgeId,
                FromNodeId = reverse ? edge.ToNodeId : edge.FromNodeId,
                ToNodeId = reverse ? edge.FromNodeId : edge.ToNodeId,
                Distance = edge.Length,
                Direction = edge.Direction == LegacyEdgeDirection.Bidirectional
                    ? MapEdgeDirection.Bidirectional
                    : MapEdgeDirection.OneWay,
                EdgeType = MapEdgeType.Normal,
                Cost = edge.Cost,
                SpeedLimit = edge.MaxSpeed > 0 ? edge.MaxSpeed : null,
                AreaId = string.IsNullOrWhiteSpace(edge.AreaCode) ? null : edge.AreaCode,
                Enabled = edge.IsEnabled && edge.Direction != LegacyEdgeDirection.Closed,
                Properties = properties
            };
        }

        private static MapNodeType ToContractNodeType(LegacyMapNodeType nodeType) => nodeType switch
        {
            LegacyMapNodeType.Station => MapNodeType.WorkStation,
            LegacyMapNodeType.Pickup => MapNodeType.PickPoint,
            LegacyMapNodeType.Dropoff => MapNodeType.PutPoint,
            LegacyMapNodeType.Charge => MapNodeType.ChargeStation,
            LegacyMapNodeType.Waiting => MapNodeType.WaitingPoint,
            LegacyMapNodeType.Elevator => MapNodeType.Elevator,
            LegacyMapNodeType.Door => MapNodeType.Door,
            LegacyMapNodeType.Normal or LegacyMapNodeType.Intersection => MapNodeType.Normal,
            _ => MapNodeType.Unknown
        };

        private static string ComputeVersion(
            IEnumerable<MapNode> nodes,
            IEnumerable<MapEdge> edges,
            IEnumerable<VendorNodeMappingDto> mappings)
        {
            var content = string.Join("\n",
                nodes.OrderBy(node => node.NodeId).Select(node =>
                    $"N|{node.NodeId}|{node.NodeCode}|{node.NodeType}|{node.Position.X:R}|{node.Position.Y:R}|{node.IsEnabled}")
                .Concat(edges.OrderBy(edge => edge.EdgeId).Select(edge =>
                    $"E|{edge.EdgeId}|{edge.FromNodeId}|{edge.ToNodeId}|{edge.Direction}|{edge.Length:R}|{edge.Cost}|{edge.IsEnabled}"))
                .Concat(mappings.OrderBy(mapping => mapping.VendorCode).ThenBy(mapping => mapping.SystemNodeId).Select(mapping =>
                    $"V|{mapping.VendorCode}|{mapping.SystemNodeId}|{mapping.VendorNodeCode}")));
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return $"db-{Convert.ToHexString(hash)[..12]}";
        }

        private static void AddProperty(IDictionary<string, string> properties, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                properties[key] = value;
            }
        }
    }
}
