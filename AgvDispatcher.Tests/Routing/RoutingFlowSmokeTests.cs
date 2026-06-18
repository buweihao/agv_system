using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Planning.Interfaces;
using AgvDispatcher.Core.Contracts.Planning.Requests;
using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Reservations.Interfaces;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Interfaces;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Infrastructure.Mock.Reservations;
using AgvDispatcher.Infrastructure.Mock.Planning;
using AgvDispatcher.Infrastructure.Mock.Traffic;
using Xunit;

namespace AgvDispatcher.Tests.Routing;

public sealed class RoutingFlowSmokeTests
{
    private static readonly RequestContext Context = new() { SourceModule = "RoutingFlowSmokeTests" };

    [Fact]
    public async Task RollingWindow_ReleasesPassedEdges_AndEventuallyReleasesAllResources()
    {
        var (map, plan) = await PlanLinearRouteAsync();
        var segments = plan.Segments!;
        ITrafficControlService traffic = new MockTrafficControlService();
        IRouteReservationService reservations = new MockRouteReservationService(traffic);

        var reservationId = await CreateReservationAsync(
            reservations,
            "TASK-001",
            "AGV-001",
            map,
            plan.PlanId,
            segments,
            rollingWindowSize: 2);

        var firstWindow = await reservations.AcquireNextWindowAsync(new AcquireNextRouteWindowRequest
        {
            Context = Context,
            ReservationId = reservationId,
            CurrentNodeId = "N1",
            CurrentSegmentSequence = 0,
            RollingWindowSize = 2
        });

        Assert.True(firstWindow.Success);
        Assert.True(firstWindow.Data!.Acquired);
        await AssertEdgeStatesAsync(traffic, ("E1", TrafficResourceState.Locked), ("E2", TrafficResourceState.Locked));

        var releasedPassed = await reservations.ReleasePassedResourcesAsync(new ReleasePassedRouteResourcesRequest
        {
            Context = Context,
            ReservationId = reservationId,
            CurrentNodeId = "N2",
            PassedSegmentSequence = 1
        });

        Assert.True(releasedPassed.Success);
        await AssertEdgeStatesAsync(traffic, ("E1", TrafficResourceState.Free), ("E2", TrafficResourceState.Locked));

        var nextWindow = await reservations.AcquireNextWindowAsync(new AcquireNextRouteWindowRequest
        {
            Context = Context,
            ReservationId = reservationId,
            CurrentNodeId = "N2",
            CurrentSegmentSequence = 1,
            RollingWindowSize = 2
        });

        Assert.True(nextWindow.Success);
        Assert.True(nextWindow.Data!.Acquired);
        await AssertEdgeStatesAsync(traffic, ("E2", TrafficResourceState.Locked), ("E3", TrafficResourceState.Locked));

        var released = await reservations.ReleaseReservationAsync(new ReleaseRouteReservationRequest
        {
            Context = Context,
            ReservationId = reservationId,
            Reason = "Smoke test completed"
        });

        Assert.True(released.Success);
        await AssertEdgeStatesAsync(
            traffic,
            ("E1", TrafficResourceState.Free),
            ("E2", TrafficResourceState.Free),
            ("E3", TrafficResourceState.Free));
        await AssertNoHeldResourcesAsync(traffic);
    }

    [Fact]
    public async Task ConflictingVehicles_SecondVehicleWaits_ThenAcquiresAfterFirstReleases()
    {
        var (map, plan) = await PlanLinearRouteAsync();
        var segments = plan.Segments!;
        ITrafficControlService traffic = new MockTrafficControlService();
        IRouteReservationService reservations = new MockRouteReservationService(traffic);

        var firstReservationId = await CreateReservationAsync(
            reservations, "TASK-001", "AGV-001", map, plan.PlanId, segments, rollingWindowSize: 2);
        var secondReservationId = await CreateReservationAsync(
            reservations, "TASK-002", "AGV-002", map, plan.PlanId, new[] { segments[1] }, rollingWindowSize: 1);

        var firstAcquire = await AcquireAsync(reservations, firstReservationId, currentSequence: 0, windowSize: 2);
        Assert.True(firstAcquire.Success);
        Assert.True(firstAcquire.Data!.Acquired);
        await AssertEdgeStatesAsync(traffic, ("E1", TrafficResourceState.Locked), ("E2", TrafficResourceState.Locked));

        var conflictingAcquire = await AcquireAsync(reservations, secondReservationId, currentSequence: 1, windowSize: 1);
        Assert.True(conflictingAcquire.Success);
        Assert.False(conflictingAcquire.Data!.Acquired);
        Assert.True(conflictingAcquire.Data.ShouldWait);

        var firstRelease = await reservations.ReleaseReservationAsync(new ReleaseRouteReservationRequest
        {
            Context = Context,
            ReservationId = firstReservationId,
            Reason = "Allow waiting AGV to continue"
        });
        Assert.True(firstRelease.Success);

        var retry = await AcquireAsync(reservations, secondReservationId, currentSequence: 1, windowSize: 1);
        Assert.True(retry.Success);
        Assert.True(retry.Data!.Acquired);
        await AssertEdgeStatesAsync(traffic, ("E2", TrafficResourceState.Locked));

        var secondRelease = await reservations.ReleaseReservationAsync(new ReleaseRouteReservationRequest
        {
            Context = Context,
            ReservationId = secondReservationId,
            Reason = "Smoke test cleanup"
        });
        Assert.True(secondRelease.Success);
        await AssertEdgeStatesAsync(traffic, ("E1", TrafficResourceState.Free), ("E2", TrafficResourceState.Free));
        await AssertNoHeldResourcesAsync(traffic);
    }

