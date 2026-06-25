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
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using Prism.Events;

namespace AgvDispatcher.Modules.MapModule.Services
{
    /// <summary>
    /// In-memory map management service for contract integration and UI development.
    /// </summary>
    public sealed class MockMapManagementService : IMapManagementService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly Dictionary<string, MapDraftDto> _drafts = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<MapVersionDto> _versions = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="MockMapManagementService"/> class.
        /// </summary>
        public MockMapManagementService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        /// <inheritdoc />
        public Task<AgvResult<MapDraftDto>> CreateDraftAsync(
            CreateMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.Now;
            var draft = new MapDraftDto
            {
                DraftId = Guid.NewGuid().ToString("N"),
                Map = new MapSnapshotDto
                {
                    MapId = Guid.NewGuid().ToString("N"),
                    MapName = string.IsNullOrWhiteSpace(request.MapName) ? "Untitled Map" : request.MapName,
                    Version = "draft",
                    UpdatedAt = now
                },
                CreatedAt = now,
                UpdatedAt = now,
                OperatorId = request.Context.OperatorId,
                State = MapState.Draft
            };

            _drafts[draft.DraftId] = draft;
            return Task.FromResult(AgvResult<MapDraftDto>.Ok(draft));
        }

        /// <inheritdoc />
        public Task<AgvResult<MapDraftDto>> GetDraftAsync(
            GetMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_drafts.TryGetValue(request.DraftId, out var draft)
                ? AgvResult<MapDraftDto>.Ok(draft)
                : AgvResult<MapDraftDto>.Fail(FailureCode.InvalidRequest, $"Draft '{request.DraftId}' was not found."));
        }

        /// <inheritdoc />
        public Task<AgvResult<MapDraftDto>> SaveDraftAsync(
            SaveMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.Now;
            var draft = new MapDraftDto
            {
                DraftId = request.DraftId,
                Map = request.Map,
                CreatedAt = _drafts.TryGetValue(request.DraftId, out var existing) ? existing.CreatedAt : now,
                UpdatedAt = now,
                OperatorId = request.Context.OperatorId,
                State = MapState.Draft
            };

            _drafts[request.DraftId] = draft;
            return Task.FromResult(AgvResult<MapDraftDto>.Ok(draft));
        }

        /// <inheritdoc />
        public Task<AgvResult<MapValidationResultDto>> ValidateDraftAsync(
            ValidateMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_drafts.TryGetValue(request.DraftId, out var draft))
            {
                return Task.FromResult(AgvResult<MapValidationResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    $"Draft '{request.DraftId}' was not found."));
            }

            var messages = new List<string>();
            if (string.IsNullOrWhiteSpace(draft.Map.MapId))
            {
                messages.Add("MapId is required.");
            }

            if (draft.Map.Nodes.GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                messages.Add("Duplicate node IDs are not allowed.");
            }

            if (draft.Map.Edges.GroupBy(edge => edge.EdgeId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                messages.Add("Duplicate edge IDs are not allowed.");
            }

            var result = new MapValidationResultDto
            {
                IsValid = messages.Count == 0,
                Messages = messages
            };

            return Task.FromResult(AgvResult<MapValidationResultDto>.Ok(result));
        }

        /// <inheritdoc />
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

            var draft = _drafts[request.DraftId];
            var now = DateTimeOffset.Now;
            var version = string.IsNullOrWhiteSpace(draft.Map.Version) || draft.Map.Version == "draft"
                ? now.ToString("yyyyMMddHHmmss")
                : draft.Map.Version;

            MarkCurrentVersion(draft.Map.MapId, version);

            _versions.Add(new MapVersionDto
            {
                MapId = draft.Map.MapId,
                MapName = draft.Map.MapName,
                Version = version,
                IsCurrent = true,
                PublishedAt = now,
                OperatorId = request.Context.OperatorId,
                State = MapState.Published
            });

            var result = new MapPublishResultDto
            {
                MapId = draft.Map.MapId,
                Version = version,
                PublishedAt = now,
                OperatorId = request.Context.OperatorId
            };

            _eventAggregator.GetEvent<PubSubEvent<MapPublishedEvent>>().Publish(new MapPublishedEvent
            {
                MapId = result.MapId,
                MapVersion = result.Version,
                PublishedAt = result.PublishedAt.DateTime,
                OperatorId = result.OperatorId
            });

            return AgvResult<MapPublishResultDto>.Ok(result);
        }

        /// <inheritdoc />
        public Task<AgvResult<MapActivationResultDto>> ActivateMapAsync(
            ActivateMapRequest request,
            CancellationToken cancellationToken = default)
        {
            var version = _versions.FirstOrDefault(item =>
                string.Equals(item.MapId, request.MapId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Version, request.Version, StringComparison.OrdinalIgnoreCase));
            if (version is null)
            {
                return Task.FromResult(AgvResult<MapActivationResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    $"Map version '{request.MapId}/{request.Version}' was not found."));
            }

            var old = _versions.FirstOrDefault(item => item.IsCurrent);
            MarkCurrentVersion(request.MapId, request.Version);
            var now = DateTimeOffset.Now;
            var result = new MapActivationResultDto
            {
                OldMapId = old?.MapId,
                OldVersion = old?.Version,
                MapId = request.MapId,
                Version = request.Version,
                ActivatedAt = now,
                OperatorId = request.Context.OperatorId
            };

            _eventAggregator.GetEvent<PubSubEvent<ActiveMapChangedEvent>>().Publish(new ActiveMapChangedEvent
            {
                OldMapId = result.OldMapId,
                OldMapVersion = result.OldVersion,
                NewMapId = result.MapId,
                NewMapVersion = result.Version,
                ChangedAt = now.DateTime,
                OperatorId = request.Context.OperatorId,
                Reason = request.Reason
            });

            return Task.FromResult(AgvResult<MapActivationResultDto>.Ok(result));
        }

        /// <inheritdoc />
        public Task<AgvResult<IReadOnlyList<MapVersionDto>>> GetMapVersionsAsync(
            GetMapVersionsRequest request,
            CancellationToken cancellationToken = default)
        {
            var versions = string.IsNullOrWhiteSpace(request.MapId)
                ? _versions
                : _versions.Where(item => string.Equals(item.MapId, request.MapId, StringComparison.OrdinalIgnoreCase)).ToList();

            return Task.FromResult(AgvResult<IReadOnlyList<MapVersionDto>>.Ok(versions));
        }

        /// <inheritdoc />
        public Task<AgvResult<MapRollbackResultDto>> RollbackToVersionAsync(
            RollbackMapVersionRequest request,
            CancellationToken cancellationToken = default)
        {
            var version = _versions.FirstOrDefault(item =>
                string.Equals(item.MapId, request.MapId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Version, request.Version, StringComparison.OrdinalIgnoreCase));

            if (version is null)
            {
                return Task.FromResult(AgvResult<MapRollbackResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    $"Map version '{request.MapId}/{request.Version}' was not found."));
            }

            var now = DateTimeOffset.Now;
            MarkCurrentVersion(request.MapId, request.Version);
            var result = new MapRollbackResultDto
            {
                MapId = request.MapId,
                Version = request.Version,
                RolledBackAt = now,
                OperatorId = request.Context.OperatorId
            };

            _eventAggregator.GetEvent<PubSubEvent<MapPublishedEvent>>().Publish(new MapPublishedEvent
            {
                MapId = result.MapId,
                MapVersion = result.Version,
                PublishedAt = result.RolledBackAt.DateTime,
                OperatorId = result.OperatorId
            });

            return Task.FromResult(AgvResult<MapRollbackResultDto>.Ok(result));
        }

        /// <inheritdoc />
        public Task<AgvResult<MapExportResultDto>> ExportMapAsync(
            ExportMapRequest request,
            CancellationToken cancellationToken = default)
        {
            var payload = JsonSerializer.Serialize(_drafts.Values.Select(item => item.Map));
            var result = new MapExportResultDto
            {
                Format = request.Format,
                Payload = payload,
                ExportedAt = DateTimeOffset.Now
            };

            return Task.FromResult(AgvResult<MapExportResultDto>.Ok(result));
        }

        /// <inheritdoc />
        public Task<AgvResult<MapImportResultDto>> ImportMapAsync(
            ImportMapRequest request,
            CancellationToken cancellationToken = default)
        {
            var map = JsonSerializer.Deserialize<MapSnapshotDto>(request.Payload);
            if (map is null)
            {
                return Task.FromResult(AgvResult<MapImportResultDto>.Fail(
                    FailureCode.InvalidRequest,
                    "Import payload is not a valid map snapshot."));
            }

            var now = DateTimeOffset.Now;
            var draft = new MapDraftDto
            {
                DraftId = Guid.NewGuid().ToString("N"),
                Map = map,
                CreatedAt = now,
                UpdatedAt = now,
                OperatorId = request.Context.OperatorId,
                State = MapState.Draft
            };

            _drafts[draft.DraftId] = draft;
            return Task.FromResult(AgvResult<MapImportResultDto>.Ok(new MapImportResultDto { Draft = draft }));
        }

        private void MarkCurrentVersion(string mapId, string version)
        {
            for (var i = 0; i < _versions.Count; i++)
            {
                var item = _versions[i];
                if (!string.Equals(item.MapId, mapId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _versions[i] = new MapVersionDto
                {
                    MapId = item.MapId,
                    MapName = item.MapName,
                    Version = item.Version,
                    IsCurrent = string.Equals(item.Version, version, StringComparison.OrdinalIgnoreCase),
                    State = string.Equals(item.Version, version, StringComparison.OrdinalIgnoreCase)
                        ? MapState.Active
                        : item.State == MapState.Active
                            ? MapState.Archived
                            : item.State,
                    PublishedAt = item.PublishedAt,
                    OperatorId = item.OperatorId
                };
            }
        }

        /// <inheritdoc />
        public Task<AgvResult> DeleteDraftAsync(
            DeleteMapDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            _drafts.Remove(request.DraftId);
            return Task.FromResult(AgvResult.Ok());
        }

        /// <inheritdoc />
        public Task<AgvResult> ArchiveMapAsync(
            ArchiveMapVersionRequest request,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < _versions.Count; i++)
            {
                var item = _versions[i];
                if (!string.Equals(item.MapId, request.MapId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(item.Version, request.Version, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (item.IsCurrent)
                {
                    return Task.FromResult(AgvResult.Fail(
                        FailureCode.InvalidState,
                        "Cannot archive the active map."));
                }

                _versions[i] = new MapVersionDto
                {
                    MapId = item.MapId,
                    MapName = item.MapName,
                    Version = item.Version,
                    IsCurrent = false,
                    State = MapState.Archived,
                    PublishedAt = item.PublishedAt,
                    OperatorId = item.OperatorId
                };
                return Task.FromResult(AgvResult.Ok());
            }

            return Task.FromResult(AgvResult.Fail(
                FailureCode.InvalidRequest,
                $"Map version '{request.MapId}/{request.Version}' was not found."));
        }
    }
}
