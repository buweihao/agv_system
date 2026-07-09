using System.Collections.Concurrent;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Reservations.Enums;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;

namespace AgvDispatcher.Infrastructure.Mock.Reservations
{
    /// <summary>
    /// In-memory route reservation coordinator backed by ITrafficControlService.
    /// </summary>
    public sealed class MockRouteReservationService : IRouteReservationService
    {
        private readonly ITrafficControlService _trafficControlService;
        private readonly ConcurrentDictionary<string, RouteReservationDto> _reservations =
            new(StringComparer.Ordinal);
        private readonly SemaphoreSlim _gate = new(1, 1);

        public MockRouteReservationService(ITrafficControlService trafficControlService)
        {
            _trafficControlService = trafficControlService ??
                throw new ArgumentNullException(nameof(trafficControlService));
        }

        public async Task<AgvResult<RouteReservationDto>> CreateReservationAsync(
            CreateRouteReservationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return AgvResult<RouteReservationDto>.Fail(
                    FailureCode.InvalidRequest,
                    "A route reservation request is required.");
            }

            if (string.IsNullOrWhiteSpace(request.TaskId) ||
                string.IsNullOrWhiteSpace(request.VehicleId) ||
                string.IsNullOrWhiteSpace(request.PlanId) ||
                string.IsNullOrWhiteSpace(request.MapId) ||
                string.IsNullOrWhiteSpace(request.MapVersion) ||
                request.Segments is null ||
                request.Segments.Count == 0 ||
                request.RollingWindowSize <= 0)
            {
                return AgvResult<RouteReservationDto>.Fail(
                    FailureCode.InvalidRequest,
                    "TaskId, VehicleId, PlanId, MapId, MapVersion, route segments, and a positive rolling window size are required.");
            }

            if (request.Segments.Any(segment =>
                    segment is null ||
                    string.IsNullOrWhiteSpace(segment.FromNodeId) ||
                    string.IsNullOrWhiteSpace(segment.ToNodeId) ||
                    string.IsNullOrWhiteSpace(segment.EdgeId)))
            {
                return AgvResult<RouteReservationDto>.Fail(
                    FailureCode.InvalidRequest,
                    "Every route segment must define FromNodeId, ToNodeId, and EdgeId.");
            }

            if (request.Segments.Select(segment => segment.Sequence).Distinct().Count() != request.Segments.Count)
            {
                return AgvResult<RouteReservationDto>.Fail(
                    FailureCode.InvalidRequest,
                    "Route segment sequences must be unique.");
            }

            var now = DateTimeOffset.Now;
            var reservation = new RouteReservationDto
            {
                ReservationId = Guid.NewGuid().ToString("N"),
                TaskId = request.TaskId,
                VehicleId = request.VehicleId,
                PlanId = request.PlanId,
                MapId = request.MapId,
                MapVersion = request.MapVersion,
                RollingWindowSize = request.RollingWindowSize,
                ReservationPolicy = request.ReservationPolicy,
                State = RouteReservationState.Created,
                Segments = request.Segments
                    .OrderBy(segment => segment.Sequence)
                    .Select(segment => new RouteReservedSegmentDto
                    {
                        Segment = segment,
                        Resources = ToTrafficResources(segment)
                    })
                    .ToArray(),
                CreatedAt = now,
                UpdatedAt = now
            };

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!_reservations.TryAdd(reservation.ReservationId, reservation))
                {
                    return AgvResult<RouteReservationDto>.Fail(
                        FailureCode.UnknownError,
                        "Could not allocate a unique route reservation id.");
                }

                // Rolling-window reservations are intentionally created without taking traffic
                // resources. LockFullPath is the only policy that acquires during creation.
                if (request.ReservationPolicy == RouteReservationPolicy.LockFullPath)
                {
                    var acquireResult = await AcquireSegmentsAsync(
                        reservation,
                        reservation.Segments,
                        request.Context,
                        "Full-path route lock",
                        cancellationToken);
                    reservation = acquireResult.Reservation;
                    if (acquireResult.Acquired)
                    {
                        reservation = CopyReservation(
                            reservation,
                            currentWindow: BuildWindow(reservation.Segments, reservation.Segments));
                    }

                    _reservations[reservation.ReservationId] = reservation;
                }

