using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    internal static class MockData
    {
        public static IReadOnlyList<Vehicle> CreateVehicles()
        {
            return new[]
            {
                new Vehicle { VehicleId = "AGV-002", VehicleCode = "AGV-002", Name = "2号搬运车", Brand = "RGV-A", Model = "A100", AreaCode = "A" },
                new Vehicle { VehicleId = "AGV-003", VehicleCode = "AGV-003", Name = "3号搬运车", Brand = "RGV-A", Model = "A100", AreaCode = "A" },
                new Vehicle { VehicleId = "AGV-008", VehicleCode = "AGV-008", Name = "8号搬运车", Brand = "RGV-B", Model = "B200", AreaCode = "B" },
                new Vehicle { VehicleId = "AGV-010", VehicleCode = "AGV-010", Name = "10号搬运车", Brand = "RGV-B", Model = "B200", AreaCode = "A" },
                new Vehicle { VehicleId = "AGV-017", VehicleCode = "AGV-017", Name = "17号搬运车", Brand = "RGV-C", Model = "C300", AreaCode = "C" }
            };
        }

        public static IReadOnlyList<VehicleStatusSnapshot> CreateVehicleSnapshots(DateTime reportedAt)
        {
            return new[]
            {
                new VehicleStatusSnapshot { VehicleId = "AGV-002", Brand = "RGV-A", State = RobotState.Running, CurrentTaskId = "T-240102", Location = "A01-02", BatteryLevel = 18, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-003", Brand = "RGV-A", State = RobotState.Running, CurrentTaskId = "T-240106", Location = "A02-08", BatteryLevel = 65, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-008", Brand = "RGV-B", State = RobotState.Fault, CurrentTaskId = null, Location = "B01-05", BatteryLevel = 12, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-010", Brand = "RGV-B", State = RobotState.Running, CurrentTaskId = "T-240105", Location = "A03-01", BatteryLevel = 80, ReportedAt = reportedAt },
                new VehicleStatusSnapshot { VehicleId = "AGV-017", Brand = "RGV-C", State = RobotState.Idle, CurrentTaskId = null, Location = "Charge-03", BatteryLevel = 92, ReportedAt = reportedAt }
            };
        }

        public static IReadOnlyList<TaskOrder> CreateTasks(DateTime now)
        {
            return new[]
            {
                CreateTask("T-240101", "搬运", TaskPriority.High, TaskState.Running, "AGV-001", "A1", "B2", now, now.AddMinutes(30)),
                CreateTask("T-240102", "搬运", TaskPriority.Normal, TaskState.Running, "AGV-002", "A2", "B3", now, now.AddMinutes(30)),
                CreateTask("T-240103", "充电", TaskPriority.High, TaskState.Pending, "AGV-005", "C1", "Charge-1", now, null),
                CreateTask("T-240104", "巡检", TaskPriority.Low, TaskState.Completed, "AGV-008", "D1", "D2", now, now),
                CreateTask("T-240105", "补货", TaskPriority.Normal, TaskState.Failed, "AGV-010", "W1", "W2", now, null),
                CreateTask("T-240106", "搬运", TaskPriority.High, TaskState.Running, "AGV-011", "A3", "B1", now, now.AddMinutes(30)),
                CreateTask("T-240107", "补货", TaskPriority.Normal, TaskState.Cancelled, "", "E1", "E2", now, null),
                CreateTask("T-240108", "巡检", TaskPriority.Low, TaskState.Completed, "AGV-015", "F1", "F3", now, now),
                CreateTask("T-240109", "搬运", TaskPriority.Normal, TaskState.Pending, "", "A1", "B4", now, null),
                CreateTask("T-240110", "充电", TaskPriority.High, TaskState.Running, "AGV-020", "G1", "Charge-2", now, now.AddMinutes(30))
            };
        }

        public static IReadOnlyList<ChargeStation> CreateChargeStations()
        {
            return new[]
            {
                CreateStation("C-01", "1号充电桩", ChargeStationState.Charging, "AGV-002", 30, 0),
                CreateStation("C-02", "2号充电桩", ChargeStationState.Occupied, "AGV-015", 10, 1),
                CreateStation("C-03", "3号充电桩", ChargeStationState.Available, null, 20, 0),
                CreateStation("C-04", "4号充电桩", ChargeStationState.Fault, null, 0, 0),
                CreateStation("C-05", "5号充电桩", ChargeStationState.Offline, null, 0, 0)
            };
        }

        public static IReadOnlyList<AlarmEvent> CreateAlarms(DateTime now)
        {
            return new[]
            {
                new AlarmEvent { AlarmId = "ALM-001", AlarmCode = "ERR-LIDAR", Name = "激光避障异常", Description = "AGV-008 激光避障异常", Severity = AlarmSeverity.Critical, SourceType = "Vehicle", SourceId = "AGV-008", VehicleId = "AGV-008", OccurredAt = now.AddMinutes(-2) },
                new AlarmEvent { AlarmId = "ALM-002", AlarmCode = "WARN-BATTERY", Name = "电量低于20%", Description = "AGV-014 电量低于20%", Severity = AlarmSeverity.Warning, SourceType = "Vehicle", SourceId = "AGV-014", VehicleId = "AGV-014", OccurredAt = now.AddMinutes(-3) },
                new AlarmEvent { AlarmId = "ALM-003", AlarmCode = "WARN-LATENCY", Name = "通讯延迟过高", Description = "AGV-002 通讯延迟过高", Severity = AlarmSeverity.Warning, SourceType = "Vehicle", SourceId = "AGV-002", VehicleId = "AGV-002", OccurredAt = now.AddMinutes(-5) }
            };
        }

        public static IReadOnlyList<MapNode> CreateMapNodes()
        {
            return new[]
            {
                new MapNode { NodeId = "P1", MapId = "MAIN", NodeCode = "P1", Name = "P1取货点", NodeType = MapNodeType.Pickup, AreaCode = "A", Position = new MapPosition { X = 80, Y = 80 } },
                new MapNode { NodeId = "P2", MapId = "MAIN", NodeCode = "P2", Name = "P2取货点", NodeType = MapNodeType.Pickup, AreaCode = "A", Position = new MapPosition { X = 80, Y = 300 } },
                new MapNode { NodeId = "X1", MapId = "MAIN", NodeCode = "X1", Name = "中央路口", NodeType = MapNodeType.Intersection, AreaCode = "A", Position = new MapPosition { X = 400, Y = 200 } },
                new MapNode { NodeId = "W1", MapId = "MAIN", NodeCode = "W1", Name = "等待点", NodeType = MapNodeType.Waiting, AreaCode = "A", Position = new MapPosition { X = 400, Y = 420 } },
                new MapNode { NodeId = "D1", MapId = "MAIN", NodeCode = "D1", Name = "D1放货点", NodeType = MapNodeType.Dropoff, AreaCode = "B", Position = new MapPosition { X = 720, Y = 80 } },
                new MapNode { NodeId = "D2", MapId = "MAIN", NodeCode = "D2", Name = "D2放货点", NodeType = MapNodeType.Dropoff, AreaCode = "B", Position = new MapPosition { X = 720, Y = 300 } },
                new MapNode { NodeId = "Charge-1", MapId = "MAIN", NodeCode = "Charge-1", Name = "1号充电位", NodeType = MapNodeType.Charge, AreaCode = "C", Position = new MapPosition { X = 720, Y = 480 } }
            };
        }

        public static IReadOnlyList<MapEdge> CreateMapEdges()
        {
            return new[]
            {
                // 取货点 → 路口：双向
                new MapEdge { EdgeId = "E-P1-X1", MapId = "MAIN", FromNodeId = "P1", ToNodeId = "X1", Direction = EdgeDirection.Bidirectional, Length = 360, MaxSpeed = 1.5 },
                new MapEdge { EdgeId = "E-P2-X1", MapId = "MAIN", FromNodeId = "P2", ToNodeId = "X1", Direction = EdgeDirection.Bidirectional, Length = 340, MaxSpeed = 1.5 },
                // 路口 → 放货点 D1：单向（仅去程）
                new MapEdge { EdgeId = "E-X1-D1", MapId = "MAIN", FromNodeId = "X1", ToNodeId = "D1", Direction = EdgeDirection.ForwardOnly, Length = 360, MaxSpeed = 1.5 },
                // 路口 → 放货点 D2：双向
                new MapEdge { EdgeId = "E-X1-D2", MapId = "MAIN", FromNodeId = "X1", ToNodeId = "D2", Direction = EdgeDirection.Bidirectional, Length = 340, MaxSpeed = 1.2 },
                // 路口 → 等待点：双向
                new MapEdge { EdgeId = "E-X1-W1", MapId = "MAIN", FromNodeId = "X1", ToNodeId = "W1", Direction = EdgeDirection.Bidirectional, Length = 220, MaxSpeed = 1.0 },
                // 放货点 D2 → 充电位：双向
                new MapEdge { EdgeId = "E-D2-CH1", MapId = "MAIN", FromNodeId = "D2", ToNodeId = "Charge-1", Direction = EdgeDirection.Bidirectional, Length = 180, MaxSpeed = 1.0 },
                // 等待点 → 充电位：临时锁定（演示锁定状态，规划时绕开）
                new MapEdge { EdgeId = "E-W1-CH1", MapId = "MAIN", FromNodeId = "W1", ToNodeId = "Charge-1", Direction = EdgeDirection.Bidirectional, Length = 320, MaxSpeed = 1.0 },
                // 取货点 P2 → 等待点：禁用（演示禁用状态）
                new MapEdge { EdgeId = "E-P2-W1", MapId = "MAIN", FromNodeId = "P2", ToNodeId = "W1", Direction = EdgeDirection.Bidirectional, Length = 420, MaxSpeed = 1.0, IsEnabled = false }
            };
        }

        public static IReadOnlyList<MapLocationAlias> CreateMapAliases()
        {
            return new[]
            {
                new MapLocationAlias { AliasId = "AL-001", MapId = "MAIN", NodeId = "P1", AliasType = "Vendor", AliasValue = "STATION-01", Brand = null, IsEnabled = true, Remark = "全局取货位别名" },
                new MapLocationAlias { AliasId = "AL-002", MapId = "MAIN", NodeId = "D1", AliasType = "Vendor", AliasValue = "DOCK-A", Brand = "RGV-A", IsEnabled = true, Remark = "RGV-A 厂商放货位映射" }
            };
        }

        public static IReadOnlyList<SignalPoint> CreateSignals(DateTime now)
        {
            return new[]
            {
                CreateSignal("SIG_LIFT_TOP", "提升机上限位", SignalPointType.Elevator, SignalState.Normal, "1", "LIFT-001", now),
                CreateSignal("SIG_DOOR_01", "安全门01", SignalPointType.Door, SignalState.Active, "0", "DOOR-01", now),
                CreateSignal("SIG_OPTIC_01", "光电开关01", SignalPointType.Sensor, SignalState.Normal, "1", "SENSOR-01", now),
                CreateSignal("SIG_HB_05", "AGV05心跳检测", SignalPointType.Custom, SignalState.Offline, "Timeout", "AGV-005", now),
                CreateSignal("SIG_ESTOP_A", "A区急停按钮", SignalPointType.Button, SignalState.Abnormal, "1", "PANEL-A", now),
                CreateSignal("SIG_WEIGHT_02", "称重传感器", SignalPointType.Sensor, SignalState.Normal, "500kg", "SCALE-02", now),
                CreateSignal("SIG_PLC_RDY", "输送线就绪", SignalPointType.Conveyor, SignalState.Normal, "1", "CONV-03", now)
            };
        }

        public static IReadOnlyList<SignalLogEntry> CreateSignalLogs(DateTime now)
        {
            var time = now.ToString("HH:mm:ss.fff");
            return new[]
            {
                CreateSignalLog(time, "提升机上限位", "SIG_LIFT_TOP", "设备信号", "正常 -> 异常", "1", "LIFT-001", "24ms", "成功"),
                CreateSignalLog(time, "安全门01", "SIG_DOOR_01", "安全信号", "正常 -> 激活", "0", "DOOR-01", "12ms", "成功"),
                CreateSignalLog(time, "光电开关01", "SIG_OPTIC_01", "传感器", "激活 -> 正常", "1", "SENSOR-01", "32ms", "成功"),
                CreateSignalLog(time, "AGV05心跳检测", "SIG_HB_05", "虚拟信号", "正常 -> 离线", "Timeout", "AGV-005", "5000ms", "失败"),
                CreateSignalLog(time, "A区急停按钮", "SIG_ESTOP_A", "安全信号", "正常 -> 激活", "1", "PANEL-A", "8ms", "成功"),
                CreateSignalLog(time, "称重传感器", "SIG_WEIGHT_02", "设备信号", "异常 -> 正常", "500kg", "SCALE-02", "45ms", "成功")
            };
        }

        private static TaskOrder CreateTask(string id, string type, TaskPriority priority, TaskState state, string vehicleId, string source, string target, DateTime createdAt, DateTime? finishedAt)
        {
            return new TaskOrder
            {
                TaskId = id,
                TaskNo = id,
                TaskType = type,
                Priority = priority,
                State = state,
                AssignedVehicleId = string.IsNullOrWhiteSpace(vehicleId) ? null : vehicleId,
                SourceNodeId = source,
                TargetNodeId = target,
                CreatedAt = createdAt,
                FinishedAt = finishedAt
            };
        }

        private static ChargeStation CreateStation(string id, string name, ChargeStationState state, string? vehicleId, double power, double queueWeight)
        {
            return new ChargeStation
            {
                StationId = id,
                StationCode = id,
                Name = name,
                NodeId = id.Replace("C-", "Charge-"),
                State = state,
                BoundVehicleId = vehicleId,
                RatedPowerKw = power,
                QueueWeight = queueWeight
            };
        }

        private static SignalPoint CreateSignal(string id, string name, SignalPointType type, SignalState state, string value, string device, DateTime changedAt)
        {
            return new SignalPoint
            {
                SignalId = id,
                SignalCode = id,
                Name = name,
                PointType = type,
                State = state,
                CurrentValue = value,
                Remark = device,
                LastChangedAt = changedAt
            };
        }

        private static SignalLogEntry CreateSignalLog(string time, string name, string id, string type, string change, string value, string device, string responseTime, string result)
        {
            return new SignalLogEntry
            {
                LogTime = time,
                SignalName = name,
                SignalId = id,
                SignalType = type,
                StateChange = change,
                TriggerValue = value,
                Device = device,
                ResponseTime = responseTime,
                Result = result
            };
        }
    }
}
