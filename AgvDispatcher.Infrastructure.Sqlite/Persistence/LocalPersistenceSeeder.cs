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
                new Vehicle { VehicleId = "AGV-002", VehicleCode = "AGV-002", Name = "AGV 002", Brand = "RGV-A", Model = "A100", AreaCode = "A", MaxSpeed = 1.5, RatedLoad = 500, AdapterType = "MockBrandA", ProtocolType = "HTTP", Endpoint = "http://192.168.1.10:8000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Lift, MinDispatchBattery = 30.0 },
                new Vehicle { VehicleId = "AGV-003", VehicleCode = "AGV-003", Name = "AGV 003", Brand = "RGV-A", Model = "A100", AreaCode = "A", MaxSpeed = 1.5, RatedLoad = 500, AdapterType = "MockBrandA", ProtocolType = "HTTP", Endpoint = "http://192.168.1.11:8000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Lift, MinDispatchBattery = 30.0 },
                new Vehicle { VehicleId = "AGV-008", VehicleCode = "AGV-008", Name = "AGV 008", Brand = "RGV-B", Model = "B200", AreaCode = "B", MaxSpeed = 1.2, RatedLoad = 800, AdapterType = "MockBrandB", ProtocolType = "TCP", Endpoint = "192.168.1.20:5000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Tow, MinDispatchBattery = 40.0 },
                new Vehicle { VehicleId = "AGV-010", VehicleCode = "AGV-010", Name = "AGV 010", Brand = "RGV-B", Model = "B200", AreaCode = "A", MaxSpeed = 1.2, RatedLoad = 800, AdapterType = "MockBrandB", ProtocolType = "TCP", Endpoint = "192.168.1.21:5000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Tow, MinDispatchBattery = 40.0 },
                new Vehicle { VehicleId = "AGV-017", VehicleCode = "AGV-017", Name = "AGV 017", Brand = "RGV-C", Model = "C300", AreaCode = "C", MaxSpeed = 1.0, RatedLoad = 1000, AdapterType = "RealTcp", ProtocolType = "TCP", Endpoint = "192.168.1.30:4000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Fork, MinDispatchBattery = 20.0 }
            };
        }

        private static IReadOnlyList<ChargeStation> CreateChargeStations()
        {
            return new[]
            {
                CreateStation("C-01", "Charge Station 01", ChargeStationState.Charging, "AGV-002", 30, 0, 760, 500, "RGV-A", "HTTP", "192.168.1.100", 80),
                CreateStation("C-02", "Charge Station 02", ChargeStationState.Occupied, "AGV-015", 10, 1, 820, 500, "RGV-B", "TCP", "192.168.1.101", 502),
                CreateStation("C-03", "Charge Station 03", ChargeStationState.Available, null, 20, 0, 880, 500, "RGV-A,RGV-B", "TCP", "192.168.1.102", 502),
                CreateStation("C-04", "Charge Station 04", ChargeStationState.Fault, null, 0, 0, 940, 500, "RGV-C", "MQTT", "192.168.1.103", 1883),
                CreateStation("C-05", "Charge Station 05", ChargeStationState.Offline, null, 0, 0, 1000, 500, "", "", "", 0)
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
            return new List<ParameterConfig>
            {
                new ParameterConfig { ParamKey = "MAX_WAIT_TIME", ParamName = "最大等待时间", ParamValue = "300", DataType = "Int(seconds)", Description = "任务节点最大允许等待时间。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "DEFAULT_SPEED", ParamName = "默认速度", ParamValue = "1.2", DataType = "Float(m/s)", Description = "创建任务时的默认速度。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "RETRY_COUNT", ParamName = "重试次数", ParamValue = "3", DataType = "Int", Description = "派发失败后的自动重试次数。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "LOG_RETENTION_DAYS", ParamName = "日志保留天数", ParamValue = "90", DataType = "Int(days)", Description = "本地操作日志保留窗口。", RequiresRestart = true },
                new ParameterConfig { ParamKey = "MIN_DISPATCH_BATTERY", ParamName = "最低派发电量", ParamValue = "20", DataType = "Float(%)", Description = "全局最低派发电量，低于此电量不派发任务。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "LOW_BATTERY_ALARM", ParamName = "低电量告警阈值", ParamValue = "15", DataType = "Float(%)", Description = "低于此电量触发告警。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "TASK_TIMEOUT_MINUTES", ParamName = "任务超时时间", ParamValue = "60", DataType = "Int(minutes)", Description = "任务执行超时时间。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "AUTO_DISPATCH_ENABLED", ParamName = "自动派发开关", ParamValue = "true", DataType = "Boolean", Description = "是否开启自动任务派发。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_BATTERY", ParamName = "电量评分权重", ParamValue = "25.0", DataType = "Float", Description = "调度时电量的得分权重 (0-100)。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_DISTANCE", ParamName = "距离评分权重", ParamValue = "35.0", DataType = "Float", Description = "调度时距离的得分权重 (0-100)。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_LOAD", ParamName = "载重评分权重", ParamValue = "10.0", DataType = "Float", Description = "调度时载重能力的得分权重 (0-100)。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_PRIORITY", ParamName = "优先级评分权重", ParamValue = "15.0", DataType = "Float", Description = "调度时优先级的得分权重 (0-100)。", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_AREA", ParamName = "区域评分权重", ParamValue = "15.0", DataType = "Float", Description = "调度时同区域的得分权重 (0-100)。", RequiresRestart = false }
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

        private static ChargeStation CreateStation(string id, string name, ChargeStationState state, string? vehicleId, double power, double queueWeight, double x, double y, string allowedBrands, string protocolType, string endpoint, int port)
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
                AllowedBrands = allowedBrands,
                ProtocolType = protocolType,
                Endpoint = endpoint,
                Port = port,
                OutputVoltage = 48,
                OutputCurrent = 30,
                ConnectorType = "GB/T",
                SupportedVehicleTypes = "AGV,RGV",
                SupportAutoCharge = true,
                MaxQueueCount = 2,
                IsExclusive = false,
                Position = new MapPosition { MapId = "MAIN", X = x, Y = y, NodeId = id.Replace("C-", "Charge-"), AreaCode = "C" }
            };
        }
    }
}
