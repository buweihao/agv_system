using AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public sealed class MockSimulationScenario
    {
        public string ScenarioId { get; init; } = Guid.NewGuid().ToString("N");

        public string Name { get; init; } = string.Empty;

        public MapSnapshotDto? MapSnapshot { get; init; }

        public IReadOnlyList<MockSimulationVehicle> Vehicles { get; init; } =
            Array.Empty<MockSimulationVehicle>();

        public IReadOnlyList<MockSimulationTask> Tasks { get; init; } =
            Array.Empty<MockSimulationTask>();

        public MockSimulationOptions Options { get; init; } = new();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ScenarioId))
            {
                throw new ArgumentException("ScenarioId cannot be empty.", nameof(ScenarioId));
            }

            if (Vehicles.Count == 0)
            {
                throw new ArgumentException("At least one simulation vehicle is required.", nameof(Vehicles));
            }

            var duplicateVehicle = Vehicles
                .GroupBy(vehicle => vehicle.VehicleId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateVehicle is not null)
            {
                throw new ArgumentException($"Duplicate vehicle id '{duplicateVehicle.Key}'.", nameof(Vehicles));
            }

            var vehicleIds = Vehicles
                .Select(vehicle => vehicle.VehicleId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var task in Tasks.Where(task => !string.IsNullOrWhiteSpace(task.AssignedVehicleId)))
            {
                if (!vehicleIds.Contains(task.AssignedVehicleId!))
                {
                    throw new ArgumentException(
                        $"Task '{task.TaskId}' is assigned to unknown vehicle '{task.AssignedVehicleId}'.",
                        nameof(Tasks));
                }
            }

            Options.Validate();
        }

        public MockSimulationScenario Clone() => new()
        {
            ScenarioId = ScenarioId,
            Name = Name,
            MapSnapshot = MapSnapshot,
            Vehicles = Vehicles.Select(vehicle => vehicle.Clone()).ToArray(),
            Tasks = Tasks.Select(task => task.Clone()).ToArray(),
            Options = Options.Clone()
        };
    }
}
