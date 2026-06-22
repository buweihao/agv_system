using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Services;

/// <summary>SQLite-backed single-process traffic resource controller.</summary>
public sealed class PersistentTrafficControlService : ITrafficControlService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly AgvDispatcherDbContext _db;
    public PersistentTrafficControlService(AgvDispatcherDbContext db) => _db = db;

    public async Task<AgvResult<TrafficSnapshotDto>> GetTrafficSnapshotAsync(RequestContext context, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct); try { await CleanupAsync(ct); var rows = await _db.TrafficResourceLocks.AsNoTracking().ToArrayAsync(ct); return AgvResult<TrafficSnapshotDto>.Ok(new TrafficSnapshotDto { Version = rows.Sum(x => x.ConcurrencyToken), GeneratedAt = DateTimeOffset.Now, Resources = rows.Select(Map).ToArray() }); } finally { Gate.Release(); }
    }
    public async Task<AgvResult<TrafficResourceStatusDto>> GetResourceStatusAsync(TrafficResourceKey resource, RequestContext context, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct); try { await CleanupAsync(ct); var row = await FindAsync(resource, ct); return AgvResult<TrafficResourceStatusDto>.Ok(row is null ? Free(resource) : Map(row)); } finally { Gate.Release(); }
    }
    public async Task<AgvResult<IReadOnlyList<TrafficResourceStatusDto>>> GetResourceStatusesAsync(IReadOnlyList<TrafficResourceKey> resources, RequestContext context, CancellationToken ct = default)
    {
        var list = new List<TrafficResourceStatusDto>(); foreach (var r in resources) list.Add((await GetResourceStatusAsync(r, context, ct)).Data!); return AgvResult<IReadOnlyList<TrafficResourceStatusDto>>.Ok(list);
    }
    public async Task<AgvResult<TrafficAvailabilityResultDto>> CheckAvailabilityAsync(TrafficAvailabilityRequest request, CancellationToken ct = default)
    {
        var statuses = (await GetResourceStatusesAsync(request.Resources, request.Context, ct)).Data!; var conflicts = statuses.Where(x => x.State != TrafficResourceState.Free).Select(Conflict).ToArray();
        return AgvResult<TrafficAvailabilityResultDto>.Ok(new TrafficAvailabilityResultDto { IsAvailable = conflicts.Length == 0, ResourceStatuses = statuses, Conflicts = conflicts, Message = conflicts.Length == 0 ? "All requested resources are available." : "One or more requested resources are unavailable." });
    }
    public async Task<AgvResult<TrafficReservationDto>> TryAcquireAsync(TrafficAcquireRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AgvId) || request.Resources.Count == 0) return AgvResult<TrafficReservationDto>.Fail(FailureCode.InvalidRequest, "An AGV id and resources are required.");
        await Gate.WaitAsync(ct); try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct); await CleanupAsync(ct); var resources = Distinct(request.Resources);
            foreach (var r in resources) { var held = await FindAsync(r, ct); if (held is not null) return AgvResult<TrafficReservationDto>.Fail(FailureCode.InvalidState, $"Resource {r.ResourceType}:{r.ResourceId} is {(TrafficResourceState)held.State}.", (TrafficResourceState)held.State != TrafficResourceState.Disabled); }
            var id = Guid.NewGuid().ToString("N"); var now = DateTimeOffset.Now; DateTimeOffset? expires = request.Ttl.HasValue ? now.Add(request.Ttl.Value) : null;
            foreach (var r in resources) _db.TrafficResourceLocks.Add(new TrafficResourceLockEntity { ResourceType = (int)r.ResourceType, ResourceId = r.ResourceId, State = (int)State(request.LockMode), OccupiedByAgvId = request.LockMode == TrafficLockMode.Occupy ? request.AgvId : null, ReservedByAgvId = request.LockMode == TrafficLockMode.Occupy ? null : request.AgvId, TaskId = request.TaskId, RouteReservationId = request.Context.CorrelationId, TrafficReservationId = id, LockMode = (int)request.LockMode, Reason = request.Reason, ExpireAt = expires, CreatedAt = now, UpdatedAt = now, ConcurrencyToken = 1 });
            try { await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); } catch (DbUpdateException) { _db.ChangeTracker.Clear(); return AgvResult<TrafficReservationDto>.Fail(FailureCode.InvalidState, "A traffic resource was acquired concurrently.", true); }
            return AgvResult<TrafficReservationDto>.Ok(new TrafficReservationDto { ReservationId = id, AgvId = request.AgvId, TaskId = request.TaskId, LockMode = request.LockMode, Resources = resources, CreatedAt = now, ExpireAt = expires, Status = TrafficReservationStatus.Active });
        } finally { Gate.Release(); }
    }
    public async Task<AgvResult> ReleaseAsync(TrafficReleaseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReservationId) && string.IsNullOrWhiteSpace(request.AgvId) && string.IsNullOrWhiteSpace(request.TaskId)) return AgvResult.Fail(FailureCode.InvalidRequest, "An owner is required.");
        await Gate.WaitAsync(ct); try { await CleanupAsync(ct); var rows = request.Resources.Count == 0 ? await _db.TrafficResourceLocks.ToListAsync(ct) : await RowsAsync(request.Resources, ct); var matches = rows.Where(x => Owner(x, request)).ToArray(); _db.RemoveRange(matches); await _db.SaveChangesAsync(ct); return AgvResult.Ok($"Released {matches.Length} traffic resource(s)."); } finally { Gate.Release(); }
    }
    public async Task<AgvResult> UpdateAgvOccupancyAsync(AgvOccupancyUpdateRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AgvId)) return AgvResult.Fail(FailureCode.InvalidRequest, "An AGV id is required."); var reported = Reported(request);
        await Gate.WaitAsync(ct); try { await CleanupAsync(ct); foreach (var r in reported) { var held = await FindAsync(r, ct); if (held is not null && ((TrafficResourceState)held.State is TrafficResourceState.Blocked or TrafficResourceState.Disabled || (!string.Equals(held.OccupiedByAgvId, request.AgvId, StringComparison.OrdinalIgnoreCase) && !string.Equals(held.ReservedByAgvId, request.AgvId, StringComparison.OrdinalIgnoreCase)))) return AgvResult.Fail(FailureCode.InvalidState, $"Resource {r.ResourceType}:{r.ResourceId} is owned by another AGV."); }
            var keys = reported.Select(Key).ToHashSet(); var old = await _db.TrafficResourceLocks.Where(x => x.State == (int)TrafficResourceState.Occupied && x.OccupiedByAgvId == request.AgvId).ToListAsync(ct); _db.RemoveRange(old.Where(x => !keys.Contains($"{x.ResourceType}:{x.ResourceId}")));
            foreach (var r in reported) { var row = await FindAsync(r, ct); if (row is null) { row = new TrafficResourceLockEntity { ResourceType = (int)r.ResourceType, ResourceId = r.ResourceId, CreatedAt = DateTimeOffset.Now }; _db.Add(row); } row.State = (int)TrafficResourceState.Occupied; row.OccupiedByAgvId = request.AgvId; row.ReservedByAgvId = null; row.TaskId = request.TaskId; row.TrafficReservationId = null; row.Reason = "AGV occupancy report"; row.UpdatedAt = request.ReportTime; row.ConcurrencyToken++; }
            await _db.SaveChangesAsync(ct); return AgvResult.Ok(); } finally { Gate.Release(); }
    }
    public async Task<AgvResult<TrafficBlockDto>> BlockResourcesAsync(TrafficBlockRequest request, CancellationToken ct = default)
    {
        var acq = await TryAcquireAsync(new TrafficAcquireRequest { Context = request.Context, AgvId = request.OperatorId ?? "SYSTEM", TaskId = null, LockMode = TrafficLockMode.Block, Resources = request.Resources, Reason = request.Reason, Ttl = request.Ttl }, ct); if (!acq.Success) return AgvResult<TrafficBlockDto>.Fail(acq.Error!); return AgvResult<TrafficBlockDto>.Ok(new TrafficBlockDto { BlockId = acq.Data!.ReservationId, Resources = acq.Data.Resources, Reason = request.Reason, OperatorId = request.OperatorId, CreatedAt = acq.Data.CreatedAt, ExpireAt = acq.Data.ExpireAt });
    }
    public async Task<AgvResult> UnblockResourcesAsync(TrafficUnblockRequest request, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct); try { var rows = await RowsAsync(request.Resources, ct); var blocked = rows.Where(x => x.State == (int)TrafficResourceState.Blocked).ToArray(); _db.RemoveRange(blocked); await _db.SaveChangesAsync(ct); return AgvResult.Ok($"Unblocked {blocked.Length} traffic resource(s)."); } finally { Gate.Release(); }
    }

    private async Task CleanupAsync(CancellationToken ct) { var now = DateTimeOffset.Now; var expirable = await _db.TrafficResourceLocks.Where(x => x.ExpireAt != null).ToListAsync(ct); var expired = expirable.Where(x => x.ExpireAt <= now).ToArray(); if (expired.Length > 0) { _db.RemoveRange(expired); await _db.SaveChangesAsync(ct); } }
    private Task<TrafficResourceLockEntity?> FindAsync(TrafficResourceKey r, CancellationToken ct) => _db.TrafficResourceLocks.FirstOrDefaultAsync(x => x.ResourceType == (int)r.ResourceType && x.ResourceId == r.ResourceId, ct);
    private async Task<List<TrafficResourceLockEntity>> RowsAsync(IEnumerable<TrafficResourceKey> resources, CancellationToken ct) { var keys = Distinct(resources).Select(Key).ToHashSet(); return (await _db.TrafficResourceLocks.ToListAsync(ct)).Where(x => keys.Contains($"{x.ResourceType}:{x.ResourceId}")).ToList(); }
    private static bool Owner(TrafficResourceLockEntity x, TrafficReleaseRequest r) => (!string.IsNullOrWhiteSpace(r.ReservationId) && r.ReservationId == x.TrafficReservationId) || (!string.IsNullOrWhiteSpace(r.AgvId) && r.AgvId == (x.OccupiedByAgvId ?? x.ReservedByAgvId)) || (!string.IsNullOrWhiteSpace(r.TaskId) && r.TaskId == x.TaskId);
    private static TrafficResourceStatusDto Map(TrafficResourceLockEntity x) => new() { Resource = new TrafficResourceKey { ResourceType = (TrafficResourceType)x.ResourceType, ResourceId = x.ResourceId }, State = (TrafficResourceState)x.State, OccupiedByAgvId = x.OccupiedByAgvId, ReservedByAgvId = x.ReservedByAgvId, TaskId = x.TaskId, ReservationId = x.TrafficReservationId, Reason = x.Reason, ExpireAt = x.ExpireAt, UpdatedAt = x.UpdatedAt };
    private static TrafficResourceStatusDto Free(TrafficResourceKey r) => new() { Resource = r, State = TrafficResourceState.Free, UpdatedAt = DateTimeOffset.Now };
    private static TrafficConflictDto Conflict(TrafficResourceStatusDto x) => new() { Resource = x.Resource, CurrentState = x.State, ConflictAgvId = x.OccupiedByAgvId ?? x.ReservedByAgvId, ConflictTaskId = x.TaskId, ReservationId = x.ReservationId, FailureCode = x.State == TrafficResourceState.Occupied ? TrafficFailureCode.ResourceAlreadyOccupied : x.State is TrafficResourceState.Blocked or TrafficResourceState.Disabled ? TrafficFailureCode.ResourceBlocked : TrafficFailureCode.ResourceAlreadyReserved, Message = $"Resource is {x.State}." };
    private static TrafficResourceState State(TrafficLockMode m) => m switch { TrafficLockMode.Reserve => TrafficResourceState.Reserved, TrafficLockMode.Occupy => TrafficResourceState.Occupied, TrafficLockMode.Lock => TrafficResourceState.Locked, TrafficLockMode.Block => TrafficResourceState.Blocked, _ => TrafficResourceState.Unknown };
    private static IReadOnlyList<TrafficResourceKey> Distinct(IEnumerable<TrafficResourceKey> r) => r.GroupBy(Key).Select(x => x.First()).ToArray();
    private static string Key(TrafficResourceKey r) => $"{(int)r.ResourceType}:{r.ResourceId}";
    private static TrafficResourceKey[] Reported(AgvOccupancyUpdateRequest r) => r.OccupiedNodeIds.Append(r.CurrentNodeId).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new TrafficResourceKey { ResourceType = TrafficResourceType.Node, ResourceId = x! }).Concat(r.OccupiedEdgeIds.Append(r.CurrentEdgeId).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = x! })).GroupBy(Key).Select(x => x.First()).ToArray();
}
