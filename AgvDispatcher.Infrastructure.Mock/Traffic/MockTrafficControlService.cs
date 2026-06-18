using System.Collections.Concurrent;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;

namespace AgvDispatcher.Infrastructure.Mock.Traffic
{
    /// <summary>
    /// Thread-safe, in-memory traffic control implementation for development and demos.
    /// </summary>
    public sealed class MockTrafficControlService : ITrafficControlService
    {
        private readonly object _syncRoot = new();
        private readonly ConcurrentDictionary<string, TrafficResourceStatusDto> _resources = new();
        private long _version;

        public Task<AgvResult<TrafficSnapshotDto>> GetTrafficSnapshotAsync(
            RequestContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                return Task.FromResult(AgvResult<TrafficSnapshotDto>.Ok(new TrafficSnapshotDto
                {
                    Version = _version,
                    GeneratedAt = DateTimeOffset.Now,
                    Resources = _resources.Values.Select(CopyStatus).ToArray()
                }));
            }
        }

        public Task<AgvResult<TrafficResourceStatusDto>> GetResourceStatusAsync(
            TrafficResourceKey resource,
            RequestContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                return Task.FromResult(AgvResult<TrafficResourceStatusDto>.Ok(GetStatus(resource)));
            }
        }

        public Task<AgvResult<IReadOnlyList<TrafficResourceStatusDto>>> GetResourceStatusesAsync(
            IReadOnlyList<TrafficResourceKey> resources,
            RequestContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                IReadOnlyList<TrafficResourceStatusDto> statuses = resources.Select(GetStatus).ToArray();
                return Task.FromResult(AgvResult<IReadOnlyList<TrafficResourceStatusDto>>.Ok(statuses));
            }
        }

        public Task<AgvResult<TrafficAvailabilityResultDto>> CheckAvailabilityAsync(
            TrafficAvailabilityRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                var statuses = request.Resources.Select(GetStatus).ToArray();
                var conflicts = statuses
                    .Where(status => status.State != TrafficResourceState.Free)
                    .Select(ToConflict)
                    .ToArray();

                return Task.FromResult(AgvResult<TrafficAvailabilityResultDto>.Ok(
                    new TrafficAvailabilityResultDto
                    {
                        IsAvailable = conflicts.Length == 0,
                        ResourceStatuses = statuses,
                        Conflicts = conflicts,
                        Message = conflicts.Length == 0
                            ? "All requested resources are available."
                            : "One or more requested resources are unavailable."
                    }));
            }
        }

        public Task<AgvResult<TrafficReservationDto>> TryAcquireAsync(
            TrafficAcquireRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.AgvId) || request.Resources.Count == 0 ||
                !Enum.IsDefined(request.LockMode))
            {
                return Task.FromResult(AgvResult<TrafficReservationDto>.Fail(
                    FailureCode.InvalidRequest,
                    "An AGV id and at least one resource are required."));
            }

            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                var resources = DistinctResources(request.Resources);
                var unavailable = resources.Select(GetStatus)
                    .FirstOrDefault(status => status.State != TrafficResourceState.Free);
                if (unavailable is not null)
                {
                    return Task.FromResult(AgvResult<TrafficReservationDto>.Fail(
                        FailureCode.InvalidState,
                        $"Resource {unavailable.Resource.ResourceType}:{unavailable.Resource.ResourceId} is {unavailable.State}.",
                        retryable: unavailable.State != TrafficResourceState.Disabled));
                }

                var reservationId = Guid.NewGuid().ToString("N");
                var now = DateTimeOffset.Now;
                DateTimeOffset? expiresAt = request.Ttl.HasValue ? now.Add(request.Ttl.Value) : null;
                foreach (var resource in resources)
                {
                    _resources[ToDictionaryKey(resource)] = new TrafficResourceStatusDto
                    {
                        Resource = CopyResource(resource),
                        State = ToState(request.LockMode),
                        OccupiedByAgvId = request.LockMode == TrafficLockMode.Occupy ? request.AgvId : null,
                        ReservedByAgvId = request.LockMode != TrafficLockMode.Occupy
                            ? request.AgvId
                            : null,
                        TaskId = request.TaskId,
                        ReservationId = reservationId,
                        Reason = request.Reason,
                        ExpireAt = expiresAt,
                        UpdatedAt = now
                    };
                }

                _version++;
                return Task.FromResult(AgvResult<TrafficReservationDto>.Ok(new TrafficReservationDto
                {
                    ReservationId = reservationId,
                    AgvId = request.AgvId,
                    TaskId = request.TaskId,
                    LockMode = request.LockMode,
                    Resources = resources,
                    CreatedAt = now,
                    ExpireAt = expiresAt,
                    Status = TrafficReservationStatus.Active
                }));
            }
        }

        public Task<AgvResult> ReleaseAsync(
            TrafficReleaseRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.ReservationId) &&
                string.IsNullOrWhiteSpace(request.AgvId) &&
                string.IsNullOrWhiteSpace(request.TaskId))
            {
                return Task.FromResult(AgvResult.Fail(
                    FailureCode.InvalidRequest,
                    "A reservation, AGV, or task owner is required."));
            }

            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                var candidates = request.Resources.Count == 0
                    ? _resources.Keys.ToArray()
                    : DistinctResources(request.Resources).Select(ToDictionaryKey).ToArray();
                var released = 0;

                foreach (var identity in candidates)
                {
                    if (!_resources.TryGetValue(identity, out var entry) || !OwnerMatches(entry, request))
                    {
                        continue;
                    }

                    _resources.TryRemove(identity, out _);
                    released++;
                }

                if (released > 0)
                {
                    _version++;
                }

                return Task.FromResult(AgvResult.Ok($"Released {released} traffic resource(s)."));
            }
        }

        public Task<AgvResult> UpdateAgvOccupancyAsync(
            AgvOccupancyUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.AgvId))
            {
                return Task.FromResult(AgvResult.Fail(FailureCode.InvalidRequest, "An AGV id is required."));
            }

            var reported = request.OccupiedNodeIds
                .Append(request.CurrentNodeId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => new TrafficResourceKey { ResourceType = TrafficResourceType.Node, ResourceId = id! })
                .Concat(request.OccupiedEdgeIds
                    .Append(request.CurrentEdgeId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => new TrafficResourceKey
                    {
                        ResourceType = TrafficResourceType.Edge,
                        ResourceId = id!
                    }))
                .GroupBy(ToDictionaryKey)
                .Select(group => group.First())
                .ToArray();

            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                var reportedIds = reported.Select(ToDictionaryKey).ToHashSet(StringComparer.Ordinal);
                var conflict = reported.Select(GetStatus).FirstOrDefault(status =>
                    status.State != TrafficResourceState.Free &&
                    (status.State is TrafficResourceState.Blocked or TrafficResourceState.Disabled ||
                     status.OccupiedByAgvId != request.AgvId &&
                     status.ReservedByAgvId != request.AgvId));
                if (conflict is not null)
                {
                    return Task.FromResult(AgvResult.Fail(
                        FailureCode.InvalidState,
                        $"Cannot occupy {conflict.Resource.ResourceType}:{conflict.Resource.ResourceId}; it is owned by another AGV."));
                }

                foreach (var identity in _resources
                    .Where(pair => pair.Value.State == TrafficResourceState.Occupied &&
                                   pair.Value.OccupiedByAgvId == request.AgvId &&
                                   !reportedIds.Contains(pair.Key))
                    .Select(pair => pair.Key)
                    .ToArray())
                {
                    _resources.TryRemove(identity, out _);
                }

                foreach (var resource in reported)
                {
                    _resources[ToDictionaryKey(resource)] = new TrafficResourceStatusDto
                    {
                        Resource = CopyResource(resource),
                        State = TrafficResourceState.Occupied,
                        OccupiedByAgvId = request.AgvId,
                        TaskId = request.TaskId,
                        Reason = "AGV occupancy report",
                        UpdatedAt = request.ReportTime
                    };
                }

                _version++;
                return Task.FromResult(AgvResult.Ok());
            }
        }

        public Task<AgvResult<TrafficBlockDto>> BlockResourcesAsync(
            TrafficBlockRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Resources.Count == 0)
            {
                return Task.FromResult(AgvResult<TrafficBlockDto>.Fail(
                    FailureCode.InvalidRequest,
                    "At least one resource is required."));
            }

            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                var resources = DistinctResources(request.Resources);
                var unavailable = resources.Select(GetStatus)
                    .FirstOrDefault(status => status.State != TrafficResourceState.Free);
                if (unavailable is not null)
                {
                    return Task.FromResult(AgvResult<TrafficBlockDto>.Fail(
                        FailureCode.InvalidState,
                        $"Resource {unavailable.Resource.ResourceType}:{unavailable.Resource.ResourceId} is already unavailable."));
                }

                var blockId = Guid.NewGuid().ToString("N");
                var now = DateTimeOffset.Now;
                DateTimeOffset? expiresAt = request.Ttl.HasValue ? now.Add(request.Ttl.Value) : null;
                foreach (var resource in resources)
                {
                    _resources[ToDictionaryKey(resource)] = new TrafficResourceStatusDto
                    {
                        Resource = CopyResource(resource),
                        State = TrafficResourceState.Blocked,
                        ReservationId = blockId,
                        Reason = request.Reason,
                        ExpireAt = expiresAt,
                        UpdatedAt = now
                    };
                }

                _version++;
                return Task.FromResult(AgvResult<TrafficBlockDto>.Ok(new TrafficBlockDto
                {
                    BlockId = blockId,
                    Resources = resources,
                    Reason = request.Reason,
                    OperatorId = request.OperatorId,
                    CreatedAt = now,
                    ExpireAt = expiresAt
                }));
            }
        }

        public Task<AgvResult> UnblockResourcesAsync(
            TrafficUnblockRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                RemoveExpiredEntries();
                var unblocked = 0;
                foreach (var identity in DistinctResources(request.Resources).Select(ToDictionaryKey))
                {
                    if (_resources.TryGetValue(identity, out var entry) && entry.State == TrafficResourceState.Blocked)
                    {
                        _resources.TryRemove(identity, out _);
                        unblocked++;
                    }
                }

                if (unblocked > 0)
                {
                    _version++;
                }

                return Task.FromResult(AgvResult.Ok($"Unblocked {unblocked} traffic resource(s)."));
            }
        }

        private TrafficResourceStatusDto GetStatus(TrafficResourceKey resource)
        {
            return _resources.TryGetValue(ToDictionaryKey(resource), out var entry)
                ? CopyStatus(entry)
                : new TrafficResourceStatusDto
                {
                    Resource = CopyResource(resource),
                    State = TrafficResourceState.Free,
                    UpdatedAt = DateTimeOffset.Now
                };
        }

        private static TrafficResourceStatusDto CopyStatus(TrafficResourceStatusDto status)
        {
            return new TrafficResourceStatusDto
            {
                Resource = CopyResource(status.Resource),
                State = status.State,
                OccupiedByAgvId = status.OccupiedByAgvId,
                ReservedByAgvId = status.ReservedByAgvId,
                TaskId = status.TaskId,
                ReservationId = status.ReservationId,
                Reason = status.Reason,
                ExpireAt = status.ExpireAt,
                UpdatedAt = status.UpdatedAt
            };
        }

        private static TrafficConflictDto ToConflict(TrafficResourceStatusDto status)
        {
            return new TrafficConflictDto
            {
                Resource = status.Resource,
                CurrentState = status.State,
                ConflictAgvId = status.OccupiedByAgvId ?? status.ReservedByAgvId,
                ConflictTaskId = status.TaskId,
                ReservationId = status.ReservationId,
                FailureCode = status.State switch
                {
                    TrafficResourceState.Occupied => TrafficFailureCode.ResourceAlreadyOccupied,
                    TrafficResourceState.Reserved or TrafficResourceState.Locked => TrafficFailureCode.ResourceAlreadyReserved,
                    TrafficResourceState.Blocked => TrafficFailureCode.ResourceBlocked,
                    TrafficResourceState.Disabled => TrafficFailureCode.ResourceDisabled,
                    _ => TrafficFailureCode.InternalError
                },
                Message = $"Resource is {status.State}."
            };
        }

        private static bool OwnerMatches(TrafficResourceStatusDto status, TrafficReleaseRequest request)
        {
            return (!string.IsNullOrWhiteSpace(request.ReservationId) && request.ReservationId == status.ReservationId) ||
                   (!string.IsNullOrWhiteSpace(request.AgvId) &&
                    request.AgvId == (status.OccupiedByAgvId ?? status.ReservedByAgvId)) ||
                   (!string.IsNullOrWhiteSpace(request.TaskId) && request.TaskId == status.TaskId);
        }

        private void RemoveExpiredEntries()
        {
            var now = DateTimeOffset.Now;
            var expired = _resources
                .Where(pair => pair.Value.ExpireAt <= now)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var identity in expired)
            {
                _resources.TryRemove(identity, out _);
            }

            if (expired.Length > 0)
            {
                _version++;
            }
        }

        private static IReadOnlyList<TrafficResourceKey> DistinctResources(IEnumerable<TrafficResourceKey> resources)
        {
            return resources
                .GroupBy(ToDictionaryKey)
                .Select(group => CopyResource(group.First()))
                .ToArray();
        }

        private static TrafficResourceState ToState(TrafficLockMode mode) => mode switch
        {
            TrafficLockMode.Reserve => TrafficResourceState.Reserved,
            TrafficLockMode.Occupy => TrafficResourceState.Occupied,
            TrafficLockMode.Lock => TrafficResourceState.Locked,
            TrafficLockMode.Block => TrafficResourceState.Blocked,
            _ => TrafficResourceState.Unknown
        };

        private static string ToDictionaryKey(TrafficResourceKey resource) =>
            $"{(int)resource.ResourceType}:{resource.ResourceId}";

        private static TrafficResourceKey CopyResource(TrafficResourceKey resource) => new()
        {
            ResourceType = resource.ResourceType,
            ResourceId = resource.ResourceId
        };

        // TODO: Publish TrafficResourceChangedEvent when Core exposes a transport-agnostic event publisher.
    }
}
