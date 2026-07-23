using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using AgvDispatcher.DebugDashboard.Services;
using Xunit;

namespace AgvDispatcher.Tests.DebugDashboard;

public sealed class VehicleMotionSimulationServiceTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 5)]
    [InlineData(1, 10)]
    public void Interpolate_ReturnsExpectedPoint(double progress, double expectedX)
    {
        var start = Position(0, 0);
        var end = Position(10, 0);

        var result = VehicleMotionMath.Interpolate(start, end, progress);

        Assert.Equal(expectedX, result.X, 6);
        Assert.Equal(0, result.Y, 6);
    }

    [Theory]
    [InlineData(10, 0, 0)]
    [InlineData(0, 10, 90)]
    [InlineData(-10, 0, 180)]
    [InlineData(0, -10, 270)]
    public void CalculateHeading_UsesMapDegrees(double x, double y, double expected)
    {
        Assert.Equal(expected, VehicleMotionMath.CalculateHeading(Position(0, 0), Position(x, y)), 6);
    }

    [Fact]
    public async Task ManualTick_UsesDeltaTimeAndReachesTarget()
    {
        var simulation = new FakeSimulation();
        simulation.Add("A", Position(0, 0));
        var scheduler = new ManualScheduler();
        using var service = new VehicleMotionSimulationService(simulation, scheduler);

        var motion = service.MoveToAsync("A", Position(10, 0), speed: 10);
        scheduler.Advance(TimeSpan.FromSeconds(0.5));
        Assert.Equal(5, simulation.GetVehicle("A")!.Position.X, 6);
        scheduler.Advance(TimeSpan.FromSeconds(0.5));

        Assert.True((await motion).ReachedTarget);
        Assert.Equal(10, simulation.GetVehicle("A")!.Position.X, 6);
    }

    [Fact]
    public void DuplicateTarget_ReturnsSameMotionTask()
    {
        var simulation = new FakeSimulation();
        simulation.Add("A", Position(0, 0));
        using var service = new VehicleMotionSimulationService(simulation, new ManualScheduler());

        var first = service.MoveToAsync("A", Position(10, 0));
        var second = service.MoveToAsync("A", Position(10, 0));

        Assert.Same(first, second);
    }

    [Fact]
    public void TwoVehicles_AdvanceIndependently()
    {
        var simulation = new FakeSimulation();
        simulation.Add("A", Position(0, 0));
        simulation.Add("B", Position(0, 0));
        var scheduler = new ManualScheduler();
        using var service = new VehicleMotionSimulationService(simulation, scheduler);

        _ = service.MoveToAsync("A", Position(10, 0), speed: 10);
        _ = service.MoveToAsync("B", Position(0, 20), speed: 5);
        scheduler.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(10, simulation.GetVehicle("A")!.Position.X, 6);
        Assert.Equal(5, simulation.GetVehicle("B")!.Position.Y, 6);
    }

    [Fact]
    public async Task FaultAndCancellation_StopMotion()
    {
        var simulation = new FakeSimulation();
        simulation.Add("A", Position(0, 0));
        simulation.Add("B", Position(0, 0));
        var scheduler = new ManualScheduler();
        using var service = new VehicleMotionSimulationService(simulation, scheduler);
        using var cancellation = new CancellationTokenSource();

        var faulted = service.MoveToAsync("A", Position(10, 0));
        var cancelled = service.MoveToAsync("B", Position(10, 0), cancellationToken: cancellation.Token);
        simulation.SetState("A", RobotState.Fault);
        cancellation.Cancel();
        scheduler.Advance(TimeSpan.FromSeconds(1));

        Assert.False((await faulted).ReachedTarget);
        Assert.False((await cancelled).ReachedTarget);
        Assert.False(service.IsMoving("A"));
        Assert.False(service.IsMoving("B"));
    }

    private static MapPosition Position(double x, double y) => new()
    {
        MapId = "MAIN",
        X = x,
        Y = y
    };

    private sealed class ManualScheduler : IVehicleMotionScheduler
    {
        public event EventHandler<TimeSpan>? Tick;
        public void Start() { }
        public void Stop() { }
        public void Advance(TimeSpan elapsed) => Tick?.Invoke(this, elapsed);
        public void Dispose() { }
    }

    private sealed class FakeSimulation : IMockSimulationService
    {
        private readonly Dictionary<string, VehicleStatusSnapshot> _vehicles = new(StringComparer.OrdinalIgnoreCase);
        public event EventHandler? VehiclesChanged;
        public IReadOnlyList<VehicleStatusSnapshot> GetVehicles() => _vehicles.Values.Select(item => item.Clone()).ToArray();
        public VehicleStatusSnapshot? GetVehicle(string vehicleId) => _vehicles.GetValueOrDefault(vehicleId)?.Clone();

        public void Add(string vehicleId, MapPosition position) => _vehicles[vehicleId] = new VehicleStatusSnapshot
        {
            VehicleId = vehicleId,
            Position = position.Clone(),
            Location = position.NodeId ?? string.Empty,
            State = RobotState.Running,
            IsOnline = true,
            BatteryLevel = 100
        };

        public void SetState(string vehicleId, RobotState state) => _vehicles[vehicleId].State = state;

        public void UpsertVehicle(MockVehicleUpdate update)
        {
            var snapshot = _vehicles[update.VehicleId];
            if (update.Position is not null) snapshot.Position = update.Position.Clone();
            if (update.State.HasValue) snapshot.State = update.State.Value;
            snapshot.ReportedAt = DateTime.Now;
            VehiclesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
