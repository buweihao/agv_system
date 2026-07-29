using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.DebugDashboard.Services;

public interface IMockScenarioController
{
    event EventHandler? ScenarioChanged;

    IReadOnlyList<MockScenarioOption> GetScenarios();

    MockScenarioState CurrentState { get; }

    Task<MockScenarioOperationResult> InitializeScenarioAsync(
        string scenarioKey,
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> RunSameTargetFlowAsync(
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> RunNarrowAisleFlowAsync(
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> StartVehicleAsync(
        MockScenarioVehicleSlot slot,
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> StartAutomaticVehicleAsync(
        MockScenarioVehicleSlot slot,
        CancellationToken cancellationToken = default);

    MockScenarioOperationResult StopAutomaticVehicle(string vehicleId);

    void StopAllAutomaticVehicles();

    bool IsAutomaticRunning(string vehicleId);

    Task<MockScenarioOperationResult> ArriveNextNodeAsync(
        string vehicleId,
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> MoveVehicleToNextNodeAsync(
        string vehicleId,
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> DepartVehicleAAsync(
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> RetryWaitingTaskAsync(
        string vehicleId,
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> BlockScenarioResourceAsync(
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> UnblockScenarioResourceAsync(
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> ReleaseCurrentOccupancyAsync(
        string vehicleId,
        CancellationToken cancellationToken = default);

    Task<MockScenarioOperationResult> ResetScenarioAsync(
        CancellationToken cancellationToken = default);
}

public enum MockScenarioVehicleSlot
{
    VehicleA,
    VehicleB
}

public sealed class MockScenarioOption
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}

public sealed class MockScenarioState
{
    public string ScenarioKey { get; init; } = string.Empty;

    public string ScenarioName { get; init; } = string.Empty;

    public string VehicleAId { get; init; } = string.Empty;

    public string VehicleBId { get; init; } = string.Empty;

    public string? TaskAId { get; init; }

    public string? TaskBId { get; init; }

    public string SourceA { get; init; } = string.Empty;

    public string SourceB { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public string MergeNode { get; init; } = string.Empty;

    public string VehicleAExitNode { get; init; } = string.Empty;

    public bool IsVehicleAHoldingTarget { get; init; }

    public bool IsFullFlowRunning { get; init; }

    public TrafficResourceKey? ManualBlockResource { get; init; }

    public bool IsManualBlockActive { get; init; }

    public bool IsVehicleAAutomaticRunning { get; init; }

    public bool IsVehicleBAutomaticRunning { get; init; }

    public IReadOnlyList<MockScenarioStepLog> Logs { get; init; } = Array.Empty<MockScenarioStepLog>();
}

public sealed class MockScenarioStepLog
{
    public DateTimeOffset Time { get; init; } = DateTimeOffset.Now;

    public string Step { get; init; } = string.Empty;

    public string Result { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;
}

public sealed class MockScenarioOperationResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static MockScenarioOperationResult Ok(string message) => new()
    {
        Success = true,
        Message = message
    };

    public static MockScenarioOperationResult Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}
