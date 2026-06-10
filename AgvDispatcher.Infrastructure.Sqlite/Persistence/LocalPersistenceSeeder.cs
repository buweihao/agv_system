using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Persistence
{
    internal static class LocalPersistenceSeeder
    {
        public static void EnsureSeedData(AgvDispatcherDbContext db)
        {
            db.Database.EnsureCreated();

            if (!db.Vehicles.Any())
            {
                db.Vehicles.AddRange(CreateVehicles());
            }

            if (!db.ChargeStations.Any())
            {
                db.ChargeStations.AddRange(CreateChargeStations());
            }

            if (!db.MapNodes.Any())
            {
                db.MapNodes.AddRange(CreateMapNodes());
            }

            if (!db.MapEdges.Any())
            {
                db.MapEdges.AddRange(CreateMapEdges());
            }

            if (!db.TaskTemplates.Any())
            {
                db.TaskTemplates.AddRange(CreateTaskTemplates());
            }

            if (!db.SystemParameters.Any())
            {
                db.SystemParameters.AddRange(CreateSystemParameters());
            }

            if (!db.Alarms.Any())
            {
                db.Alarms.AddRange(CreateAlarms(DateTime.Now));
            }

            if (!db.OperationLogs.Any())
            {
                db.OperationLogs.AddRange(CreateOperationLogs(DateTime.Now));
            }

            db.SaveChanges();
        }

        private static IReadOnlyList<Vehicle> CreateVehicles()
        {
            return new[]
            {
                new Vehicle { VehicleId = "AGV-002", VehicleCode = "AGV-002", Name = "AGV 002", Brand = "RGV-A", Model = "A100", AreaCode = "A", MaxSpeed = 1.5, RatedLoad = 500 },
                new Vehicle { VehicleId = "AGV-003", VehicleCode = "AGV-003", Name = "AGV 003", Brand = "RGV-A", Model = "A100", AreaCode = "A", MaxSpeed = 1.5, RatedLoad = 500 },
                new Vehicle { VehicleId = "AGV-008", VehicleCode = "AGV-008", Name = "AGV 008", Brand = "RGV-B", Model = "B200", AreaCode = "B", MaxSpeed = 1.2, RatedLoad = 800 },
                new Vehicle { VehicleId = "AGV-010", VehicleCode = "AGV-010", Name = "AGV 010", Brand = "RGV-B", Model = "B200", AreaCode = "A", MaxSpeed = 1.2, RatedLoad = 800 },
                new Vehicle { VehicleId = "AGV-017", VehicleCode = "AGV-017", Name = "AGV 017", Brand = "RGV-C", Model = "C300", AreaCode = "C", MaxSpeed = 1.0, RatedLoad = 1000 }
            };
        }

        private static IReadOnlyList<ChargeStation> CreateChargeStations()
        {
            return new[]
            {
                CreateStation("C-01", "Charge Station 01", ChargeStationState.Charging, "AGV-002", 30, 0, 760, 500),
                CreateStation("C-02", "Charge Station 02", ChargeStationState.Occupied, "AGV-015", 10, 1, 820, 500),
                CreateStation("C-03", "Charge Station 03", ChargeStationState.Available, null, 20, 0, 880, 500),
                CreateStation("C-04", "Charge Station 04", ChargeStationState.Fault, null, 0, 0, 940, 500),
                CreateStation("C-05", "Charge Station 05", ChargeStationState.Offline, null, 0, 0, 1000, 500)
            };
        }

        private static IReadOnlyList<MapNode> CreateMapNodes()
        {
            return new[]
            {
                new MapNode { NodeId = "A1", MapId = "MAIN", NodeCode = "A1", Name = "Pickup A1", NodeType = MapNodeType.Pickup, AreaCode = "A", Position = new MapPosition { MapId = "MAIN", X = 60, Y = 60, NodeId = "A1", AreaCode = "A" } },
                new MapNode { NodeId = "A2", MapId = "MAIN", NodeCode = "A2", Name = "Pickup A2", NodeType = MapNodeType.Pickup, AreaCode = "A", Position = new MapPosition { MapId = "MAIN", X = 260, Y = 60, NodeId = "A2", AreaCode = "A" } },
                new MapNode { NodeId = "B2", MapId = "MAIN", NodeCode = "B2", Name = "Dropoff B2", NodeType = MapNodeType.Dropoff, AreaCode = "B", Position = new MapPosition { MapId = "MAIN", X = 460, Y = 280, NodeId = "B2", AreaCode = "B" } },
                new MapNode { NodeId = "B3", MapId = "MAIN", NodeCode = "B3", Name = "Dropoff B3", NodeType = MapNodeType.Dropoff, AreaCode = "B", Position = new MapPosition { MapId = "MAIN", X = 680, Y = 280, NodeId = "B3", AreaCode = "B" } },
                new MapNode { NodeId = "Charge-1", MapId = "MAIN", NodeCode = "Charge-1", Name = "Charge Node 1", NodeType = MapNodeType.Charge, AreaCode = "C", Position = new MapPosition { MapId = "MAIN", X = 760, Y = 500, NodeId = "Charge-1", AreaCode = "C" } }
            };
        }

        private static IReadOnlyList<MapEdge> CreateMapEdges()
        {
            return new[]
            {
                new MapEdge { EdgeId = "E-A1-A2", MapId = "MAIN", FromNodeId = "A1", ToNodeId = "A2", Length = 200, MaxSpeed = 1.5 },
                new MapEdge { EdgeId = "E-A2-B2", MapId = "MAIN", FromNodeId = "A2", ToNodeId = "B2", Length = 280, MaxSpeed = 1.5 },
                new MapEdge { EdgeId = "E-B2-B3", MapId = "MAIN", FromNodeId = "B2", ToNodeId = "B3", Length = 220, MaxSpeed = 1.2 },
                new MapEdge { EdgeId = "E-B3-CH1", MapId = "MAIN", FromNodeId = "B3", ToNodeId = "Charge-1", Length = 260, MaxSpeed = 1.0 }
            };
        }

        private static IReadOnlyList<TaskTemplateConfig> CreateTaskTemplates()
        {
            return new[]
            {
                new TaskTemplateConfig { TemplateName = "Material Transfer", TemplateType = "Transfer", Scene = "Workshop Logistics", Priority = "High", Description = "Move materials between production areas.", IsEnabled = true, UpdateTime = "2024-01-12 10:20" },
                new TaskTemplateConfig { TemplateName = "Finished Goods Inbound", TemplateType = "Transfer", Scene = "Warehouse", Priority = "Normal", Description = "Move packed goods into storage.", IsEnabled = true, UpdateTime = "2024-01-11 15:30" },
                new TaskTemplateConfig { TemplateName = "Night Patrol", TemplateType = "Patrol", Scene = "Security", Priority = "Low", Description = "Scheduled night patrol route.", IsEnabled = false, UpdateTime = "2024-01-10 09:15" },
                new TaskTemplateConfig { TemplateName = "Low Battery Recharge", TemplateType = "Charge", Scene = "System", Priority = "High", Description = "Return to charge when battery is low.", IsEnabled = true, UpdateTime = "2024-01-05 16:20" }
            };
        }

        private static IReadOnlyList<ParameterConfig> CreateSystemParameters()
        {
            return new[]
            {
                new ParameterConfig { ParamKey = "MAX_WAIT_TIME", ParamName = "Max Wait Time", ParamValue = "300", DataType = "Int(seconds)", Description = "Maximum allowed waiting time at task nodes." },
                new ParameterConfig { ParamKey = "DEFAULT_SPEED", ParamName = "Default Speed", ParamValue = "1.2", DataType = "Float(m/s)", Description = "Default speed for created tasks." },
                new ParameterConfig { ParamKey = "RETRY_COUNT", ParamName = "Retry Count", ParamValue = "3", DataType = "Int", Description = "Automatic retry count after dispatch failures." },
                new ParameterConfig { ParamKey = "LOG_RETENTION_DAYS", ParamName = "Log Retention Days", ParamValue = "90", DataType = "Int(days)", Description = "Local operation log retention window." }
            };
        }

        private static IReadOnlyList<AlarmEvent> CreateAlarms(DateTime now)
        {
            return new[]
            {
                new AlarmEvent { AlarmId = "ALM-001", AlarmCode = "ERR-LIDAR", Name = "Lidar abnormal", Description = "AGV-008 lidar abnormal.", Severity = AlarmSeverity.Critical, SourceType = "Vehicle", SourceId = "AGV-008", VehicleId = "AGV-008", OccurredAt = now.AddMinutes(-2) },
                new AlarmEvent { AlarmId = "ALM-002", AlarmCode = "WARN-BATTERY", Name = "Low battery", Description = "AGV battery is lower than 20%.", Severity = AlarmSeverity.Warning, SourceType = "Vehicle", SourceId = "AGV-014", VehicleId = "AGV-014", OccurredAt = now.AddMinutes(-3) },
                new AlarmEvent { AlarmId = "ALM-003", AlarmCode = "WARN-LATENCY", Name = "High latency", Description = "Vehicle communication latency is high.", Severity = AlarmSeverity.Warning, SourceType = "Vehicle", SourceId = "AGV-002", VehicleId = "AGV-002", OccurredAt = now.AddMinutes(-5) }
            };
        }

        private static IReadOnlyList<OperationLog> CreateOperationLogs(DateTime now)
        {
            return new[]
            {
                new OperationLog { LogId = "LOG-001", Category = "System", Action = "Startup", Message = "Local persistence initialized.", Operator = "System", OccurredAt = now.AddMinutes(-10) },
                new OperationLog { LogId = "LOG-002", Category = "Vehicle", Action = "Online", Message = "AGV-002 status received.", VehicleId = "AGV-002", OccurredAt = now.AddMinutes(-9) },
                new OperationLog { LogId = "LOG-003", Category = "Alarm", Action = "Raised", Message = "Initial alarm record created.", VehicleId = "AGV-008", OccurredAt = now.AddMinutes(-2) }
            };
        }

        private static ChargeStation CreateStation(string id, string name, ChargeStationState state, string? vehicleId, double power, double queueWeight, double x, double y)
        {
            return new ChargeStation
            {
                StationId = id,
                StationCode = id,
                Name = name,
                AreaCode = "C",
                NodeId = id.Replace("C-", "Charge-"),
                State = state,
                BoundVehicleId = vehicleId,
                RatedPowerKw = power,
                QueueWeight = queueWeight,
                Position = new MapPosition { MapId = "MAIN", X = x, Y = y, NodeId = id.Replace("C-", "Charge-"), AreaCode = "C" }
            };
        }
    }
}