    private static async Task<string> CreateReservationAsync(
        IRouteReservationService reservations,
        string taskId,
        string vehicleId,
        MapSnapshotDto map,
        string planId,
        IReadOnlyList<PathSegmentDto> segments,
        int rollingWindowSize)
    {
        var result = await reservations.CreateReservationAsync(new CreateRouteReservationRequest
        {
            Context = Context,
            TaskId = taskId,
            VehicleId = vehicleId,
            PlanId = planId,
            MapId = map.MapId,
            MapVersion = map.Version,
            Segments = segments,
            RollingWindowSize = rollingWindowSize
        });

        Assert.True(result.Success);
        return Assert.IsType<string>(result.Data!.ReservationId);
    }

    private static Task<AgvResult<AgvDispatcher.Core.Contracts.Reservations.Models.RouteRollingLockResultDto>> AcquireAsync(
        IRouteReservationService reservations,
        string reservationId,
        int currentSequence,
        int windowSize) =>
        reservations.AcquireNextWindowAsync(new AcquireNextRouteWindowRequest
        {
            Context = Context,
            ReservationId = reservationId,
            CurrentSegmentSequence = currentSequence,
            RollingWindowSize = windowSize
        });

    private static async Task AssertEdgeStatesAsync(
        ITrafficControlService traffic,
        params (string EdgeId, TrafficResourceState ExpectedState)[] expectations)
    {
        foreach (var (edgeId, expectedState) in expectations)
        {
            var result = await traffic.GetResourceStatusAsync(
                new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = edgeId },
                Context);

            Assert.True(result.Success);
            Assert.Equal(expectedState, result.Data!.State);
        }
    }

    private static async Task AssertNoHeldResourcesAsync(ITrafficControlService traffic)
    {
        var snapshot = await traffic.GetTrafficSnapshotAsync(Context);

        Assert.True(snapshot.Success);
        Assert.Empty(snapshot.Data!.Resources);
    }

    private static async Task<(MapSnapshotDto Map, PathPlanResult Plan)> PlanLinearRouteAsync()
    {
        var nodes = Enumerable.Range(1, 4)
            .Select(index => new MapNodeDto
            {
                NodeId = $"N{index}",
                NodeCode = $"N{index}",
                NodeName = $"Node {index}",
                X = index - 1,
                Y = 0
            })
            .ToArray();

        var edges = Enumerable.Range(1, 3)
            .Select(index => new MapEdgeDto
            {
                EdgeId = $"E{index}",
                FromNodeId = $"N{index}",
                ToNodeId = $"N{index + 1}",
                Distance = 1,
                Direction = MapEdgeDirection.OneWay
            })
            .ToArray();

        var map = new MapSnapshotDto
        {
            MapId = "SMOKE-MAP",
            MapName = "N1 -> N2 -> N3 -> N4",
            Version = "1",
            Nodes = nodes,
            Edges = edges
        };

        IPathPlanner planner = new DijkstraPathPlanner();
        var plan = await planner.PlanAsync(new PathPlanRequest
        {
            Context = Context,
            MapSnapshot = map,
            VehicleId = "AGV-001",
            StartNodeId = "N1",
            TargetNodeId = "N4"
        });

        Assert.True(plan.Success);
        Assert.True(plan.Data!.IsReachable);
        Assert.Equal(new[] { "E1", "E2", "E3" }, plan.Data.Segments!.Select(segment => segment.EdgeId));
        return (map, plan.Data);
    }
}
