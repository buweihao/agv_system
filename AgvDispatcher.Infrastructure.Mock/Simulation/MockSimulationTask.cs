using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public sealed class MockSimulationTask
    {
        public string TaskId { get; init; } = string.Empty;

        public string TaskType { get; init; } = "MockSimulation";

        public string SourceNodeId { get; init; } = string.Empty;

        public string TargetNodeId { get; init; } = string.Empty;

        public string? AssignedVehicleId { get; init; }

        public TaskPriority Priority { get; init; } = TaskPriority.Normal;

        public MockSimulationTask Clone() => new()
        {
            TaskId = TaskId,
            TaskType = TaskType,
            SourceNodeId = SourceNodeId,
            TargetNodeId = TargetNodeId,
            AssignedVehicleId = AssignedVehicleId,
            Priority = Priority
        };
    }
}
