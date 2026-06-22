using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.MapManagement.Interfaces;
using AgvDispatcher.Core.Contracts.MapManagement.Requests;
using AgvDispatcher.Core.Contracts.MapManagement.Results;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Events;
using LegacyEdgeDirection = AgvDispatcher.Core.Enums.EdgeDirection;
using LegacyMapNodeType = AgvDispatcher.Core.Enums.MapNodeType;
using VehicleCapability = AgvDispatcher.Core.Enums.VehicleCapability;

namespace AgvDispatcher.Modules.MapModule.Services
{
    /// <summary>
    /// In-memory map management service. 草稿保存不影响当前运行地图，发布/回滚才切换运行版本。
    /// </summary>
    public sealed class MockMapManagementService : IMapManagementService
    {
        private readonly MockMapStore _store;
        private readonly MapStaticValidator _validator;
        private readonly IEventAggregator _eventAggregator;
        private readonly IMapRepository _mapRepository;
        private readonly IMapLocationAliasRepository _aliasRepository;

        public MockMapManagementService(
            MockMapStore store,
            MapStaticValidator validator,
            IEventAggregator eventAggregator,
            IMapRepository mapRepository,
            IMapLocationAliasRepository aliasRepository)
        {
            _store = store;
            _validator = validator;
            _eventAggregator = eventAggregator;
            _mapRepository = mapRepository;
            _aliasRepository = aliasRepository;
        }

        public Task<AgvResult<MapDraftDto>> CreateDraftAsync(
            CreateMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            var draft = _store.CreateDraft(request.MapName, request.SourceVersion, request.Context.OperatorId);
            return Task.FromResult(AgvResult<MapDraftDto>.Ok(draft));
        }

        public Task<AgvResult<MapDraftDto>> GetDraftAsync(
            GetMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            var draft = _store.GetDraft(request.DraftId);
            return Task.FromResult(draft is null
                ? AgvResult<MapDraftDto>.Fail(FailureCode.InvalidRequest, $"Draft '{request.DraftId}' was not found.")
                : AgvResult<MapDraftDto>.Ok(draft));
        }

        public Task<AgvResult<MapDraftDto>> SaveDraftAsync(
            SaveMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            // 保存草稿只更新草稿区，不切换运行图，也不触发 MapPublishedEvent。
            var draft = _store.SaveDraft(request.DraftId, request.Map, request.Context.OperatorId);
            return Task.FromResult(AgvResult<MapDraftDto>.Ok(draft));
        }

        public Task<AgvResult<MapValidationResultDto>> ValidateDraftAsync(
            ValidateMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            var draft = _store.GetDraft(request.DraftId);
            if (draft is null)
            {
                return Task.FromResult(AgvResult<MapValidationResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    $"Draft '{request.DraftId}' was not found."));
            }

            return Task.FromResult(AgvResult<MapValidationResultDto>.Ok(_validator.Validate(draft.Map)));
        }

        public async Task<AgvResult<MapPublishResultDto>> PublishDraftAsync(
            PublishMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            var validation = await ValidateDraftAsync(
                new ValidateMapDraftRequest { Context = request.Context, DraftId = request.DraftId },
                cancellationToken);

            if (!validation.Success || validation.Data?.IsValid != true)
            {
                return AgvResult<MapPublishResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    validation.Data is null ? validation.Message : string.Join("; ", validation.Data.Messages));
            }

            var (snapshot, version) = _store.PublishDraft(request.DraftId, request.Context.OperatorId);
            await PersistCurrentMapAsync(snapshot, cancellationToken);
            PublishMapChanged(snapshot.MapId, snapshot.Version, version.PublishedAt ?? DateTimeOffset.Now, request.Context.OperatorId);

