using AgvDispatcher.Core.Contracts.Map;

namespace AgvDispatcher.Infrastructure.Mock.Simulation
{
    public static class MockSimulationScenarioCatalog
    {
        public static IReadOnlyList<MockSimulationScenarioDefinition> GetDefinitions() => new[]
        {
            new MockSimulationScenarioDefinition("SameTarget", "两车同终点"),
            new MockSimulationScenarioDefinition("NarrowAisle", "相向窄道"),
            new MockSimulationScenarioDefinition("Intersection", "路口占用"),
            new MockSimulationScenarioDefinition("AlternativeRoute", "可替代路线"),
            new MockSimulationScenarioDefinition("CurrentNodeReplan", "当前位置重规划"),
            new MockSimulationScenarioDefinition("Linear", "线性路线")
        };

        public static MockSimulationScenario Create(string scenarioKey)
        {
            return scenarioKey switch
            {
                "SameTarget" => CreateSameTargetScenario(),
                "NarrowAisle" => CreateNarrowAisleScenario(),
                "Intersection" => CreateIntersectionScenario(),
                "AlternativeRoute" => CreateAlternativeRouteScenario(),
                "CurrentNodeReplan" => CreateCurrentNodeReplanScenario(),
                "Linear" => CreateLinearScenario(),
                _ => CreateSameTargetScenario()
            };
        }

        private static MockSimulationScenario CreateSameTargetScenario() => new()
        {
            ScenarioId = "SameTarget",
            Name = "SameTarget",
            MapSnapshot = CreateSameTargetMap(),
            Vehicles = new[]
            {
                Vehicle("AGV-MOCK-A", "P1"),
                Vehicle("AGV-MOCK-B", "P2")
            },
            Tasks = new[]
            {
                Task("TASK-MOCK-A", "P1", "D", "AGV-MOCK-A"),
                Task("TASK-MOCK-B", "P2", "D", "AGV-MOCK-B")
            },
            Options = new MockSimulationOptions
            {
                RollingWindowSize = 2,
                WaitTimeout = TimeSpan.FromSeconds(10),
                RetryInterval = TimeSpan.FromSeconds(1),
                MaxRetryCount = 3,
                AutoStartTasks = false
            }
        };

        private static MockSimulationScenario CreateNarrowAisleScenario() => new()
        {
            ScenarioId = "NarrowAisle",
            Name = "NarrowAisle",
            MapSnapshot = CreateNarrowAisleMap(),
            Vehicles = new[]
            {
                Vehicle("AGV-MOCK-A", "L"),
                Vehicle("AGV-MOCK-B", "R")
            },
            Tasks = new[]
            {
                Task("TASK-MOCK-A", "L", "R", "AGV-MOCK-A"),
                Task("TASK-MOCK-B", "R", "L", "AGV-MOCK-B")
            },
            Options = new MockSimulationOptions
            {
                RollingWindowSize = 2,
                AutoStartTasks = false
            }
        };

        private static MockSimulationScenario CreateIntersectionScenario() => new()
        {
            ScenarioId = "Intersection",
            Name = "Intersection",
            MapSnapshot = CreateIntersectionMap(),
            Vehicles = new[]
            {
                Vehicle("AGV-MOCK-A", "A1"),
                Vehicle("AGV-MOCK-B", "B1")
            },
            Tasks = new[]
            {
                Task("TASK-MOCK-A", "A1", "A2", "AGV-MOCK-A"),
                Task("TASK-MOCK-B", "B1", "B2", "AGV-MOCK-B")
            },
            Options = new MockSimulationOptions
            {
                RollingWindowSize = 1,
                AutoStartTasks = false
            }
        };

        private static MockSimulationScenario CreateAlternativeRouteScenario() => new()
        {
            ScenarioId = "AlternativeRoute",
            Name = "AlternativeRoute",
            MapSnapshot = CreateAlternativeRouteMap(),
            Vehicles = new[]
            {
                Vehicle("AGV-MOCK-A", "S"),
                Vehicle("AGV-MOCK-B", "B")
            },
            Tasks = new[]
            {
                Task("TASK-MOCK-A", "S", "T", "AGV-MOCK-A"),
                Task("TASK-MOCK-B", "B", "T", "AGV-MOCK-B")
            },
            Options = new MockSimulationOptions
            {
                RollingWindowSize = 1,
                ReplanOnLockedResource = true,
                ReplanOnBlockedResource = true,
                AutoStartTasks = false
            }
        };

        private static MockSimulationScenario CreateCurrentNodeReplanScenario() => new()
        {
            ScenarioId = "CurrentNodeReplan",
            Name = "CurrentNodeReplan",
            MapSnapshot = CreateCurrentNodeReplanMap(),
            Vehicles = new[]
            {
                Vehicle("AGV-MOCK-A", "S"),
                Vehicle("AGV-MOCK-B", "P")
            },
            Tasks = new[]
            {
                Task("TASK-MOCK-A", "S", "T", "AGV-MOCK-A"),
                Task("TASK-MOCK-B", "P", "T", "AGV-MOCK-B")
            },
            Options = new MockSimulationOptions
            {
                RollingWindowSize = 1,
                ReplanOnBlockedResource = true,
                MaxReplanCount = 3,
                AutoStartTasks = false
            }
        };

