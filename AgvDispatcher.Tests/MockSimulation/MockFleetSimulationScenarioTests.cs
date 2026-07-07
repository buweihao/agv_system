using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Reservations.Requests;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;
using AgvDispatcher.Core.Contracts.Traffic.Requests;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Infrastructure.Mock.Simulation;
using AgvDispatcher.Infrastructure.Mock.Traffic;
using Xunit;

namespace AgvDispatcher.Tests.MockSimulation;

public sealed class MockFleetSimulationScenarioTests
{
    private static readonly RequestContext Context = new()
    {
        SourceModule = nameof(MockFleetSimulationScenarioTests)
    };

    [Fact]
    public async Task TwoVehiclesSameTarget_ShouldSerializeAccessToSharedDestination()
    {
        var fixture = MockSimulationFixture.Create(MockSimulationMaps.CreateSameTargetMap());
        await fixture.Engine.InitializeAsync(SameTargetScenario());

        var started = await fixture.Engine.StartAllAsync();
        await RunUntilStateAsync(fixture.Engine, "AGV-A", MockVehicleSimulationState.Idle);
        var step = await fixture.Engine.StepAsync();

        Assert.True(started.Succeeded, started.Message);
        Assert.Equal(TaskState.Completed, fixture.Tasks.GetTask("TASK-A")!.State);
        Assert.Equal(MockVehicleSimulationState.Running, fixture.Engine.GetVehicleState("AGV-B")!.State);
        Assert.Contains(step.Events, item =>
            item.EventType == MockSimulationEventType.VehicleMoved &&
            item.VehicleId == "AGV-B");
    }

    [Fact]
    public async Task TwoVehiclesOppositeDirectionNarrowAisle_ShouldPreventHeadOnConflict()
    {
        var fixture = MockSimulationFixture.Create(MockSimulationMaps.CreateNarrowAisleMap());
        await fixture.Engine.InitializeAsync(NarrowAisleScenario());

        var started = await fixture.Engine.StartAllAsync();
        var step = await fixture.Engine.StepAsync();

        Assert.True(started.Succeeded, started.Message);
        Assert.Equal(MockVehicleSimulationState.Running, fixture.Engine.GetVehicleState("AGV-A")!.State);
        Assert.Equal(MockVehicleSimulationState.WaitingForTraffic, fixture.Engine.GetVehicleState("AGV-B")!.State);
        Assert.Contains(step.Events, item =>
            item.EventType == MockSimulationEventType.WaitingForTraffic &&
            item.VehicleId == "AGV-B");
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E-NARROW"));
    }

    [Fact]
    public async Task IntersectionOccupied_ShouldMakeOtherVehicleWait()
    {
        var fixture = MockSimulationFixture.Create(MockSimulationMaps.CreateIntersectionMap());
        await fixture.Engine.InitializeAsync(IntersectionScenario());

        var started = await fixture.Engine.StartAllAsync();

        Assert.True(started.Succeeded, started.Message);
        Assert.Equal(MockVehicleSimulationState.Running, fixture.Engine.GetVehicleState("AGV-A")!.State);
        Assert.Equal(MockVehicleSimulationState.WaitingForTraffic, fixture.Engine.GetVehicleState("AGV-B")!.State);
        Assert.Equal(TrafficResourceState.Locked, await NodeStateAsync(fixture.Traffic, "X"));
    }

