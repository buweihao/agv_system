namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public interface IMockFleetSimulationEngine
    {
        MockSimulationScenario? CurrentScenario { get; }

        long CurrentTick { get; }

        IReadOnlyList<MockVehicleRuntimeState> GetVehicleStates();

        MockVehicleRuntimeState? GetVehicleState(string vehicleId);

        IReadOnlyList<MockSimulationEvent> GetRecentEvents(int maxCount = 100);

        Task<MockSimulationTickResult> InitializeAsync(
            MockSimulationScenario scenario,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> StartTaskAsync(
            string vehicleId,
            string taskId,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> StartAllAsync(CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> CancelTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> InjectFaultAsync(
            string vehicleId,
            MockFaultPolicy faultPolicy,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> RecoverVehicleAsync(
            string vehicleId,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> UpdateOptionsAsync(
            MockSimulationOptions options,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> SetMapEdgeEnabledAsync(
            string edgeId,
            bool enabled,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> BlockTrafficResourceAsync(
            AgvDispatcher.Core.Contracts.Traffic.Enums.TrafficResourceType resourceType,
            string resourceId,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> UnblockTrafficResourceAsync(
            AgvDispatcher.Core.Contracts.Traffic.Enums.TrafficResourceType resourceType,
            string resourceId,
            CancellationToken cancellationToken = default);

        Task<MockSimulationTickResult> StepAsync(CancellationToken cancellationToken = default);
    }
}