                return AgvResult<RouteReservationDto>.Ok(reservation);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<AgvResult<RouteRollingLockResultDto>> AcquireNextWindowAsync(
            AcquireNextRouteWindowRequest request,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!_reservations.TryGetValue(request.ReservationId, out var reservation))
                {
                    return AgvResult<RouteRollingLockResultDto>.Fail(
                        FailureCode.InvalidRequest,
                        "Route reservation was not found.");
                }

                if (reservation.State is RouteReservationState.Released or
                    RouteReservationState.Canceled or
                    RouteReservationState.Completed or
                    RouteReservationState.Failed)
                {
                    return AgvResult<RouteRollingLockResultDto>.Fail(
                        FailureCode.InvalidState,
                        $"Reservation cannot acquire resources while in state {reservation.State}.");
                }

                var windowSize = reservation.ReservationPolicy == RouteReservationPolicy.LockFullPath
                    ? reservation.Segments.Count
                    : request.RollingWindowSize > 0
                    ? request.RollingWindowSize
                    : reservation.RollingWindowSize;
                var windowSegments = reservation.Segments
                    .Where(segment => !segment.IsReleased &&
                                      segment.Segment.Sequence > request.CurrentSegmentSequence)
                    .OrderBy(segment => segment.Segment.Sequence)
                    .Take(windowSize)
                    .ToArray();

                if (windowSegments.Length == 0)
                {
                    var terminalState = reservation.Segments.All(segment => segment.IsReleased)
                        ? RouteReservationState.Completed
                        : RouteReservationState.FullyLocked;
                    reservation = CopyReservation(reservation, state: terminalState);
                    _reservations[reservation.ReservationId] = reservation;
                    return AgvResult<RouteRollingLockResultDto>.Ok(new RouteRollingLockResultDto
                    {
                        ReservationId = reservation.ReservationId,
                        State = terminalState,
                        Acquired = true,
                        AcquiredWindow = BuildWindow(windowSegments, reservation.Segments),
                        Message = "No additional route resources need to be acquired."
                    });
                }

                var segmentsToAcquire = windowSegments.Where(segment => !segment.IsLocked).ToArray();
                if (segmentsToAcquire.Length > 0)
                {
                    var acquire = await AcquireSegmentsAsync(
                        reservation,
                        segmentsToAcquire,
                        request.Context,
                        $"Rolling route window for {reservation.ReservationId}",
                        cancellationToken);
                    if (!acquire.Acquired)
                    {
                        reservation = acquire.Reservation;
                        _reservations[reservation.ReservationId] = reservation;

                        return AgvResult<RouteRollingLockResultDto>.Ok(new RouteRollingLockResultDto
                        {
                            ReservationId = reservation.ReservationId,
                            State = RouteReservationState.Waiting,
                            Acquired = false,
                            ShouldWait = !acquire.RequiresReplan,
                            RequiresReplan = acquire.RequiresReplan,
                            FailureReason = acquire.FailureReason,
                            Message = acquire.Message
                        });
                    }

                    reservation = acquire.Reservation;
                    var updatedSegments = reservation.Segments;
                    var updatedWindowSegments = updatedSegments
                        .Where(segment => windowSegments.Any(window =>
                            window.Segment.Sequence == segment.Segment.Sequence))
                        .ToArray();
                    var currentWindow = BuildWindow(updatedWindowSegments, updatedSegments);
                    reservation = CopyReservation(
                        reservation,
                        currentWindow: currentWindow);
                    _reservations[reservation.ReservationId] = reservation;
                }
                else
                {
                    var state = reservation.Segments.Where(segment => !segment.IsReleased).All(segment => segment.IsLocked)
                        ? RouteReservationState.FullyLocked
                        : RouteReservationState.PartiallyLocked;
                    var currentWindow = BuildWindow(windowSegments, reservation.Segments);
                    reservation = CopyReservation(
                        reservation,
                        state: state,
                        currentWindow: currentWindow);
                    _reservations[reservation.ReservationId] = reservation;
                }

