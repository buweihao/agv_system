using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using AgvDispatcher.Infrastructure.Sqlite.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgvDispatcher.Tests.Dispatching;

public sealed class PersistentTrafficReservationLifecycleTests
{
    private static readonly RequestContext Context = new() { SourceModule = nameof(PersistentTrafficReservationLifecycleTests) };

    [Fact] public async Task StartTaskAsync_ShouldPersistInitialWindowLocks() { await using var f = await Fixture.CreateAsync(); var id = await f.CreateAsync("T1", "V1"); Assert.True((await f.Routes.AcquireNextWindowAsync(Acquire(id, 0, 2))).Data!.Acquired); Assert.Equal(4, await f.Db.TrafficResourceLocks.CountAsync()); }

    [Fact] public async Task AdvanceRouteAsync_ShouldReleasePassedEdgeAndKeepCurrentNodeOccupied()
    {
        await using var f = await Fixture.CreateAsync(); var id = await f.CreateAsync("T1", "V1"); await f.Routes.AcquireNextWindowAsync(Acquire(id, 0, 2));
        await f.Traffic.UpdateAgvOccupancyAsync(new AgvOccupancyUpdateRequest { Context = Context, AgvId = "V1", TaskId = "T1", CurrentNodeId = "N2", OccupiedNodeIds = new[] { "N2" }, ReportTime = DateTimeOffset.Now });
        await f.Routes.ReleasePassedResourcesAsync(new ReleasePassedRouteResourcesRequest { Context = Context, ReservationId = id, CurrentNodeId = "N2", PassedSegmentSequence = 1 }); await f.Routes.AcquireNextWindowAsync(Acquire(id, 1, 2));
        Assert.Equal(TrafficResourceState.Free, await f.State(TrafficResourceType.Edge, "E1")); Assert.Equal(TrafficResourceState.Occupied, await f.State(TrafficResourceType.Node, "N2")); Assert.Equal(TrafficResourceState.Locked, await f.State(TrafficResourceType.Edge, "E3")); Assert.Equal(TrafficResourceState.Locked, await f.State(TrafficResourceType.Node, "N4"));
    }

    [Fact] public async Task SecondVehicle_ShouldWait_WhenFirstVehicleOccupiesCurrentNode()
    {
        await using var f = await Fixture.CreateAsync(); var a = await f.CreateAsync("T1", "V1"); await f.Routes.AcquireNextWindowAsync(Acquire(a, 0, 2)); await f.Traffic.UpdateAgvOccupancyAsync(new AgvOccupancyUpdateRequest { Context = Context, AgvId = "V1", TaskId = "T1", CurrentNodeId = "N2", OccupiedNodeIds = new[] { "N2" }, ReportTime = DateTimeOffset.Now }); var b = await f.CreateAsync("T2", "V2"); var wait = await f.Routes.AcquireNextWindowAsync(Acquire(b, 0, 2)); Assert.True(wait.Data!.ShouldWait); Assert.False(wait.Data.Acquired);
    }

    [Fact] public async Task RetryWaitingTaskAsync_ShouldStartAfterResourcesReleased()
    {
        await using var f = await Fixture.CreateAsync(); var a = await f.CreateAsync("T1", "V1"); var b = await f.CreateAsync("T2", "V2"); await f.Routes.AcquireNextWindowAsync(Acquire(a, 0, 2)); Assert.True((await f.Routes.AcquireNextWindowAsync(Acquire(b, 0, 2))).Data!.ShouldWait); await f.Routes.ReleaseReservationAsync(Release(a)); Assert.True((await f.Routes.AcquireNextWindowAsync(Acquire(b, 0, 2))).Data!.Acquired);
    }

    [Fact] public async Task CancelTaskAsync_ShouldReleaseAllPersistentLocks() { await using var f = await Fixture.CreateAsync(); var id = await f.CreateAsync("T1", "V1"); await f.Routes.AcquireNextWindowAsync(Acquire(id, 0, 2)); await f.Routes.ReleaseReservationAsync(new ReleaseRouteReservationRequest { Context = Context, ReservationId = id, Reason = "cancel task" }); Assert.Empty(await f.Db.TrafficResourceLocks.ToArrayAsync()); }
    [Fact] public async Task CompleteTaskAsync_ShouldReleaseAllPersistentLocks() { await using var f = await Fixture.CreateAsync(); var id = await f.CreateAsync("T1", "V1"); await f.Routes.AcquireNextWindowAsync(Acquire(id, 0, 2)); await f.Routes.ReleaseReservationAsync(Release(id)); Assert.Empty(await f.Db.TrafficResourceLocks.ToArrayAsync()); }