    [Fact]
    public async Task LockedEdge_ShouldMakeOtherVehicleAvoidOrWait()
    {
        var fixture = MockSimulationFixture.Create(MockSimulationMaps.CreateSameTargetMap());
        await fixture.Engine.InitializeAsync(SameTargetScenario());

        Assert.True((await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A")).Succeeded);
        var second = await fixture.Engine.StartTaskAsync("AGV-B", "TASK-B");

        Assert.True(second.Succeeded, second.Message);
        Assert.Contains(second.Events, item => item.EventType == MockSimulationEventType.WaitingForTraffic);
        Assert.Equal(MockVehicleSimulationState.WaitingForTraffic, fixture.Engine.GetVehicleState("AGV-B")!.State);
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(fixture.Traffic, "E-X-D"));
    }

    [Fact]
    public async Task WaitingTimeout_ShouldRetryAndPrepareForReplan()
    {
        var fixture = MockSimulationFixture.Create(MockSimulationMaps.CreateSameTargetMap());
        await fixture.Engine.InitializeAsync(SameTargetScenario(new MockSimulationOptions
        {
            RollingWindowSize = 2,
            WaitTimeout = TimeSpan.Zero,
            RetryInterval = TimeSpan.Zero,
            MaxRetryCount = 0,
            OnTimeoutPolicy = MockSimulationTimeoutPolicy.RetryOnly
        }));
        Assert.True((await fixture.Engine.StartAllAsync()).Succeeded);

        var step = await fixture.Engine.StepAsync();

        Assert.True(step.Succeeded, step.Message);
        Assert.Contains(step.Events, item =>
            item.EventType == MockSimulationEventType.WaitingTimedOut &&
            item.VehicleId == "AGV-B");
        Assert.Contains(step.Events, item =>
            item.EventType == MockSimulationEventType.ReplanRequired &&
            item.VehicleId == "AGV-B");
        Assert.Equal(MockVehicleSimulationState.TimedOut, fixture.Engine.GetVehicleState("AGV-B")!.State);
    }

    [Fact]
    public async Task CancelTask_ShouldReleaseReservationAndTrafficResources()
    {
        var fixture = MockSimulationFixture.Create(MockSimulationMaps.CreateLinearMap());
        await fixture.Engine.InitializeAsync(LinearScenario());
        Assert.True((await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A")).Succeeded);
        Assert.True((await fixture.Engine.StepAsync()).Succeeded);

        var canceled = await fixture.Engine.CancelTaskAsync("TASK-A");

        Assert.True(canceled.Succeeded, canceled.Message);
        Assert.Equal(TaskState.Cancelled, fixture.Tasks.GetTask("TASK-A")!.State);
        Assert.Equal(MockVehicleSimulationState.Idle, fixture.Engine.GetVehicleState("AGV-A")!.State);
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E1"));
        Assert.Equal(TrafficResourceState.Free, await EdgeStateAsync(fixture.Traffic, "E2"));
        Assert.Equal(TrafficResourceState.Free, await NodeStateAsync(fixture.Traffic, "N2"));

        var activeReservations = await fixture.Reservations.GetActiveReservationsAsync(
            new GetRouteReservationsRequest { Context = Context });
        Assert.True(activeReservations.Success, activeReservations.Message);
        Assert.Empty(activeReservations.Data!);
    }

    [Fact]
    public async Task FaultVehicle_ShouldHoldOrReleaseResourcesByPolicy()
    {
        var holdFixture = MockSimulationFixture.Create(MockSimulationMaps.CreateSameTargetMap());
        await holdFixture.Engine.InitializeAsync(SameTargetScenario());
        Assert.True((await holdFixture.Engine.StartAllAsync()).Succeeded);

        Assert.True((await holdFixture.Engine.InjectFaultAsync("AGV-A", MockFaultPolicy.HoldResources)).Succeeded);
        var holdStep = await holdFixture.Engine.StepAsync();

        Assert.True(holdStep.Succeeded, holdStep.Message);
        Assert.Equal(MockVehicleSimulationState.WaitingForTraffic, holdFixture.Engine.GetVehicleState("AGV-B")!.State);
        Assert.Equal(TrafficResourceState.Locked, await EdgeStateAsync(holdFixture.Traffic, "E-X-D"));

        var releaseFixture = MockSimulationFixture.Create(MockSimulationMaps.CreateSameTargetMap());
        await releaseFixture.Engine.InitializeAsync(SameTargetScenario());
        Assert.True((await releaseFixture.Engine.StartAllAsync()).Succeeded);

        Assert.True((await releaseFixture.Engine.InjectFaultAsync("AGV-A", MockFaultPolicy.ReleaseReservation)).Succeeded);
        var releaseStep = await releaseFixture.Engine.StepAsync();

        Assert.True(releaseStep.Succeeded, releaseStep.Message);
        Assert.Equal(MockVehicleSimulationState.Running, releaseFixture.Engine.GetVehicleState("AGV-B")!.State);
        Assert.Contains(releaseStep.Events, item =>
            item.EventType == MockSimulationEventType.RetryAttempted &&
            item.VehicleId == "AGV-B");
    }

    [Fact]
    public async Task DisabledMapEdge_ShouldMarkCurrentRouteInvalid()
    {
        var mutableMap = MockSimulationMaps.CreateMutableMapWithDisableEdgeSupport();
        mutableMap.DisableEdge("E-S-A");
        var fixture = MockSimulationFixture.Create(mutableMap.Snapshot);
        await fixture.Engine.InitializeAsync(AlternativeRouteScenario(new MockSimulationOptions
        {
            RollingWindowSize = 1,
            WaitTimeout = TimeSpan.Zero,
            RetryInterval = TimeSpan.Zero,
            MaxRetryCount = 1
        }));
        Assert.True((await fixture.Engine.StartTaskAsync("AGV-A", "TASK-A")).Succeeded);
        var blocked = await fixture.Traffic.BlockResourcesAsync(new TrafficBlockRequest
        {
            Context = Context,
            Resources = new[]
            {
                new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = "E-B-C" }
            },
            Reason = "Mock map edge disabled while current route is active"
        });
        Assert.True(blocked.Success, blocked.Message);

        var step = await fixture.Engine.StepAsync();

        Assert.True(step.Succeeded, step.Message);
        Assert.Contains(step.Events, item => item.EventType == MockSimulationEventType.ReplanRequired);
        Assert.Equal(MockVehicleSimulationState.Replanning, fixture.Engine.GetVehicleState("AGV-A")!.State);
    }

    private static MockSimulationScenario SameTargetScenario(MockSimulationOptions? options = null) => new()
    {
        ScenarioId = "same-target",
        Name = "Two vehicles same target",
        MapSnapshot = MockSimulationMaps.CreateSameTargetMap(),
        Vehicles = new[]
        {
            Vehicle("AGV-A", "P1"),
            Vehicle("AGV-B", "P2")
        },
        Tasks = new[]
        {
            SimulationTask("TASK-A", "P1", "D", "AGV-A"),
            SimulationTask("TASK-B", "P2", "D", "AGV-B")
        },
        Options = options ?? DefaultOptions(2)
    };

    private static MockSimulationScenario NarrowAisleScenario() => new()
    {
        ScenarioId = "narrow-aisle",
        Name = "Opposite direction narrow aisle",
        MapSnapshot = MockSimulationMaps.CreateNarrowAisleMap(),
        Vehicles = new[]
        {
            Vehicle("AGV-A", "L"),
            Vehicle("AGV-B", "R")
        },
        Tasks = new[]
        {
            SimulationTask("TASK-A", "L", "R", "AGV-A"),
            SimulationTask("TASK-B", "R", "L", "AGV-B")
        },
        Options = DefaultOptions(2)
    };

    private static MockSimulationScenario IntersectionScenario() => new()
    {
        ScenarioId = "intersection",
        Name = "Shared intersection",
        MapSnapshot = MockSimulationMaps.CreateIntersectionMap(),
        Vehicles = new[]
        {
            Vehicle("AGV-A", "A1"),
            Vehicle("AGV-B", "B1")
        },
        Tasks = new[]
        {
            SimulationTask("TASK-A", "A1", "A2", "AGV-A"),
            SimulationTask("TASK-B", "B1", "B2", "AGV-B")
        },
        Options = DefaultOptions(1)
    };

    private static MockSimulationScenario LinearScenario() => new()
    {
        ScenarioId = "linear",
        Name = "Linear route",
        MapSnapshot = MockSimulationMaps.CreateLinearMap(),
        Vehicles = new[] { Vehicle("AGV-A", "N1") },
        Tasks = new[] { SimulationTask("TASK-A", "N1", "N4", "AGV-A") },
        Options = DefaultOptions(1)
    };

    private static MockSimulationScenario AlternativeRouteScenario(MockSimulationOptions? options = null) => new()
    {
        ScenarioId = "alternative-route",
        Name = "Alternative route",
        MapSnapshot = MockSimulationMaps.CreateAlternativeRouteMap(),
        Vehicles = new[] { Vehicle("AGV-A", "S") },
        Tasks = new[] { SimulationTask("TASK-A", "S", "T", "AGV-A") },
        Options = options ?? DefaultOptions(1)
    };

    private static MockSimulationOptions DefaultOptions(int rollingWindowSize) => new()
    {
        RollingWindowSize = rollingWindowSize,
        WaitTimeout = TimeSpan.Zero,
        RetryInterval = TimeSpan.Zero,
        MaxRetryCount = 3
    };

    private static MockSimulationVehicle Vehicle(string vehicleId, string startNodeId) => new()
    {
        VehicleId = vehicleId,
        Brand = vehicleId,
        StartNodeId = startNodeId,
        BatteryLevel = 90
    };

    private static MockSimulationTask SimulationTask(
        string taskId,
        string sourceNodeId,
        string targetNodeId,
        string vehicleId) => new()
    {
        TaskId = taskId,
        SourceNodeId = sourceNodeId,
        TargetNodeId = targetNodeId,
        AssignedVehicleId = vehicleId
    };

    private static async Task<TrafficResourceState> EdgeStateAsync(
        MockTrafficControlService traffic,
        string edgeId)
    {
        var result = await traffic.GetResourceStatusAsync(
            new TrafficResourceKey { ResourceType = TrafficResourceType.Edge, ResourceId = edgeId },
            Context);
        return result.Data!.State;
    }

    private static async Task<TrafficResourceState> NodeStateAsync(
        MockTrafficControlService traffic,
        string nodeId)
    {
        var result = await traffic.GetResourceStatusAsync(
            new TrafficResourceKey { ResourceType = TrafficResourceType.Node, ResourceId = nodeId },
            Context);
        return result.Data!.State;
    }

    private static async Task<MockSimulationTickResult> RunUntilStateAsync(
        MockFleetSimulationEngine engine,
        string vehicleId,
        MockVehicleSimulationState state)
    {
        MockSimulationTickResult? last = null;
        for (var i = 0; i < 12; i++)
        {
            last = await engine.StepAsync();
            if (engine.GetVehicleState(vehicleId)?.State == state)
            {
                return last;
            }
        }

        return last ?? new MockSimulationTickResult
        {
            Succeeded = false,
            Message = "Simulation did not run."
        };
    }
}