                return AgvResult<RouteRollingLockResultDto>.Ok(new RouteRollingLockResultDto
                {
                    ReservationId = reservation.ReservationId,
                    State = reservation.State,
                    Acquired = true,
                    AcquiredWindow = reservation.CurrentWindow ?? BuildWindow(windowSegments, reservation.Segments),
                    Message = "The rolling route window is locked."
                });
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<AgvResult<RouteRollingReleaseResultDto>> ReleasePassedResourcesAsync(
            ReleasePassedRouteResourcesRequest request,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!_reservations.TryGetValue(request.ReservationId, out var reservation))
                {
                    return AgvResult<RouteRollingReleaseResultDto>.Fail(
                        FailureCode.InvalidRequest,
                        "Route reservation was not found.");
                }

                // PassedSegmentSequence is the last segment sequence the AGV has completely traversed.
                var passedSegments = reservation.Segments
                    .Where(segment => !segment.IsReleased &&
                                      segment.Segment.Sequence <= request.PassedSegmentSequence)
                    .ToArray();
                var keepCurrentNodeOccupied = false;
                if (!string.IsNullOrWhiteSpace(request.CurrentNodeId))
                {
                    var currentNodeStatus = await _trafficControlService.GetResourceStatusAsync(
                        new TrafficResourceKey
                        {
                            ResourceType = TrafficResourceType.Node,
                            ResourceId = request.CurrentNodeId
                        },
                        request.Context,
                        cancellationToken);
                    keepCurrentNodeOccupied = currentNodeStatus.Success &&
                        currentNodeStatus.Data?.State == TrafficResourceState.Occupied &&
                        string.Equals(
                            currentNodeStatus.Data.OccupiedByAgvId,
                            reservation.VehicleId,
                            StringComparison.OrdinalIgnoreCase);
                }

                var releasedResources = new List<TrafficResourceKey>();
                foreach (var segment in passedSegments.Where(segment => segment.IsLocked))
                {
                    // The passed segment's ToNode is also the AGV's current physical occupancy.
                    // End the segment reservation, but leave that node under occupancy control;
                    // the next position report will release it after the AGV moves away.
                    var resourcesToRelease = segment.Resources
                        .Where(resource => !keepCurrentNodeOccupied ||
                            !IsCurrentNode(resource, request.CurrentNodeId))
                        .ToArray();
                    if (resourcesToRelease.Length == 0)
                    {
                        continue;
                    }

                    foreach (var trafficReservationId in segment.TrafficReservationIds)
                    {
                        var releaseResult = await _trafficControlService.ReleaseAsync(
                            new TrafficReleaseRequest
                            {
                                Context = request.Context,
                                ReservationId = trafficReservationId,
                                AgvId = reservation.VehicleId,
                                TaskId = reservation.TaskId,
                                Resources = resourcesToRelease,
                                Reason = $"Passed node {request.CurrentNodeId}"
                            },
                            cancellationToken);
                        if (!releaseResult.Success)
                        {
                            return AgvResult<RouteRollingReleaseResultDto>.Fail(
                                releaseResult.Code,
                                releaseResult.Message,
                                releaseResult.Retryable);
                        }
                    }

                    releasedResources.AddRange(resourcesToRelease);
                }

                var releasedAt = DateTimeOffset.Now;
                var passedSequences = passedSegments.Select(segment => segment.Segment.Sequence).ToHashSet();
                var updatedSegments = reservation.Segments.Select(segment =>
                    passedSequences.Contains(segment.Segment.Sequence)
                        ? CopySegment(segment, isLocked: false, isReleased: true, releasedAt: releasedAt)
                        : segment).ToArray();
                var state = updatedSegments.All(segment => segment.IsReleased)
                    ? RouteReservationState.Completed
                    : updatedSegments.Any(segment => segment.IsLocked)
                        ? RouteReservationState.PartiallyLocked
                        : RouteReservationState.Created;
                var currentWindow = BuildCurrentLockedWindow(updatedSegments);
                reservation = CopyReservation(
                    reservation,
                    state: state,
                    segments: updatedSegments,
                    currentWindow: currentWindow,
                    setCurrentWindow: true);
                _reservations[reservation.ReservationId] = reservation;