    [Fact] public async Task BlockedResource_ShouldRequireReplan()
    {
        await using var f = await Fixture.CreateAsync(); await f.Traffic.BlockResourcesAsync(new TrafficBlockRequest { Context = Context, Resources = new[] { R(TrafficResourceType.Edge, "E1") }, Reason = "maintenance" }); var id = await f.CreateAsync("T1", "V1"); var result = await f.Routes.AcquireNextWindowAsync(Acquire(id, 0, 2)); Assert.True(result.Data!.RequiresReplan); Assert.False(result.Data.ShouldWait);
    }

    [Fact] public async Task PersistentLocks_ShouldBeRecoverableAfterServiceRecreated()
    {
        await using var f = await Fixture.CreateAsync(); var id = await f.CreateAsync("T1", "V1"); await f.Routes.AcquireNextWindowAsync(Acquire(id, 0, 2)); await using var db2 = new AgvDispatcherDbContext(f.Options); var traffic2 = new PersistentTrafficControlService(db2); var routes2 = new PersistentRouteReservationService(db2, traffic2); Assert.Equal(TrafficResourceState.Locked, (await traffic2.GetResourceStatusAsync(R(TrafficResourceType.Edge, "E1"), Context)).Data!.State); Assert.True((await routes2.GetReservationAsync(new GetRouteReservationRequest { Context = Context, ReservationId = id })).Success);
    }

    [Fact] public async Task ConcurrentAcquire_ShouldAllowOnlyOneVehicle()
    {
        await using var f = await Fixture.CreateAsync(); var resource = new[] { R(TrafficResourceType.Edge, "E1") }; var results = await Task.WhenAll(f.Traffic.TryAcquireAsync(new TrafficAcquireRequest { Context = Context, AgvId = "V1", TaskId = "T1", LockMode = TrafficLockMode.Lock, Resources = resource }), f.Traffic.TryAcquireAsync(new TrafficAcquireRequest { Context = Context, AgvId = "V2", TaskId = "T2", LockMode = TrafficLockMode.Lock, Resources = resource })); Assert.Single(results.Where(x => x.Success));
    }

    private static AcquireNextRouteWindowRequest Acquire(string id, int current, int size) => new() { Context = Context, ReservationId = id, CurrentSegmentSequence = current, RollingWindowSize = size };
    private static ReleaseRouteReservationRequest Release(string id) => new() { Context = Context, ReservationId = id, Reason = "completed" };
    private static TrafficResourceKey R(TrafficResourceType type, string id) => new() { ResourceType = type, ResourceId = id };
    private static PathSegmentDto[] Segments() => new[] { S(1, "N1", "N2", "E1"), S(2, "N2", "N3", "E2"), S(3, "N3", "N4", "E3") };
    private static PathSegmentDto S(int seq, string from, string to, string edge) => new() { Sequence = seq, FromNodeId = from, ToNodeId = to, EdgeId = edge, Distance = 1 };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection; public AgvDispatcherDbContext Db { get; } public DbContextOptions<AgvDispatcherDbContext> Options { get; } public PersistentTrafficControlService Traffic { get; } public PersistentRouteReservationService Routes { get; }
        private Fixture(SqliteConnection c, DbContextOptions<AgvDispatcherDbContext> o, AgvDispatcherDbContext db) { _connection = c; Options = o; Db = db; Traffic = new PersistentTrafficControlService(db); Routes = new PersistentRouteReservationService(db, Traffic); }
        public static async Task<Fixture> CreateAsync() { var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync(); var o = new DbContextOptionsBuilder<AgvDispatcherDbContext>().UseSqlite(c).Options; var db = new AgvDispatcherDbContext(o); await db.Database.EnsureCreatedAsync(); return new Fixture(c, o, db); }
        public async Task<string> CreateAsync(string task, string vehicle) { var result = await Routes.CreateReservationAsync(new CreateRouteReservationRequest { Context = Context, TaskId = task, VehicleId = vehicle, PlanId = "P-" + task, MapId = "M1", MapVersion = "1", RollingWindowSize = 2, Segments = Segments() }); Assert.True(result.Success); return result.Data!.ReservationId; }
        public async Task<TrafficResourceState> State(TrafficResourceType type, string id) => (await Traffic.GetResourceStatusAsync(R(type, id), Context)).Data!.State;
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
}
