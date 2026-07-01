using System.Text.Json;
using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Reservations.Enums;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Models;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Services;

/// <summary>SQLite-backed route reservation and rolling-window service.</summary>
public sealed class PersistentRouteReservationService : IRouteReservationService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly DbContextOptions<AgvDispatcherDbContext> _options;
    private readonly ITrafficControlService _traffic;
    public PersistentRouteReservationService(DbContextOptions<AgvDispatcherDbContext> options, ITrafficControlService traffic) { _options = options; _traffic = traffic; }

    public async Task<AgvResult<RouteReservationDto>> CreateReservationAsync(CreateRouteReservationRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.TaskId) || string.IsNullOrWhiteSpace(r.VehicleId) || r.Segments.Count == 0 || r.RollingWindowSize <= 0) return AgvResult<RouteReservationDto>.Fail(FailureCode.InvalidRequest, "Task, vehicle, segments and window are required.");
        await Gate.WaitAsync(ct); try { await using var db = CreateContext(); var now = DateTimeOffset.Now; var entity = new RouteReservationEntity { ReservationId = Guid.NewGuid().ToString("N"), TaskId = r.TaskId, VehicleId = r.VehicleId, PlanId = r.PlanId, MapId = r.MapId, MapVersion = r.MapVersion, RollingWindowSize = r.RollingWindowSize, ReservationPolicy = (int)r.ReservationPolicy, State = (int)RouteReservationState.Created, CreatedAt = now, UpdatedAt = now, Segments = r.Segments.OrderBy(x => x.Sequence).Select(x => new RouteReservationSegmentEntity { Sequence = x.Sequence, FromNodeId = x.FromNodeId, ToNodeId = x.ToNodeId, EdgeId = x.EdgeId, Distance = x.Distance, Cost = x.EstimatedSeconds, ResourcesJson = JsonSerializer.Serialize(Resources(x)) }).ToList() }; db.RouteReservations.Add(entity); await db.SaveChangesAsync(ct); var dto = Map(entity); if (r.ReservationPolicy == RouteReservationPolicy.LockFullPath) { var locked = await AcquireCoreAsync(db, entity, 0, int.MaxValue, r.Context, ct); if (!locked.Success || locked.Data?.Acquired != true) return AgvResult<RouteReservationDto>.Fail(FailureCode.InvalidState, locked.Message); dto = Map(entity); } return AgvResult<RouteReservationDto>.Ok(dto); } finally { Gate.Release(); }
    }
    public async Task<AgvResult<RouteRollingLockResultDto>> AcquireNextWindowAsync(AcquireNextRouteWindowRequest r, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct); try { await using var db = CreateContext(); var entity = await LoadAsync(db, r.ReservationId, ct); if (entity is null) return AgvResult<RouteRollingLockResultDto>.Fail(FailureCode.InvalidRequest, "Route reservation was not found."); return await AcquireCoreAsync(db, entity, r.CurrentSegmentSequence, r.RollingWindowSize > 0 ? r.RollingWindowSize : entity.RollingWindowSize, r.Context, ct); } finally { Gate.Release(); }
    }
    private async Task<AgvResult<RouteRollingLockResultDto>> AcquireCoreAsync(AgvDispatcherDbContext db, RouteReservationEntity e, int current, int size, RequestContext context, CancellationToken ct)
    {
        var window = e.Segments.Where(x => !x.IsReleased && x.Sequence > current).OrderBy(x => x.Sequence).Take(size).ToArray();
        if (window.Length == 0) return AgvResult<RouteRollingLockResultDto>.Ok(new RouteRollingLockResultDto { ReservationId = e.ReservationId, State = (RouteReservationState)e.State, Acquired = true, AcquiredWindow = Window(window, e.Segments), Message = "No additional route resources need to be acquired." });
        var acquireSegments = window.Where(x => !x.IsLocked).ToArray();
        if (acquireSegments.Length > 0) { var resources = Distinct(acquireSegments.SelectMany(Resources)); var acquired = await _traffic.TryAcquireAsync(new TrafficAcquireRequest { Context = context, AgvId = e.VehicleId, TaskId = e.TaskId, LockMode = TrafficLockMode.Lock, Resources = resources, Reason = $"Rolling route window for {e.ReservationId}" }, ct); if (!acquired.Success) { var availability = await _traffic.CheckAvailabilityAsync(new TrafficAvailabilityRequest { Context = context, AgvId = e.VehicleId, TaskId = e.TaskId, Resources = resources, ExpectedMapVersion = e.MapVersion }, ct); var state = availability.Data?.Conflicts.FirstOrDefault()?.CurrentState; var replan = state is TrafficResourceState.Blocked or TrafficResourceState.Disabled; e.State = (int)RouteReservationState.Waiting; e.LastFailureReason = acquired.Message; e.UpdatedAt = DateTimeOffset.Now; await db.SaveChangesAsync(ct); return AgvResult<RouteRollingLockResultDto>.Ok(new RouteRollingLockResultDto { ReservationId = e.ReservationId, State = RouteReservationState.Waiting, ShouldWait = !replan, RequiresReplan = replan, FailureReason = replan ? RouteReservationFailureReason.ResourceBlocked : state == TrafficResourceState.Occupied ? RouteReservationFailureReason.ResourceOccupied : RouteReservationFailureReason.ResourceReserved, Message = acquired.Message }); }
            var now = DateTimeOffset.Now; foreach (var s in acquireSegments) { s.IsLocked = true; s.IsReserved = true; s.LockedAt = now; s.TrafficReservationIdsJson = JsonSerializer.Serialize(new[] { acquired.Data!.ReservationId }); } }
        e.State = (int)(e.Segments.Where(x => !x.IsReleased).All(x => x.IsLocked) ? RouteReservationState.FullyLocked : RouteReservationState.PartiallyLocked); e.UpdatedAt = DateTimeOffset.Now; await db.SaveChangesAsync(ct); return AgvResult<RouteRollingLockResultDto>.Ok(new RouteRollingLockResultDto { ReservationId = e.ReservationId, State = (RouteReservationState)e.State, Acquired = true, AcquiredWindow = Window(window, e.Segments), Message = "The rolling route window is locked." });
    }
    public async Task<AgvResult<RouteRollingReleaseResultDto>> ReleasePassedResourcesAsync(ReleasePassedRouteResourcesRequest r, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct); try { await using var db = CreateContext(); var e = await LoadAsync(db, r.ReservationId, ct); if (e is null) return AgvResult<RouteRollingReleaseResultDto>.Fail(FailureCode.InvalidRequest, "Route reservation was not found."); var passed = e.Segments.Where(x => !x.IsReleased && x.Sequence <= r.PassedSegmentSequence).ToArray(); var released = new List<TrafficResourceKey>();
            foreach (var s in passed.Where(x => x.IsLocked)) { var resources = Resources(s).Where(x => !(x.ResourceType == TrafficResourceType.Node && string.Equals(x.ResourceId, r.CurrentNodeId, StringComparison.OrdinalIgnoreCase))).ToArray(); foreach (var id in Ids(s)) if (resources.Length > 0) { var rr = await _traffic.ReleaseAsync(new TrafficReleaseRequest { Context = r.Context, ReservationId = id, AgvId = e.VehicleId, TaskId = e.TaskId, Resources = resources, Reason = $"Passed node {r.CurrentNodeId}" }, ct); if (!rr.Success) return AgvResult<RouteRollingReleaseResultDto>.Fail(rr.Code, rr.Message, rr.Retryable); } released.AddRange(resources); }
            var now = DateTimeOffset.Now; foreach (var s in passed) { s.IsLocked = false; s.IsReleased = true; s.ReleasedAt = now; } e.State = (int)(e.Segments.All(x => x.IsReleased) ? RouteReservationState.Completed : e.Segments.Any(x => x.IsLocked) ? RouteReservationState.PartiallyLocked : RouteReservationState.Created); e.UpdatedAt = now; await db.SaveChangesAsync(ct); return AgvResult<RouteRollingReleaseResultDto>.Ok(new RouteRollingReleaseResultDto { ReservationId = e.ReservationId, State = (RouteReservationState)e.State, ReleasedSegmentSequences = passed.Select(x => x.Sequence).ToArray(), ReleasedResources = Distinct(released), CurrentWindow = Window(e.Segments.Where(x => x.IsLocked).ToArray(), e.Segments) }); } finally { Gate.Release(); }
    }
    public async Task<AgvResult> ReleaseReservationAsync(ReleaseRouteReservationRequest r, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct); try { await using var db = CreateContext(); var e = await LoadAsync(db, r.ReservationId, ct); if (e is null) return AgvResult.Fail(FailureCode.InvalidRequest, "Route reservation was not found."); foreach (var s in e.Segments.Where(x => x.IsLocked)) foreach (var id in Ids(s)) { var rr = await _traffic.ReleaseAsync(new TrafficReleaseRequest { Context = r.Context, ReservationId = id, AgvId = e.VehicleId, TaskId = e.TaskId, Resources = Resources(s), Reason = r.Reason }, ct); if (!rr.Success) return rr; }
            // Passed segments can still own the vehicle's current Occupied node even though
            // their reservation flags are released. Closing the task clears that ownership too.
            var ownerRelease = await _traffic.ReleaseAsync(new TrafficReleaseRequest { Context = r.Context, AgvId = e.VehicleId, TaskId = e.TaskId, Resources = Array.Empty<TrafficResourceKey>(), Reason = r.Reason }, ct); if (!ownerRelease.Success) return ownerRelease;
            var now = DateTimeOffset.Now; foreach (var s in e.Segments) { s.IsLocked = false; s.IsReleased = true; s.ReleasedAt = now; } e.State = (int)(r.Reason.Contains("cancel", StringComparison.OrdinalIgnoreCase) ? RouteReservationState.Canceled : RouteReservationState.Released); e.ReleasedAt = now; e.UpdatedAt = now; await db.SaveChangesAsync(ct); return AgvResult.Ok(); } finally { Gate.Release(); }
    }
    public async Task<AgvResult<RouteReservationDto>> GetReservationAsync(GetRouteReservationRequest r, CancellationToken ct = default) { await Gate.WaitAsync(ct); try { await using var db = CreateContext(); var e = await LoadAsync(db, r.ReservationId, ct); return e is null ? AgvResult<RouteReservationDto>.Fail(FailureCode.InvalidRequest, "Route reservation was not found.") : AgvResult<RouteReservationDto>.Ok(Map(e)); } finally { Gate.Release(); } }
    public async Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetReservationsByTaskAsync(GetTaskRouteReservationsRequest r, CancellationToken ct = default) { await Gate.WaitAsync(ct); try { await using var db = CreateContext(); var x = await db.RouteReservations.Include(a => a.Segments).Where(a => a.TaskId == r.TaskId).ToArrayAsync(ct); return AgvResult<IReadOnlyList<RouteReservationDto>>.Ok(x.Select(Map).ToArray()); } finally { Gate.Release(); } }
    public async Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetReservationsByVehicleAsync(GetVehicleRouteReservationsRequest r, CancellationToken ct = default) { await Gate.WaitAsync(ct); try { await using var db = CreateContext(); var x = await db.RouteReservations.Include(a => a.Segments).Where(a => a.VehicleId == r.VehicleId).ToArrayAsync(ct); return AgvResult<IReadOnlyList<RouteReservationDto>>.Ok(x.Select(Map).ToArray()); } finally { Gate.Release(); } }
    public async Task<AgvResult<IReadOnlyList<RouteReservationDto>>> GetActiveReservationsAsync(GetRouteReservationsRequest r, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct); try
        {
            var terminal = new[] { (int)RouteReservationState.Released, (int)RouteReservationState.Canceled, (int)RouteReservationState.Completed, (int)RouteReservationState.Failed };
            await using var db = CreateContext();
            var q = db.RouteReservations.Include(a => a.Segments).AsNoTracking().Where(a => !terminal.Contains(a.State));
            if (!string.IsNullOrWhiteSpace(r.TaskId)) q = q.Where(a => a.TaskId == r.TaskId);
            if (!string.IsNullOrWhiteSpace(r.VehicleId)) q = q.Where(a => a.VehicleId == r.VehicleId);
            var x = (await q.ToArrayAsync(ct))
                .OrderByDescending(a => a.UpdatedAt)
                .ToArray();
            return AgvResult<IReadOnlyList<RouteReservationDto>>.Ok(x.Select(Map).ToArray());
        }
        finally { Gate.Release(); }
    }

    private AgvDispatcherDbContext CreateContext() => new(_options);
    private static Task<RouteReservationEntity?> LoadAsync(AgvDispatcherDbContext db, string id, CancellationToken ct) => db.RouteReservations.Include(x => x.Segments).FirstOrDefaultAsync(x => x.ReservationId == id, ct);
    private static RouteReservationDto Map(RouteReservationEntity e) { var segments = e.Segments.OrderBy(x => x.Sequence).Select(Map).ToArray(); return new RouteReservationDto { ReservationId = e.ReservationId, TaskId = e.TaskId, VehicleId = e.VehicleId, PlanId = e.PlanId, MapId = e.MapId, MapVersion = e.MapVersion, RollingWindowSize = e.RollingWindowSize, ReservationPolicy = (RouteReservationPolicy)e.ReservationPolicy, State = (RouteReservationState)e.State, Segments = segments, CurrentWindow = Window(segments.Where(x => x.IsLocked).Select((x, i) => e.Segments.First(s => s.Sequence == x.Segment.Sequence)).ToArray(), e.Segments), CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt, ReleasedAt = e.ReleasedAt }; }
    private static RouteReservedSegmentDto Map(RouteReservationSegmentEntity s) => new() { Segment = new PathSegmentDto { Sequence = s.Sequence, FromNodeId = s.FromNodeId, ToNodeId = s.ToNodeId, EdgeId = s.EdgeId, Distance = s.Distance, EstimatedSeconds = s.Cost }, Resources = Resources(s), TrafficReservationIds = Ids(s), IsReserved = s.IsReserved, IsLocked = s.IsLocked, IsReleased = s.IsReleased, LockedAt = s.LockedAt, ReleasedAt = s.ReleasedAt };
    private static RouteRollingWindowDto? Window(IEnumerable<RouteReservationSegmentEntity> selected, IEnumerable<RouteReservationSegmentEntity> all) { var x = selected.OrderBy(s => s.Sequence).ToArray(); if (x.Length == 0) return null; return new RouteRollingWindowDto { StartSegmentSequence = x[0].Sequence, EndSegmentSequence = x[^1].Sequence, Segments = x.Select(Map).ToArray(), IsCompletePathLocked = all.Where(s => !s.IsReleased).All(s => s.IsLocked) }; }
    private static TrafficResourceKey[] Resources(PathSegmentDto s) => new[] { new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = s.EdgeId }, new TrafficResourceKey { ResourceType = TrafficResourceType.Node, ResourceId = s.ToNodeId } };
    private static TrafficResourceKey[] Resources(RouteReservationSegmentEntity s) => JsonSerializer.Deserialize<TrafficResourceKey[]>(s.ResourcesJson) ?? Array.Empty<TrafficResourceKey>();
    private static string[] Ids(RouteReservationSegmentEntity s) => JsonSerializer.Deserialize<string[]>(s.TrafficReservationIdsJson) ?? Array.Empty<string>();
    private static IReadOnlyList<TrafficResourceKey> Distinct(IEnumerable<TrafficResourceKey> x) => x.GroupBy(r => $"{(int)r.ResourceType}:{r.ResourceId}").Select(g => g.First()).ToArray();
}