                return AgvResult<RouteRollingReleaseResultDto>.Ok(new RouteRollingReleaseResultDto
                {
                    ReservationId = reservation.ReservationId,
                    State = state,
                    ReleasedSegmentSequences = passedSegments.Select(segment => segment.Segment.Sequence).ToArray(),
                    ReleasedResources = DistinctResources(releasedResources),
                    CurrentWindow = currentWindow
                });
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<AgvResult> ReleaseReservationAsync(
            ReleaseRouteReservationRequest request,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!_reservations.TryGetValue(request.ReservationId, out var reservation))
                {
                    return AgvResult.Fail(FailureCode.InvalidRequest, "Route reservation was not found.");
                }

                var lockedSegments = reservation.Segments.Where(segment => segment.IsLocked).ToArray();
                foreach (var group in lockedSegments
                    .SelectMany(segment => segment.TrafficReservationIds.Select(id => (Id: id, Segment: segment)))
                    .GroupBy(item => item.Id, StringComparer.Ordinal))
                {
                    var releaseResult = await _trafficControlService.ReleaseAsync(
                        new TrafficReleaseRequest
                        {
                            Context = request.Context,
                            ReservationId = group.Key,
                            Resources = DistinctResources(group.SelectMany(item => item.Segment.Resources)),
                            Reason = request.Reason
                        },
                        cancellationToken);
                    if (!releaseResult.Success)
                    {
                        return releaseResult;
                    }
                }