        private static MockSimulationScenario CreateLinearScenario() => new()
        {
            ScenarioId = "Linear",
            Name = "Linear",
            MapSnapshot = CreateLinearMap(),
            Vehicles = new[] { Vehicle("AGV-MOCK-A", "N1") },
            Tasks = new[] { Task("TASK-MOCK-A", "N1", "N4", "AGV-MOCK-A") },
            Options = new MockSimulationOptions
            {
                RollingWindowSize = 2,
                AutoStartTasks = false
            }
        };

        private static MockSimulationVehicle Vehicle(string vehicleId, string startNodeId) => new()
        {
            VehicleId = vehicleId,
            Brand = "Mock",
            StartNodeId = startNodeId,
            BatteryLevel = 100,
            IsOnline = true
        };

        private static MockSimulationTask Task(
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

        private static MapSnapshotDto CreateSameTargetMap() => new()
        {
            MapId = "MAP-SAME-TARGET",
            MapName = "Same target",
            Version = "1.0",
            Nodes = new[] { Node("P1"), Node("P2"), Node("X"), Node("D") },
            Edges = new[]
            {
                Edge("E-P1-X", "P1", "X"),
                Edge("E-P2-X", "P2", "X"),
                Edge("E-X-D", "X", "D")
            }
        };

        private static MapSnapshotDto CreateNarrowAisleMap() => new()
        {
            MapId = "MAP-NARROW",
            MapName = "Narrow aisle",
            Version = "1.0",
            Nodes = new[] { Node("L"), Node("A"), Node("B"), Node("R") },
            Edges = new[]
            {
                Edge("E-L-A", "L", "A"),
                Edge("E-NARROW-A-B", "A", "B"),
                Edge("E-B-R", "B", "R"),
                Edge("E-R-B", "R", "B"),
                Edge("E-NARROW-B-A", "B", "A"),
                Edge("E-A-L", "A", "L")
            }
        };

        private static MapSnapshotDto CreateIntersectionMap() => new()
        {
            MapId = "MAP-INTERSECTION",
            MapName = "Intersection",
            Version = "1.0",
            Nodes = new[] { Node("A1"), Node("B1"), Node("X"), Node("A2"), Node("B2") },
            Edges = new[]
            {
                Edge("E-A1-X", "A1", "X"),
                Edge("E-X-A2", "X", "A2"),
                Edge("E-B1-X", "B1", "X"),
                Edge("E-X-B2", "X", "B2")
            }
        };

        private static MapSnapshotDto CreateAlternativeRouteMap() => new()
        {
            MapId = "MAP-ALTERNATIVE",
            MapName = "Alternative route",
            Version = "1.0",
            Nodes = new[] { Node("S"), Node("A"), Node("B"), Node("C"), Node("T") },
            Edges = new[]
            {
                Edge("E-S-A", "S", "A"),
                Edge("E-A-T", "A", "T"),
                Edge("E-S-B", "S", "B"),
                Edge("E-B-C", "B", "C"),
                Edge("E-C-T", "C", "T")
            }
        };

        private static MapSnapshotDto CreateCurrentNodeReplanMap() => new()
        {
            MapId = "MAP-CURRENT-NODE-REPLAN",
            MapName = "Current node replan",
            Version = "1.0",
            Nodes = new[] { Node("S"), Node("A"), Node("B"), Node("C"), Node("T"), Node("P") },
            Edges = new[]
            {
                Edge("E-S-A", "S", "A"),
                Edge("E-A-T", "A", "T"),
                Edge("E-A-B", "A", "B"),
                Edge("E-B-C", "B", "C"),
                Edge("E-C-T", "C", "T"),
                Edge("E-P-A", "P", "A")
            }
        };

        private static MapSnapshotDto CreateLinearMap() => new()
        {
            MapId = "MAP-LINEAR",
            MapName = "Linear",
            Version = "1.0",
            Nodes = new[] { Node("N1"), Node("N2"), Node("N3"), Node("N4") },
            Edges = new[]
            {
                Edge("E1", "N1", "N2"),
                Edge("E2", "N2", "N3"),
                Edge("E3", "N3", "N4")
            }
        };

        private static MapNodeDto Node(string nodeId) => new()
        {
            NodeId = nodeId,
            NodeCode = nodeId,
            NodeName = nodeId,
            NodeType = MapNodeType.Normal
        };

        private static MapEdgeDto Edge(string edgeId, string fromNodeId, string toNodeId) => new()
        {
            EdgeId = edgeId,
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            Distance = 1,
            Direction = MapEdgeDirection.OneWay,
            Enabled = true
        };
    }

    public sealed record MockSimulationScenarioDefinition(string Key, string Name);
}
