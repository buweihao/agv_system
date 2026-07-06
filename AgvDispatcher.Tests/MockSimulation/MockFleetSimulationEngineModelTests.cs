using AgvDispatcher.Infrastructure.Mock.Simulation;
using Xunit;

namespace AgvDispatcher.Tests.MockSimulation;

public sealed class MockFleetSimulationEngineModelTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCreateVehicleRuntimeStates()
    {
        var engine = new MockFleetSimulationEngine();
        var scenario = CreateScenario();

        var result = await engine.InitializeAsync(scenario);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Tick);
        Assert.Equal(2, result.VehicleStates.Count);
        Assert.All(result.Events, item => Assert.Equal(MockSimulationEventType.VehicleInitialized, item.EventType));

        var vehicleA = engine.GetVehicleState("AGV-A");
        Assert.NotNull(vehicleA);
        Assert.Equal("N1", vehicleA!.CurrentNodeId);
        Assert.Equal("TASK-A", vehicleA.CurrentTaskId);
        Assert.Equal("N3", vehicleA.TargetNodeId);
        Assert.Equal(MockVehicleSimulationState.Idle, vehicleA.State);
    }

    [Fact]
    public async Task GetVehicleStates_ShouldReturnDefensiveCopies()
    {
        var engine = new MockFleetSimulationEngine();
        await engine.InitializeAsync(CreateScenario());

        var first = engine.GetVehicleState("AGV-A");
        var second = engine.GetVehicleState("AGV-A");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal(first!.VehicleId, second!.VehicleId);
    }

    [Fact]
    public async Task StepAsync_PhaseOne_ShouldOnlyAdvanceTickAndNotMoveVehicles()
    {
        var engine = new MockFleetSimulationEngine();
        await engine.InitializeAsync(CreateScenario());

        var result = await engine.StepAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Tick);
        Assert.Single(result.Events);
        Assert.Equal(MockSimulationEventType.NoOp, result.Events[0].EventType);
        Assert.Equal("N1", engine.GetVehicleState("AGV-A")!.CurrentNodeId);
        Assert.Equal("N2", engine.GetVehicleState("AGV-B")!.CurrentNodeId);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRejectTaskAssignedToUnknownVehicle()
    {
        var engine = new MockFleetSimulationEngine();
        var scenario = new MockSimulationScenario
        {
            ScenarioId = "invalid",
            Vehicles = new[]
            {
                new MockSimulationVehicle { VehicleId = "AGV-A", StartNodeId = "N1" }
            },
            Tasks = new[]
            {
                new MockSimulationTask
                {
                    TaskId = "TASK-A",
                    SourceNodeId = "N1",
                    TargetNodeId = "N3",
                    AssignedVehicleId = "AGV-MISSING"
                }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => engine.InitializeAsync(scenario));
    }

    private static MockSimulationScenario CreateScenario() => new()
    {
        ScenarioId = "phase-1-1",
        Name = "Phase 1.1 model initialization",
        Vehicles = new[]
        {
            new MockSimulationVehicle
            {
                VehicleId = "AGV-A",
                Brand = "Mock-A",
                StartNodeId = "N1",
                BatteryLevel = 90
            },
            new MockSimulationVehicle
            {
                VehicleId = "AGV-B",
                Brand = "Mock-B",
                StartNodeId = "N2",
                BatteryLevel = 80
            }
        },
        Tasks = new[]
        {
            new MockSimulationTask
            {
                TaskId = "TASK-A",
                SourceNodeId = "N1",
                TargetNodeId = "N3",
                AssignedVehicleId = "AGV-A"
            },
            new MockSimulationTask
            {
                TaskId = "TASK-B",
                SourceNodeId = "N2",
                TargetNodeId = "N3",
                AssignedVehicleId = "AGV-B"
            }
        },
        Options = new MockSimulationOptions
        {
            RollingWindowSize = 2,
            WaitTimeout = TimeSpan.FromSeconds(5),
            RetryInterval = TimeSpan.FromSeconds(1),
            MaxRetryCount = 2
        }
    };
}