                var releasedAt = DateTimeOffset.Now;
                var releasedSegments = reservation.Segments.Select(segment =>
                    CopySegment(segment, isLocked: false, isReleased: true, releasedAt: releasedAt)).ToArray();
                var finalState = request.Reason.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                    ? RouteReservationState.Canceled
                    : RouteReservationState.Released;
                reservation = CopyReservation(
                    reservation,
                    state: finalState,
                    segments: releasedSegments,
                    currentWindow: null,
                    releasedAt: releasedAt,
                    setReleasedAt: true,
                    setCurrentWindow: true);
                _reservations[reservation.ReservationId] = reservation;
                return AgvResult.Ok();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<AgvResult<RouteReservationDto>> GetReservationAsync(
            GetRouteReservationRequest request,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                return _reservations.TryGetValue(request.ReservationId, out var reservation)
                    ? AgvResult<RouteReservationDto>.Ok(reservation)
                    : AgvResult<RouteReservationDto>.Fail(FailureCode.InvalidRequest, "Route reservation was not found.");
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetReservationsByTaskAsync(
            GetTaskRouteReservationsRequest request,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                IReadOnlyList<RouteReservationDto> matches = _reservations.Values
                    .Where(reservation => reservation.TaskId == request.TaskId)
                    .OrderBy(reservation => reservation.CreatedAt)
                    .ToArray();
                return AgvResult<IReadOnlyList<RouteReservationDto>>.Ok(matches);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetReservationsByVehicleAsync(
            GetVehicleRouteReservationsRequest request,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                IReadOnlyList<RouteReservationDto> matches = _reservations.Values
                    .Where(reservation => reservation.VehicleId == request.VehicleId)
                    .OrderBy(reservation => reservation.CreatedAt)
                    .ToArray();
                return AgvResult<IReadOnlyList<RouteReservationDto>>.Ok(matches);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetActiveReservationsAsync(
            GetRouteReservationsRequest request,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                IReadOnlyList<RouteReservationDto> matches = _reservations.Values
                    .Where(reservation =>
                        reservation.State is not RouteReservationState.Released and
                            not RouteReservationState.Canceled and
                            not RouteReservationState.Completed and
                            not RouteReservationState.Failed &&
                        (string.IsNullOrWhiteSpace(request.TaskId) ||
                         string.Equals(reservation.TaskId, request.TaskId, StringComparison.OrdinalIgnoreCase)) &&
                        (string.IsNullOrWhiteSpace(request.VehicleId) ||
                         string.Equals(reservation.VehicleId, request.VehicleId, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(reservation => reservation.UpdatedAt)
                    .ToArray();

                return AgvResult<IReadOnlyList<RouteReservationDto>>.Ok(matches);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<RouteReservationDto>> GetAllReservationsAsync(
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                return _reservations.Values
                    .OrderByDescending(reservation => reservation.UpdatedAt)
                    .ToArray();
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<(RouteReservationFailureReason Reason, bool RequiresReplan)> DescribeAcquireFailureAsync(
            RouteReservationDto reservation,
            IEnumerable<RouteReservedSegmentDto> segments,
            RequestContext context,
            CancellationToken cancellationToken)
        {
            var availability = await _trafficControlService.CheckAvailabilityAsync(
                new TrafficAvailabilityRequest
                {
                    Context = context,
                    AgvId = reservation.VehicleId,
                    TaskId = reservation.TaskId,
                    Resources = DistinctResources(segments.SelectMany(segment => segment.Resources)),
                    ExpectedMapVersion = reservation.MapVersion
                },
                cancellationToken);
            var state = availability.Data?.Conflicts.FirstOrDefault()?.CurrentState;
            return state switch
            {
                TrafficResourceState.Occupied => (RouteReservationFailureReason.ResourceOccupied, false),
                TrafficResourceState.Reserved or TrafficResourceState.Locked =>
                    (RouteReservationFailureReason.ResourceReserved, false),
                TrafficResourceState.Blocked or TrafficResourceState.Disabled =>
                    (RouteReservationFailureReason.ResourceBlocked, true),
                _ => (RouteReservationFailureReason.TrafficAcquireFailed, false)
            };
        }

        private async Task<AcquireSegmentsResult> AcquireSegmentsAsync(
            RouteReservationDto reservation,
            IReadOnlyList<RouteReservedSegmentDto> segments,
            RequestContext context,
            string reason,
            CancellationToken cancellationToken)
        {
            var acquireResult = await _trafficControlService.TryAcquireAsync(
                new TrafficAcquireRequest
                {
                    Context = context,
                    AgvId = reservation.VehicleId,
                    TaskId = reservation.TaskId,
                    LockMode = TrafficLockMode.Lock,
                    Resources = DistinctResources(segments.SelectMany(segment => segment.Resources)),
                    Reason = reason,
                    ExpectedMapVersion = reservation.MapVersion
                },
                cancellationToken);

            if (!acquireResult.Success || acquireResult.Data is null)
            {
                var failure = await DescribeAcquireFailureAsync(
                    reservation,
                    segments,
                    context,
                    cancellationToken);
                return new AcquireSegmentsResult(
                    CopyReservation(reservation, state: RouteReservationState.Waiting),
                    false,
                    failure.RequiresReplan,
                    failure.Reason,
                    acquireResult.Message);
            }

            var acquiredSequences = segments.Select(segment => segment.Segment.Sequence).ToHashSet();
            var lockedAt = DateTimeOffset.Now;
            var updatedSegments = reservation.Segments.Select(segment =>
                acquiredSequences.Contains(segment.Segment.Sequence)
                    ? CopySegment(
                        segment,
                        isReserved: true,
                        isLocked: true,
                        trafficReservationIds: segment.TrafficReservationIds
                            .Append(acquireResult.Data.ReservationId)
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                        lockedAt: lockedAt)
                    : segment).ToArray();
            var state = updatedSegments.Where(segment => !segment.IsReleased).All(segment => segment.IsLocked)
                ? RouteReservationState.FullyLocked
                : RouteReservationState.PartiallyLocked;
            return new AcquireSegmentsResult(
                CopyReservation(reservation, state: state, segments: updatedSegments),
                true,
                false,
                RouteReservationFailureReason.None,
                acquireResult.Message);
        }

        private static IReadOnlyList<TrafficResourceKey> ToTrafficResources(
            AgvDispatcher.Core.Contracts.Planning.Results.PathSegmentDto segment)
        {
            return new[]
            {
                new TrafficResourceKey
                {
                    ResourceType = TrafficResourceType.Edge,
                    ResourceId = segment.EdgeId
                },
                new TrafficResourceKey
                {
                    ResourceType = TrafficResourceType.Node,
                    ResourceId = segment.ToNodeId
                }
            };
        }

        private static bool IsCurrentNode(TrafficResourceKey resource, string? currentNodeId)
        {
            return resource.ResourceType == TrafficResourceType.Node &&
                   !string.IsNullOrWhiteSpace(currentNodeId) &&
                   string.Equals(resource.ResourceId, currentNodeId, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<TrafficResourceKey> DistinctResources(
            IEnumerable<TrafficResourceKey> resources)
        {
            return resources
                .GroupBy(resource => (resource.ResourceType, resource.ResourceId))
                .Select(group => group.First())
                .ToArray();
        }

        private static RouteRollingWindowDto? BuildCurrentLockedWindow(
            IReadOnlyList<RouteReservedSegmentDto> segments)
        {
            var locked = segments.Where(segment => segment.IsLocked && !segment.IsReleased).ToArray();
            return locked.Length == 0 ? null : BuildWindow(locked, segments);
        }

        private static RouteRollingWindowDto BuildWindow(
            IReadOnlyList<RouteReservedSegmentDto> windowSegments,
            IReadOnlyList<RouteReservedSegmentDto> allSegments)
        {
            return new RouteRollingWindowDto
            {
                StartSegmentSequence = windowSegments.FirstOrDefault()?.Segment.Sequence ?? 0,
                EndSegmentSequence = windowSegments.LastOrDefault()?.Segment.Sequence ?? 0,
                Segments = windowSegments,
                IsCompletePathLocked = allSegments.Where(segment => !segment.IsReleased).All(segment => segment.IsLocked)
            };
        }

        private static RouteReservedSegmentDto CopySegment(
            RouteReservedSegmentDto source,
            bool? isReserved = null,
            bool? isLocked = null,
            bool? isReleased = null,
            IReadOnlyList<string>? trafficReservationIds = null,
            DateTimeOffset? lockedAt = null,
            DateTimeOffset? releasedAt = null)
        {
            return new RouteReservedSegmentDto
            {
                Segment = source.Segment,
                Resources = source.Resources,
                TrafficReservationIds = trafficReservationIds ?? source.TrafficReservationIds,
                IsReserved = isReserved ?? source.IsReserved,
                IsLocked = isLocked ?? source.IsLocked,
                IsReleased = isReleased ?? source.IsReleased,
                LockedAt = lockedAt ?? source.LockedAt,
                ReleasedAt = releasedAt ?? source.ReleasedAt
            };
        }

        private static RouteReservationDto CopyReservation(
            RouteReservationDto source,
            RouteReservationState? state = null,
            IReadOnlyList<RouteReservedSegmentDto>? segments = null,
            RouteRollingWindowDto? currentWindow = null,
            DateTimeOffset? releasedAt = null,
            bool setReleasedAt = false,
            bool setCurrentWindow = false)
        {
            return new RouteReservationDto
            {
                ReservationId = source.ReservationId,
                TaskId = source.TaskId,
                VehicleId = source.VehicleId,
                PlanId = source.PlanId,
                MapId = source.MapId,
                MapVersion = source.MapVersion,
                RollingWindowSize = source.RollingWindowSize,
                ReservationPolicy = source.ReservationPolicy,
                State = state ?? source.State,
                Segments = segments ?? source.Segments,
                CurrentWindow = setCurrentWindow ? currentWindow : currentWindow ?? source.CurrentWindow,
                CreatedAt = source.CreatedAt,
                UpdatedAt = DateTimeOffset.Now,
                ReleasedAt = setReleasedAt ? releasedAt : source.ReleasedAt
            };
        }

        private sealed record AcquireSegmentsResult(
            RouteReservationDto Reservation,
            bool Acquired,
            bool RequiresReplan,
            RouteReservationFailureReason FailureReason,
            string Message);
    }
}
