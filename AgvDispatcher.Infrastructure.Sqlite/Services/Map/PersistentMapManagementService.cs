using System.Text.Json;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.MapManagement.Interfaces;
using AgvDispatcher.Core.Contracts.MapManagement.Requests;
using AgvDispatcher.Core.Contracts.MapManagement.Results;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;
using Prism.Events;
using ContractEdgeDirection = AgvDispatcher.Core.Contracts.Map.MapEdgeDirection;
using ContractNodeType = AgvDispatcher.Core.Contracts.Map.MapNodeType;
using LegacyEdgeDirection = AgvDispatcher.Core.Enums.EdgeDirection;
using LegacyMapNodeType = AgvDispatcher.Core.Enums.MapNodeType;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public sealed class PersistentMapManagementService : IMapManagementService
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;
        private readonly IEventAggregator _events;

        public PersistentMapManagementService(
            DbContextOptions<AgvDispatcherDbContext> options,
            IEventAggregator events)
        {
            _options = options;
            _events = events;
        }

        private AgvDispatcherDbContext CreateContext() => new(_options);

        public async Task<AgvResult<MapDraftDto>> CreateDraftAsync(
            CreateMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            var now = DateTimeOffset.Now;
            var source = await ResolveSourceVersionAsync(db, request.SourceMapId, request.SourceVersion, cancellationToken);
            var mapId = source?.MapId ?? Guid.NewGuid().ToString("N");
            var mapVersion = $"draft-{now:yyyyMMddHHmmssfff}";
            var name = string.IsNullOrWhiteSpace(request.MapName)
                ? source?.Name ?? "Untitled Map"
                : request.MapName;

            var version = new MapVersionEntity
            {
                MapId = mapId,
                MapVersion = mapVersion,
                Name = name,
                State = MapState.Draft,
                BaseMapId = source?.MapId,
                BaseMapVersion = source?.MapVersion,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = request.Context.OperatorId
            };
            db.MapVersions.Add(version);

            if (source is not null)
            {
                var nodes = await db.MapNodes.AsNoTracking()
                    .Where(node => node.MapId == source.MapId && node.MapVersion == source.MapVersion)
                    .ToArrayAsync(cancellationToken);
                var edges = await db.MapEdges.AsNoTracking()
                    .Where(edge => edge.MapId == source.MapId && edge.MapVersion == source.MapVersion)
                    .ToArrayAsync(cancellationToken);
                var aliases = await db.MapLocationAliases.AsNoTracking()
                    .Where(alias => alias.MapId == source.MapId && alias.MapVersion == source.MapVersion)
                    .ToArrayAsync(cancellationToken);
                db.MapNodes.AddRange(nodes.Select(node => CopyNode(node, mapId, mapVersion)));
                db.MapEdges.AddRange(edges.Select(edge => CopyEdge(edge, mapId, mapVersion)));
                db.MapLocationAliases.AddRange(aliases.Select(alias => CopyAlias(alias, mapId, mapVersion)));
            }

            await db.SaveChangesAsync(cancellationToken);
            return AgvResult<MapDraftDto>.Ok(await ToDraftAsync(db, version, cancellationToken));
        }

        public async Task<AgvResult<MapDraftDto>> GetDraftAsync(
            GetMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            var version = await db.MapVersions.AsNoTracking()
                .FirstOrDefaultAsync(item => item.MapVersion == request.DraftId && item.State == MapState.Draft, cancellationToken);
            return version is null
                ? AgvResult<MapDraftDto>.Fail(FailureCode.InvalidRequest, $"Draft '{request.DraftId}' was not found.")
                : AgvResult<MapDraftDto>.Ok(await ToDraftAsync(db, version, cancellationToken));
        }

        public async Task<AgvResult<MapDraftDto>> SaveDraftAsync(
            SaveMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            var version = await db.MapVersions
                .FirstOrDefaultAsync(item => item.MapVersion == request.DraftId, cancellationToken);
            if (version is null || version.State != MapState.Draft)
            {
                return AgvResult<MapDraftDto>.Fail(FailureCode.InvalidState, "Only draft maps can be edited.");
            }

            db.MapNodes.RemoveRange(db.MapNodes.Where(node => node.MapId == version.MapId && node.MapVersion == version.MapVersion));
            db.MapEdges.RemoveRange(db.MapEdges.Where(edge => edge.MapId == version.MapId && edge.MapVersion == version.MapVersion));
            db.MapLocationAliases.RemoveRange(db.MapLocationAliases.Where(alias => alias.MapId == version.MapId && alias.MapVersion == version.MapVersion));
            db.MapNodes.AddRange(request.Map.Nodes.Select(node => ToModelNode(node, version.MapId, version.MapVersion)));
            db.MapEdges.AddRange(request.Map.Edges.Select(edge => ToModelEdge(edge, version.MapId, version.MapVersion)));
            db.MapLocationAliases.AddRange(request.Map.VendorNodeMappings.Select(mapping => ToAlias(mapping, version.MapId, version.MapVersion)));
            version.Name = string.IsNullOrWhiteSpace(request.Map.MapName) ? version.Name : request.Map.MapName;
            version.UpdatedAt = DateTimeOffset.Now;
            version.Description = request.Comment;
            await db.SaveChangesAsync(cancellationToken);
            return AgvResult<MapDraftDto>.Ok(await ToDraftAsync(db, version, cancellationToken));
        }

        public async Task<AgvResult<MapValidationResultDto>> ValidateDraftAsync(
            ValidateMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            var version = await db.MapVersions.AsNoTracking()
                .FirstOrDefaultAsync(item => item.MapVersion == request.DraftId, cancellationToken);
            if (version is null || version.State != MapState.Draft)
            {
                return AgvResult<MapValidationResultDto>.Fail(FailureCode.InvalidRequest, $"Draft '{request.DraftId}' was not found.");
            }

            var messages = await ValidateVersionAsync(db, version.MapId, version.MapVersion, cancellationToken);
            return AgvResult<MapValidationResultDto>.Ok(new MapValidationResultDto
            {
                IsValid = messages.Count == 0,
                Messages = messages
            });
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

            await using var db = CreateContext();
            var version = await db.MapVersions
                .FirstAsync(item => item.MapVersion == request.DraftId, cancellationToken);
            var now = DateTimeOffset.Now;
            version.State = MapState.Published;
            version.IsActive = false;
            version.PublishedAt = now;
            version.UpdatedAt = now;
            version.Description = request.Comment;
            await db.SaveChangesAsync(cancellationToken);
            return AgvResult<MapPublishResultDto>.Ok(new MapPublishResultDto
            {
                MapId = version.MapId,
                Version = version.MapVersion,
                PublishedAt = now,
                OperatorId = request.Context.OperatorId
            });
        }

        public async Task<AgvResult<MapActivationResultDto>> ActivateMapAsync(
            ActivateMapRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            if (await db.TaskOrders.AsNoTracking().AnyAsync(
                    task => task.State == TaskState.Running || task.State == TaskState.Pending,
                    cancellationToken))
            {
                return AgvResult<MapActivationResultDto>.Fail(
                    FailureCode.InvalidState,
                    "Cannot activate map while dispatching tasks are running.");
            }

            var target = await db.MapVersions
                .FirstOrDefaultAsync(item => item.MapId == request.MapId && item.MapVersion == request.Version, cancellationToken);
            if (target is null || target.State is not (MapState.Published or MapState.Active))
            {
                return AgvResult<MapActivationResultDto>.Fail(FailureCode.InvalidState, "Only published maps can be activated.");
            }

            var messages = await ValidateVersionAsync(db, target.MapId, target.MapVersion, cancellationToken);
            if (messages.Count > 0)
            {
                return AgvResult<MapActivationResultDto>.Fail(FailureCode.InvalidRequest, string.Join("; ", messages));
            }

            var old = await db.MapVersions.FirstOrDefaultAsync(item => item.IsActive || item.State == MapState.Active, cancellationToken);
            var now = DateTimeOffset.Now;
            foreach (var version in await db.MapVersions.ToArrayAsync(cancellationToken))
            {
                if (version.MapId == target.MapId && version.MapVersion == target.MapVersion)
                {
                    version.State = MapState.Active;
                    version.IsActive = true;
                    version.ActivatedAt = now;
                    version.UpdatedAt = now;
                }
                else if (version.IsActive || version.State == MapState.Active)
                {
                    version.State = MapState.Archived;
                    version.IsActive = false;
                    version.UpdatedAt = now;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            _events.GetEvent<PubSubEvent<ActiveMapChangedEvent>>().Publish(new ActiveMapChangedEvent
            {
                OldMapId = old?.MapId,
                OldMapVersion = old?.MapVersion,
                NewMapId = target.MapId,
                NewMapVersion = target.MapVersion,
                ChangedAt = now.DateTime,
                OperatorId = request.Context.OperatorId,
                Reason = request.Reason
            });

            return AgvResult<MapActivationResultDto>.Ok(new MapActivationResultDto
            {
                OldMapId = old?.MapId,
                OldVersion = old?.MapVersion,
                MapId = target.MapId,
                Version = target.MapVersion,
                ActivatedAt = now,
                OperatorId = request.Context.OperatorId
            });
        }

        public async Task<AgvResult<IReadOnlyList<MapVersionDto>>> GetMapVersionsAsync(
            GetMapVersionsRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            var query = db.MapVersions.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.MapId))
            {
                query = query.Where(item => item.MapId == request.MapId);
            }

            var versions = (await query.ToArrayAsync(cancellationToken))
                .OrderByDescending(item => item.UpdatedAt)
                .Select(item => new MapVersionDto
                {
                    MapId = item.MapId,
                    MapName = item.Name,
                    Version = item.MapVersion,
                    IsCurrent = item.IsActive,
                    State = item.State,
                    PublishedAt = item.PublishedAt,
                    OperatorId = item.CreatedBy
                })
                .ToArray();
            return AgvResult<IReadOnlyList<MapVersionDto>>.Ok(versions);
        }

        public async Task<AgvResult<MapRollbackResultDto>> RollbackToVersionAsync(
            RollbackMapVersionRequest request,
            CancellationToken cancellationToken = default)
        {
            var activation = await ActivateMapAsync(new ActivateMapRequest
            {
                Context = request.Context,
                MapId = request.MapId,
                Version = request.Version,
                Reason = request.Reason
            }, cancellationToken);

            return activation.Success && activation.Data is not null
                ? AgvResult<MapRollbackResultDto>.Ok(new MapRollbackResultDto
                {
                    MapId = activation.Data.MapId,
                    Version = activation.Data.Version,
                    RolledBackAt = activation.Data.ActivatedAt,
                    OperatorId = activation.Data.OperatorId
                })
                : AgvResult<MapRollbackResultDto>.Fail(activation.Code, activation.Message);
        }

        public async Task<AgvResult> DeleteDraftAsync(
            DeleteMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            var version = await db.MapVersions
                .FirstOrDefaultAsync(item => item.MapVersion == request.DraftId, cancellationToken);
            if (version is null || version.State != MapState.Draft)
            {
                return AgvResult.Fail(FailureCode.InvalidState, "Only draft maps can be deleted.");
            }

            db.MapNodes.RemoveRange(db.MapNodes.Where(node => node.MapId == version.MapId && node.MapVersion == version.MapVersion));
            db.MapEdges.RemoveRange(db.MapEdges.Where(edge => edge.MapId == version.MapId && edge.MapVersion == version.MapVersion));
            db.MapLocationAliases.RemoveRange(db.MapLocationAliases.Where(alias => alias.MapId == version.MapId && alias.MapVersion == version.MapVersion));
            db.MapVersions.Remove(version);
            await db.SaveChangesAsync(cancellationToken);
            return AgvResult.Ok();
        }

        public async Task<AgvResult> ArchiveMapAsync(
            ArchiveMapVersionRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            var version = await db.MapVersions
                .FirstOrDefaultAsync(item => item.MapId == request.MapId && item.MapVersion == request.Version, cancellationToken);
            if (version is null)
            {
                return AgvResult.Fail(FailureCode.InvalidRequest, "Map version was not found.");
            }

            if (version.IsActive)
            {
                return AgvResult.Fail(FailureCode.InvalidState, "Cannot archive the active map.");
            }

            version.State = MapState.Archived;
            version.UpdatedAt = DateTimeOffset.Now;
            await db.SaveChangesAsync(cancellationToken);
            return AgvResult.Ok();
        }

        public async Task<AgvResult<MapExportResultDto>> ExportMapAsync(
            ExportMapRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateContext();
            var version = await ResolveSourceVersionAsync(db, request.MapId, request.Version, cancellationToken);
            if (version is null)
            {
                return AgvResult<MapExportResultDto>.Fail(FailureCode.InvalidRequest, "Map version was not found.");
            }

            var map = await ToSnapshotAsync(db, version, cancellationToken);
            return AgvResult<MapExportResultDto>.Ok(new MapExportResultDto
            {
                Format = request.Format,
                Payload = JsonSerializer.Serialize(map),
                ExportedAt = DateTimeOffset.Now
            });
        }

        public async Task<AgvResult<MapImportResultDto>> ImportMapAsync(
            ImportMapRequest request,
            CancellationToken cancellationToken = default)
        {
            var map = JsonSerializer.Deserialize<MapSnapshotDto>(request.Payload);
            if (map is null)
            {
                return AgvResult<MapImportResultDto>.Fail(FailureCode.InvalidRequest, "Import payload is not a valid map snapshot.");
            }

            var created = await CreateDraftAsync(new CreateMapDraftRequest
            {
                Context = request.Context,
                MapName = map.MapName
            }, cancellationToken);
            if (!created.Success || created.Data is null)
            {
                return AgvResult<MapImportResultDto>.Fail(created.Code, created.Message);
            }

            var saved = await SaveDraftAsync(new SaveMapDraftRequest
            {
                Context = request.Context,
                DraftId = created.Data.DraftId,
                Map = map,
                Comment = request.Comment
            }, cancellationToken);
            return saved.Success && saved.Data is not null
                ? AgvResult<MapImportResultDto>.Ok(new MapImportResultDto { Draft = saved.Data })
                : AgvResult<MapImportResultDto>.Fail(saved.Code, saved.Message);
        }

        private static async Task<MapVersionEntity?> ResolveSourceVersionAsync(
            AgvDispatcherDbContext db,
            string? mapId,
            string? version,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(mapId) && !string.IsNullOrWhiteSpace(version))
            {
                return await db.MapVersions.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.MapId == mapId && item.MapVersion == version, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(version))
            {
                return await db.MapVersions.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.MapVersion == version, cancellationToken);
            }

            return (await db.MapVersions.AsNoTracking()
                .Where(item => item.IsActive || item.State == MapState.Active)
                .ToArrayAsync(cancellationToken))
                .OrderByDescending(item => item.ActivatedAt)
                .FirstOrDefault();
        }

        private static async Task<MapDraftDto> ToDraftAsync(
            AgvDispatcherDbContext db,
            MapVersionEntity version,
            CancellationToken cancellationToken) => new()
        {
            DraftId = version.MapVersion,
            Map = await ToSnapshotAsync(db, version, cancellationToken),
            CreatedAt = version.CreatedAt,
            UpdatedAt = version.UpdatedAt,
            OperatorId = version.CreatedBy,
            State = version.State
        };

        private static async Task<MapSnapshotDto> ToSnapshotAsync(
            AgvDispatcherDbContext db,
            MapVersionEntity version,
            CancellationToken cancellationToken)
        {
            var nodes = await db.MapNodes.AsNoTracking()
                .Where(node => node.MapId == version.MapId && node.MapVersion == version.MapVersion)
                .OrderBy(node => node.NodeCode)
                .ToArrayAsync(cancellationToken);
            var edges = await db.MapEdges.AsNoTracking()
                .Where(edge => edge.MapId == version.MapId && edge.MapVersion == version.MapVersion)
                .OrderBy(edge => edge.EdgeId)
                .ToArrayAsync(cancellationToken);
            var aliases = await db.MapLocationAliases.AsNoTracking()
                .Where(alias => alias.MapId == version.MapId && alias.MapVersion == version.MapVersion && alias.IsEnabled)
                .ToArrayAsync(cancellationToken);
            return new MapSnapshotDto
            {
                MapId = version.MapId,
                MapName = version.Name,
                Version = version.MapVersion,
                Nodes = nodes.Select(ToDtoNode).ToArray(),
                Edges = edges.Select(ToDtoEdge).ToArray(),
                VendorNodeMappings = aliases.Select(alias => new VendorNodeMappingDto
                {
                    VendorCode = !string.IsNullOrWhiteSpace(alias.Brand) ? alias.Brand! : alias.AliasType,
                    SystemNodeId = alias.NodeId,
                    VendorNodeCode = alias.AliasValue
                }).ToArray(),
                UpdatedAt = version.UpdatedAt
            };
        }

        private static async Task<IReadOnlyList<string>> ValidateVersionAsync(
            AgvDispatcherDbContext db,
            string mapId,
            string mapVersion,
            CancellationToken cancellationToken)
        {
            var messages = new List<string>();
            var nodes = await db.MapNodes.AsNoTracking()
                .Where(node => node.MapId == mapId && node.MapVersion == mapVersion)
                .ToArrayAsync(cancellationToken);
            var edges = await db.MapEdges.AsNoTracking()
                .Where(edge => edge.MapId == mapId && edge.MapVersion == mapVersion)
                .ToArrayAsync(cancellationToken);
            var aliases = await db.MapLocationAliases.AsNoTracking()
                .Where(alias => alias.MapId == mapId && alias.MapVersion == mapVersion && alias.IsEnabled)
                .ToArrayAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(mapId)) messages.Add("MapId is required.");
            if (string.IsNullOrWhiteSpace(mapVersion)) messages.Add("MapVersion is required.");
            if (nodes.Length == 0) messages.Add("At least one node is required.");
            if (edges.Length == 0) messages.Add("At least one edge is required.");
            messages.AddRange(nodes.GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => $"Duplicate node IDs are not allowed: {group.Key}."));
            messages.AddRange(nodes.GroupBy(node => node.NodeCode, StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => $"Duplicate node codes are not allowed: {group.Key}."));
            messages.AddRange(edges.GroupBy(edge => edge.EdgeId, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => $"Duplicate edge IDs are not allowed: {group.Key}."));

            var nodeIds = nodes.ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            foreach (var edge in edges)
            {
                if (!nodeIds.ContainsKey(edge.FromNodeId)) messages.Add($"Edge '{edge.EdgeId}' FromNodeId '{edge.FromNodeId}' does not exist.");
                if (!nodeIds.ContainsKey(edge.ToNodeId)) messages.Add($"Edge '{edge.EdgeId}' ToNodeId '{edge.ToNodeId}' does not exist.");
                if (string.Equals(edge.FromNodeId, edge.ToNodeId, StringComparison.OrdinalIgnoreCase)) messages.Add($"Edge '{edge.EdgeId}' cannot point to itself.");
                if (edge.Length <= 0) messages.Add($"Edge '{edge.EdgeId}' length must be greater than 0.");
                if (edge.MaxSpeed <= 0) messages.Add($"Edge '{edge.EdgeId}' max speed must be greater than 0.");
            }

            foreach (var node in nodes.Where(node => !node.IsEnabled))
            {
                if (edges.Any(edge => edge.IsEnabled && (edge.FromNodeId == node.NodeId || edge.ToNodeId == node.NodeId)))
                {
                    messages.Add($"Disabled node '{node.NodeId}' cannot be used by enabled edges.");
                }
            }

            messages.AddRange(aliases.GroupBy(alias => new { VendorCode = alias.Brand ?? alias.AliasType, alias.AliasValue })
                .Where(group => group.Select(alias => alias.NodeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(group => $"Alias '{group.Key.VendorCode}/{group.Key.AliasValue}' maps to multiple nodes."));
            messages.AddRange(aliases.GroupBy(alias => new { alias.NodeId, VendorCode = alias.Brand ?? alias.AliasType })
                .Where(group => group.Select(alias => alias.AliasValue).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(group => $"Node '{group.Key.NodeId}' has multiple enabled aliases for vendor '{group.Key.VendorCode}'."));

            return messages;
        }

        private static MapNode CopyNode(MapNode source, string mapId, string mapVersion) => new()
        {
            NodeId = source.NodeId,
            MapId = mapId,
            MapVersion = mapVersion,
            NodeCode = source.NodeCode,
            Name = source.Name,
            NodeType = source.NodeType,
            Position = new MapPosition { MapId = mapId, X = source.Position.X, Y = source.Position.Y, Z = source.Position.Z },
            Heading = source.Heading,
            AreaCode = source.AreaCode,
            IsEnabled = source.IsEnabled,
            ParkingCapacity = source.ParkingCapacity,
            AllowedBrands = source.AllowedBrands,
            RequiredCapabilities = source.RequiredCapabilities,
            Tags = new Dictionary<string, string>(source.Tags, StringComparer.OrdinalIgnoreCase)
        };

        private static MapEdge CopyEdge(MapEdge source, string mapId, string mapVersion) => new()
        {
            EdgeId = source.EdgeId,
            MapId = mapId,
            MapVersion = mapVersion,
            FromNodeId = source.FromNodeId,
            ToNodeId = source.ToNodeId,
            Direction = source.Direction,
            Length = source.Length,
            MaxSpeed = source.MaxSpeed,
            TurnAngle = source.TurnAngle,
            Cost = source.Cost,
            IsEnabled = source.IsEnabled,
            AreaCode = source.AreaCode,
            AllowedBrands = source.AllowedBrands,
            MaxVehicleFlow = source.MaxVehicleFlow,
            Remark = source.Remark
        };

        private static MapLocationAlias CopyAlias(MapLocationAlias source, string mapId, string mapVersion) => new()
        {
            AliasId = Guid.NewGuid().ToString("N"),
            MapId = mapId,
            MapVersion = mapVersion,
            NodeId = source.NodeId,
            AliasType = source.AliasType,
            AliasValue = source.AliasValue,
            Brand = source.Brand,
            IsEnabled = source.IsEnabled,
            Remark = source.Remark
        };

        private static MapNode ToModelNode(MapNodeDto source, string mapId, string mapVersion) => new()
        {
            NodeId = source.NodeId,
            MapId = mapId,
            MapVersion = mapVersion,
            NodeCode = source.NodeCode,
            Name = source.NodeName,
            NodeType = ToLegacyNodeType(source.NodeType),
            Position = new MapPosition { MapId = mapId, X = source.X, Y = source.Y },
            Heading = source.Angle ?? 0,
            AreaCode = source.AreaId ?? string.Empty,
            IsEnabled = source.Enabled,
            Tags = new Dictionary<string, string>(source.Properties, StringComparer.OrdinalIgnoreCase)
        };

        private static MapEdge ToModelEdge(MapEdgeDto source, string mapId, string mapVersion) => new()
        {
            EdgeId = source.EdgeId,
            MapId = mapId,
            MapVersion = mapVersion,
            FromNodeId = source.FromNodeId,
            ToNodeId = source.ToNodeId,
            Direction = source.Direction == ContractEdgeDirection.Bidirectional ? LegacyEdgeDirection.Bidirectional : LegacyEdgeDirection.ForwardOnly,
            Length = source.Distance,
            MaxSpeed = source.SpeedLimit ?? 1,
            Cost = (int)Math.Max(1, Math.Round(source.Cost)),
            AreaCode = source.AreaId ?? string.Empty,
            IsEnabled = source.Enabled
        };

        private static MapLocationAlias ToAlias(VendorNodeMappingDto source, string mapId, string mapVersion) => new()
        {
            AliasId = Guid.NewGuid().ToString("N"),
            MapId = mapId,
            MapVersion = mapVersion,
            NodeId = source.SystemNodeId,
            AliasType = "Vendor",
            Brand = source.VendorCode,
            AliasValue = source.VendorNodeCode,
            IsEnabled = true
        };

        private static MapNodeDto ToDtoNode(MapNode source) => new()
        {
            NodeId = source.NodeId,
            NodeCode = source.NodeCode,
            NodeName = source.Name,
            NodeType = ToContractNodeType(source.NodeType),
            X = source.Position.X,
            Y = source.Position.Y,
            Angle = source.Heading,
            AreaId = source.AreaCode,
            Enabled = source.IsEnabled,
            Properties = source.Tags
        };

        private static MapEdgeDto ToDtoEdge(MapEdge source) => new()
        {
            EdgeId = source.EdgeId,
            FromNodeId = source.FromNodeId,
            ToNodeId = source.ToNodeId,
            Direction = source.Direction == LegacyEdgeDirection.Bidirectional ? ContractEdgeDirection.Bidirectional : ContractEdgeDirection.OneWay,
            Distance = source.Length,
            SpeedLimit = source.MaxSpeed,
            Cost = source.Cost,
            AreaId = source.AreaCode,
            Enabled = source.IsEnabled
        };

        private static LegacyMapNodeType ToLegacyNodeType(ContractNodeType nodeType) => nodeType switch
        {
            ContractNodeType.WorkStation => LegacyMapNodeType.Station,
            ContractNodeType.PickPoint => LegacyMapNodeType.Pickup,
            ContractNodeType.PutPoint => LegacyMapNodeType.Dropoff,
            ContractNodeType.ChargeStation => LegacyMapNodeType.Charge,
            ContractNodeType.WaitingPoint => LegacyMapNodeType.Waiting,
            ContractNodeType.Elevator => LegacyMapNodeType.Elevator,
            ContractNodeType.Door => LegacyMapNodeType.Door,
            _ => LegacyMapNodeType.Normal
        };

        private static ContractNodeType ToContractNodeType(LegacyMapNodeType nodeType) => nodeType switch
        {
            LegacyMapNodeType.Station => ContractNodeType.WorkStation,
            LegacyMapNodeType.Pickup => ContractNodeType.PickPoint,
            LegacyMapNodeType.Dropoff => ContractNodeType.PutPoint,
            LegacyMapNodeType.Charge => ContractNodeType.ChargeStation,
            LegacyMapNodeType.Waiting => ContractNodeType.WaitingPoint,
            LegacyMapNodeType.Elevator => ContractNodeType.Elevator,
            LegacyMapNodeType.Door => ContractNodeType.Door,
            _ => ContractNodeType.Normal
        };
    }
}