            return AgvResult<MapPublishResultDto>.Ok(new MapPublishResultDto
            {
                MapId = snapshot.MapId,
                Version = snapshot.Version,
                PublishedAt = version.PublishedAt ?? DateTimeOffset.Now,
                OperatorId = request.Context.OperatorId
            });
        }

        public Task<AgvResult<IReadOnlyList<MapVersionDto>>> GetMapVersionsAsync(
            GetMapVersionsRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AgvResult<IReadOnlyList<MapVersionDto>>.Ok(_store.GetVersions(request.MapId)));
        }

        public Task<AgvResult<MapRollbackResultDto>> RollbackToVersionAsync(
            RollbackMapVersionRequest request,
            CancellationToken cancellationToken = default)
        {
            var version = _store.Rollback(request.MapId, request.Version, request.Context.OperatorId);
            if (version is null)
            {
                return Task.FromResult(AgvResult<MapRollbackResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    $"Map version '{request.MapId}/{request.Version}' was not found."));
            }

            var now = DateTimeOffset.Now;
            var snapshot = _store.GetPublishedSnapshot(request.MapId, request.Version) ?? _store.GetCurrentMap();
            PersistCurrentMapAsync(snapshot, cancellationToken).GetAwaiter().GetResult();
            PublishMapChanged(request.MapId, request.Version, now, request.Context.OperatorId);
            return Task.FromResult(AgvResult<MapRollbackResultDto>.Ok(new MapRollbackResultDto
            {
                MapId = request.MapId,
                Version = request.Version,
                RolledBackAt = now,
                OperatorId = request.Context.OperatorId
            }));
        }

        private async Task PersistCurrentMapAsync(MapSnapshotDto snapshot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nextNodeIds = snapshot.Nodes
                .Select(node => node.NodeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var currentNodes = await _mapRepository.GetNodesAsync();
            foreach (var node in currentNodes.Where(node =>
                string.Equals(node.MapId, snapshot.MapId, StringComparison.OrdinalIgnoreCase)
                && !nextNodeIds.Contains(node.NodeId)))
            {
                await _mapRepository.DeleteNodeAsync(node.NodeId);
            }

            foreach (var node in snapshot.Nodes.Select(node => ToLegacyNode(snapshot.MapId, node)))
            {
                await _mapRepository.SaveNodeAsync(node);
            }

            var nextEdgeIds = snapshot.Edges
                .Select(edge => edge.EdgeId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var currentEdges = await _mapRepository.GetEdgesAsync();
            foreach (var edge in currentEdges.Where(edge =>
                string.Equals(edge.MapId, snapshot.MapId, StringComparison.OrdinalIgnoreCase)
                && !nextEdgeIds.Contains(edge.EdgeId)))
            {
                await _mapRepository.DeleteEdgeAsync(edge.EdgeId);
            }

            foreach (var edge in snapshot.Edges.Select(edge => ToLegacyEdge(snapshot.MapId, edge)))
            {
                await _mapRepository.SaveEdgeAsync(edge);
            }

            var currentAliases = await _aliasRepository.GetAllAsync();
            foreach (var alias in currentAliases.Where(alias =>
                string.Equals(alias.MapId, snapshot.MapId, StringComparison.OrdinalIgnoreCase)))
            {
                await _aliasRepository.DeleteAsync(alias.AliasId);
            }

            foreach (var alias in snapshot.VendorNodeMappings.Select(mapping => ToLegacyAlias(snapshot.MapId, mapping)))
            {
                await _aliasRepository.SaveAsync(alias);
            }
        }

        private static MapNode ToLegacyNode(string mapId, MapNodeDto node)
        {
            return new MapNode
            {
                NodeId = node.NodeId,
                MapId = mapId,
                NodeCode = string.IsNullOrWhiteSpace(node.NodeCode) ? node.NodeId : node.NodeCode,
                Name = string.IsNullOrWhiteSpace(node.NodeName) ? node.NodeId : node.NodeName,
                NodeType = ToLegacyNodeType(node.NodeType),
                Position = new MapPosition { MapId = mapId, NodeId = node.NodeId, X = node.X, Y = node.Y, AreaCode = node.AreaId },
                Heading = node.Angle ?? 0,
                AreaCode = node.AreaId ?? string.Empty,
                IsEnabled = node.Enabled,
                ParkingCapacity = TryReadInt(node.Properties, "Capacity", 1),
                AllowedBrands = TryReadString(node.Properties, "AllowedBrands"),
                RequiredCapabilities = (VehicleCapability)TryReadInt(node.Properties, "RequiredCapabilities", 0),
                Tags = node.Properties
                    .Where(item => item.Key is not ("Capacity" or "AllowedBrands" or "RequiredCapabilities"))
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
            };
        }

        private static MapEdge ToLegacyEdge(string mapId, MapEdgeDto edge)
        {
            return new MapEdge
            {
                EdgeId = string.IsNullOrWhiteSpace(edge.EdgeId) ? $"E-{edge.FromNodeId}-{edge.ToNodeId}" : edge.EdgeId,
                MapId = mapId,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Direction = edge.Enabled
                    ? edge.Direction == MapEdgeDirection.Bidirectional ? LegacyEdgeDirection.Bidirectional : LegacyEdgeDirection.ForwardOnly
                    : LegacyEdgeDirection.Closed,
                Length = edge.Distance,
                MaxSpeed = edge.SpeedLimit ?? 0,
                Cost = Math.Max(1, (int)Math.Round(edge.Cost <= 0 ? edge.Distance : edge.Cost)),
                IsEnabled = edge.Enabled,
                AreaCode = edge.AreaId ?? string.Empty,
                AllowedBrands = TryReadString(edge.Properties, "AllowedBrands"),
                MaxVehicleFlow = TryReadInt(edge.Properties, "MaxVehicleFlow", 1),
                Remark = TryReadString(edge.Properties, "Remark")
            };
        }

        private static MapLocationAlias ToLegacyAlias(string mapId, VendorNodeMappingDto mapping)
        {
            var vendorCode = string.IsNullOrWhiteSpace(mapping.VendorCode) ? "GLOBAL" : mapping.VendorCode;
            return new MapLocationAlias
            {
                AliasId = $"{vendorCode}-{mapping.SystemNodeId}",
                MapId = mapId,
                NodeId = mapping.SystemNodeId,
                AliasType = "Vendor",
                AliasValue = mapping.VendorNodeCode,
                Brand = string.Equals(vendorCode, "GLOBAL", StringComparison.OrdinalIgnoreCase) ? string.Empty : vendorCode,
                IsEnabled = true
            };
        }

        private static LegacyMapNodeType ToLegacyNodeType(MapNodeType nodeType) => nodeType switch
        {
            MapNodeType.WorkStation => LegacyMapNodeType.Station,
            MapNodeType.PickPoint => LegacyMapNodeType.Pickup,
            MapNodeType.PutPoint => LegacyMapNodeType.Dropoff,
            MapNodeType.ChargeStation => LegacyMapNodeType.Charge,
            MapNodeType.WaitingPoint => LegacyMapNodeType.Waiting,
            MapNodeType.Elevator => LegacyMapNodeType.Elevator,
            MapNodeType.Door => LegacyMapNodeType.Door,
            _ => LegacyMapNodeType.Normal
        };

        private static int TryReadInt(IReadOnlyDictionary<string, string> properties, string key, int fallback)
        {
            return properties.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;
        }

        private static string TryReadString(IReadOnlyDictionary<string, string> properties, string key)
        {
            return properties.TryGetValue(key, out var value) ? value : string.Empty;
        }

        public Task<AgvResult<MapExportResultDto>> ExportMapAsync(
            ExportMapRequest request,
            CancellationToken cancellationToken = default)
        {
            var snapshot = string.IsNullOrWhiteSpace(request.Version)
                ? _store.GetCurrentMap()
                : _store.GetPublishedSnapshot(request.MapId, request.Version);
            if (snapshot is null)
            {
                return Task.FromResult(AgvResult<MapExportResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    $"Map '{request.MapId}/{request.Version}' was not found."));
            }

            var payload = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            return Task.FromResult(AgvResult<MapExportResultDto>.Ok(new MapExportResultDto
            {
                Format = request.Format,
                Payload = payload,
                ExportedAt = DateTimeOffset.Now
            }));
        }

        public Task<AgvResult<MapImportResultDto>> ImportMapAsync(
            ImportMapRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> runtimeFields;
            MapSnapshotDto? map;
            try
            {
                runtimeFields = CollectJsonFieldNames(request.Payload);
                map = JsonSerializer.Deserialize<MapSnapshotDto>(
                    request.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                return Task.FromResult(AgvResult<MapImportResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    $"Import payload is not valid json: {ex.Message}"));
            }

            if (map is null)
            {
                return Task.FromResult(AgvResult<MapImportResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    "Import payload is not a valid map snapshot."));
            }

            var validation = _validator.Validate(map, runtimeFields);
            if (!validation.IsValid)
            {
                return Task.FromResult(AgvResult<MapImportResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    string.Join("; ", validation.Messages)));
            }

            // 外部导入只能生成草稿，不能直接覆盖当前运行图。
            var draft = _store.SaveDraft(Guid.NewGuid().ToString("N"), map, request.Context.OperatorId);
            return Task.FromResult(AgvResult<MapImportResultDto>.Ok(new MapImportResultDto
            {
                Draft = draft,
                Warnings = validation.Messages.Where(message => message.StartsWith("P1", StringComparison.OrdinalIgnoreCase)).ToList()
            }));
        }

        private void PublishMapChanged(
            string mapId,
            string version,
            DateTimeOffset publishedAt,
            string? operatorId)
        {
            // 版本指针已在 Store 内切换完成，事件在外部发布，避免锁内回调订阅者。
            _eventAggregator.GetEvent<PubSubEvent<MapPublishedEvent>>().Publish(new MapPublishedEvent
            {
                MapId = mapId,
                MapVersion = version,
                PublishedAt = publishedAt.DateTime,
                OperatorId = operatorId
            });
        }

        private static IReadOnlyList<string> CollectJsonFieldNames(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return Array.Empty<string>();
            }

            using var document = JsonDocument.Parse(payload);
            var names = new List<string>();
            CollectJsonFieldNames(document.RootElement, names);
            return names;
        }

        private static void CollectJsonFieldNames(JsonElement element, ICollection<string> names)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    CollectJsonFieldNames(property.Value, names);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    CollectJsonFieldNames(item, names);
                }
            }
        }
    }
}
