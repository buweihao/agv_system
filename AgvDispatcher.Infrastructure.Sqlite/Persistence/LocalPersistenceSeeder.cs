using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Persistence
{
    internal static class LocalPersistenceSeeder
    {
        private const string ReferenceSeedInitializedKey = "Map.ReferenceSeedInitialized";

        public static void EnsureSeedData(AgvDispatcherDbContext db)
        {
            db.Database.EnsureCreated();

            // EnsureCreated does not add newly introduced tables to an existing database.
            // Keep this idempotent bootstrap until the application adopts versioned migrations.
            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS TrafficResourceLocks (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ResourceType INTEGER NOT NULL, ResourceId TEXT NOT NULL, State INTEGER NOT NULL, OccupiedByAgvId TEXT NULL, ReservedByAgvId TEXT NULL, TaskId TEXT NULL, RouteReservationId TEXT NULL, TrafficReservationId TEXT NULL, LockMode INTEGER NULL, Reason TEXT NULL, ExpireAt TEXT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, ConcurrencyToken INTEGER NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS IX_TrafficResourceLocks_ResourceType_ResourceId ON TrafficResourceLocks(ResourceType, ResourceId);
CREATE TABLE IF NOT EXISTS RouteReservations (ReservationId TEXT NOT NULL PRIMARY KEY, TaskId TEXT NOT NULL, VehicleId TEXT NOT NULL, PlanId TEXT NOT NULL, MapId TEXT NOT NULL, MapVersion TEXT NOT NULL, RollingWindowSize INTEGER NOT NULL, ReservationPolicy INTEGER NOT NULL, State INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, ReleasedAt TEXT NULL, LastFailureReason TEXT NULL);
CREATE INDEX IF NOT EXISTS IX_RouteReservations_TaskId ON RouteReservations(TaskId);
CREATE INDEX IF NOT EXISTS IX_RouteReservations_VehicleId ON RouteReservations(VehicleId);
CREATE TABLE IF NOT EXISTS RouteReservationSegments (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ReservationId TEXT NOT NULL, Sequence INTEGER NOT NULL, FromNodeId TEXT NOT NULL, ToNodeId TEXT NOT NULL, EdgeId TEXT NOT NULL, Distance REAL NOT NULL, Cost REAL NOT NULL, IsReserved INTEGER NOT NULL, IsLocked INTEGER NOT NULL, IsReleased INTEGER NOT NULL, LockedAt TEXT NULL, ReleasedAt TEXT NULL, TrafficReservationIdsJson TEXT NOT NULL, ResourcesJson TEXT NOT NULL, FOREIGN KEY(ReservationId) REFERENCES RouteReservations(ReservationId) ON DELETE CASCADE);
CREATE UNIQUE INDEX IF NOT EXISTS IX_RouteReservationSegments_ReservationId_Sequence ON RouteReservationSegments(ReservationId, Sequence);
CREATE TABLE IF NOT EXISTS RouteReservationEvents (EventId TEXT NOT NULL PRIMARY KEY, ReservationId TEXT NOT NULL, TaskId TEXT NOT NULL, VehicleId TEXT NOT NULL, EventType TEXT NOT NULL, Message TEXT NULL, CreatedAt TEXT NOT NULL, SnapshotJson TEXT NULL);
CREATE INDEX IF NOT EXISTS IX_RouteReservationEvents_ReservationId ON RouteReservationEvents(ReservationId);");

            // Execute raw SQL to ensure MapLocationAliases exists for existing databases
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""MapLocationAliases"" (
                    ""AliasId"" TEXT NOT NULL CONSTRAINT ""PK_MapLocationAliases"" PRIMARY KEY,
                    ""MapId"" TEXT NOT NULL,
                    ""MapVersion"" TEXT NOT NULL DEFAULT 'v1',
                    ""NodeId"" TEXT NOT NULL,
                    ""AliasType"" TEXT NOT NULL,
                    ""AliasValue"" TEXT NOT NULL,
                    ""Brand"" TEXT NULL,
                    ""IsEnabled"" INTEGER NOT NULL,
                    ""Remark"" TEXT NULL
                );
                CREATE TABLE IF NOT EXISTS ""MapVersions"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_MapVersions"" PRIMARY KEY,
                    ""MapId"" TEXT NOT NULL,
                    ""MapVersion"" TEXT NOT NULL,
                    ""Name"" TEXT NOT NULL,
                    ""State"" INTEGER NOT NULL,
                    ""BaseMapId"" TEXT NULL,
                    ""BaseMapVersion"" TEXT NULL,
                    ""IsActive"" INTEGER NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NOT NULL,
                    ""PublishedAt"" TEXT NULL,
                    ""ActivatedAt"" TEXT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""Description"" TEXT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MapVersions_MapId_MapVersion"" ON ""MapVersions"" (""MapId"", ""MapVersion"");
                CREATE INDEX IF NOT EXISTS ""IX_MapVersions_IsActive"" ON ""MapVersions"" (""IsActive"");
                CREATE TABLE IF NOT EXISTS ""MapAreas"" (
                    ""MapId"" TEXT NOT NULL,
                    ""MapVersion"" TEXT NOT NULL DEFAULT 'v1',
                    ""AreaId"" TEXT NOT NULL,
                    ""AreaName"" TEXT NOT NULL,
                    ""AreaType"" INTEGER NOT NULL,
                    ""IsEnabled"" INTEGER NOT NULL,
                    ""BoundaryJson"" TEXT NOT NULL,
                    ""Properties"" TEXT NOT NULL,
                    CONSTRAINT ""PK_MapAreas"" PRIMARY KEY (""MapId"", ""MapVersion"", ""AreaId"")
                );
            ");

            EnsureMapSchemaCompatibility(db);
            db.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MapLocationAliases_MapId_MapVersion_Brand_AliasValue"" ON ""MapLocationAliases"" (""MapId"", ""MapVersion"", ""Brand"", ""AliasValue"");
            ");
            NormalizeStaticMapTables(db);
            EnsureReferenceMapSeed(db);
            var referenceSeedInitialized = IsReferenceSeedInitialized(db);

            if (!db.Vehicles.Any())
            {
                db.Vehicles.AddRange(CreateVehicles());
            }
            else
            {
                EnsureReferenceVehicleSeed(db);
            }

            if (!referenceSeedInitialized && !db.ChargeStations.Any() && !db.ChangeTracker.Entries<ChargeStation>().Any())
            {
                db.ChargeStations.AddRange(CreateChargeStations());
            }

            if (!referenceSeedInitialized && !db.MapNodes.Any() && !db.ChangeTracker.Entries<MapNode>().Any())
            {
                db.MapNodes.AddRange(CreateMapNodes());
            }

            if (!referenceSeedInitialized && !db.MapEdges.Any() && !db.ChangeTracker.Entries<MapEdge>().Any())
            {
                db.MapEdges.AddRange(CreateMapEdges());
            }

            if (!referenceSeedInitialized && !db.MapAreas.Any() && !db.ChangeTracker.Entries<MapArea>().Any())
            {
                db.MapAreas.AddRange(CreateMapAreas());
            }

            if (!db.TaskTemplates.Any())
            {
                db.TaskTemplates.AddRange(CreateTaskTemplates());
            }

            if (!db.SystemParameters.Any(parameter => parameter.ParamKey != ReferenceSeedInitializedKey))
            {
                db.SystemParameters.AddRange(CreateSystemParameters());
            }

            if (!db.SystemParameters.Any(parameter =>
                    parameter.ParamKey == "Dispatching:TrafficReservationMode"))
            {
                db.SystemParameters.Add(new ParameterConfig
                {
                    ParamKey = "Dispatching:TrafficReservationMode",
                    ParamName = "交通预约存储模式",
                    ParamValue = "InMemory",
                    DataType = "Enum(InMemory|Persistent)",
                    Description = "调度交通控制与路径预约的存储模式。可选 InMemory 或 Persistent，修改后需重启系统。",
                    RequiresRestart = true
                });
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

        private static void NormalizeStaticMapTables(AgvDispatcherDbContext db)
        {
            // Older local databases may still contain runtime columns on static map tables
            // (for example IsOccupied/IsLocked). Rebuild the tables with static fields only
            // so map publishing can stay aligned with the DTO boundary rules.
            db.Database.ExecuteSqlRaw(@"
PRAGMA foreign_keys=OFF;
DROP TABLE IF EXISTS __MapNodes_Static;
CREATE TABLE __MapNodes_Static (
    MapId TEXT NOT NULL,
    MapVersion TEXT NOT NULL,
    NodeId TEXT NOT NULL,
    NodeCode TEXT NOT NULL,
    Name TEXT NOT NULL,
    NodeType INTEGER NOT NULL,
    Position_MapId TEXT NOT NULL,
    Position_X REAL NOT NULL,
    Position_Y REAL NOT NULL,
    Position_Z REAL NOT NULL,
    Position_Heading REAL NOT NULL,
    Position_NodeId TEXT NULL,
    Position_AreaCode TEXT NULL,
    Heading REAL NOT NULL,
    AreaCode TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL,
    ParkingCapacity INTEGER NOT NULL,
    AllowedBrands TEXT NOT NULL,
    RequiredCapabilities INTEGER NOT NULL,
    Tags TEXT NOT NULL,
    CONSTRAINT PK_MapNodes PRIMARY KEY (MapId, MapVersion, NodeId)
);
INSERT OR IGNORE INTO __MapNodes_Static (
    MapId, MapVersion, NodeId, NodeCode, Name, NodeType, Position_MapId, Position_X, Position_Y,
    Position_Z, Position_Heading, Position_NodeId, Position_AreaCode, Heading,
    AreaCode, IsEnabled, ParkingCapacity, AllowedBrands, RequiredCapabilities, Tags)
SELECT
    MapId, COALESCE(NULLIF(MapVersion, ''), 'v1'), NodeId, NodeCode, Name, NodeType, Position_MapId, Position_X, Position_Y,
    Position_Z, Position_Heading, Position_NodeId, Position_AreaCode, Heading,
    AreaCode, IsEnabled, ParkingCapacity, AllowedBrands, RequiredCapabilities, Tags
FROM MapNodes;
DROP TABLE MapNodes;
ALTER TABLE __MapNodes_Static RENAME TO MapNodes;
CREATE UNIQUE INDEX IF NOT EXISTS IX_MapNodes_MapId_MapVersion_NodeCode ON MapNodes(MapId, MapVersion, NodeCode);

DROP TABLE IF EXISTS __MapEdges_Static;
CREATE TABLE __MapEdges_Static (
    MapId TEXT NOT NULL,
    MapVersion TEXT NOT NULL,
    EdgeId TEXT NOT NULL,
    FromNodeId TEXT NOT NULL,
    ToNodeId TEXT NOT NULL,
    Direction INTEGER NOT NULL,
    Length REAL NOT NULL,
    MaxSpeed REAL NOT NULL,
    TurnAngle REAL NOT NULL,
    Cost INTEGER NOT NULL,
    IsEnabled INTEGER NOT NULL,
    AreaCode TEXT NOT NULL,
    AllowedBrands TEXT NOT NULL,
    MaxVehicleFlow INTEGER NOT NULL,
    EdgeType INTEGER NOT NULL,
    Remark TEXT NOT NULL,
    CONSTRAINT PK_MapEdges PRIMARY KEY (MapId, MapVersion, EdgeId)
);
INSERT OR IGNORE INTO __MapEdges_Static (
    MapId, MapVersion, EdgeId, FromNodeId, ToNodeId, Direction, Length, MaxSpeed, TurnAngle,
    Cost, IsEnabled, AreaCode, AllowedBrands, MaxVehicleFlow, EdgeType, Remark)
SELECT
    MapId, COALESCE(NULLIF(MapVersion, ''), 'v1'), EdgeId, FromNodeId, ToNodeId, Direction, Length, MaxSpeed, TurnAngle,
    Cost, IsEnabled, AreaCode, AllowedBrands, MaxVehicleFlow, EdgeType, Remark
FROM MapEdges;
DROP TABLE MapEdges;
ALTER TABLE __MapEdges_Static RENAME TO MapEdges;
CREATE INDEX IF NOT EXISTS IX_MapEdges_FromNodeId_ToNodeId ON MapEdges(FromNodeId, ToNodeId);
PRAGMA foreign_keys=ON;");
        }

        private static void EnsureMapSchemaCompatibility(AgvDispatcherDbContext db)
        {
            AddColumnIfMissing(db, "MapNodes", "MapVersion", "TEXT NOT NULL DEFAULT 'v1'");
            AddColumnIfMissing(db, "MapEdges", "MapVersion", "TEXT NOT NULL DEFAULT 'v1'");
            AddColumnIfMissing(db, "MapEdges", "EdgeType", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing(db, "MapLocationAliases", "MapVersion", "TEXT NOT NULL DEFAULT 'v1'");
        }

        private static void AddColumnIfMissing(
            AgvDispatcherDbContext db,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            if (!TableExists(db, tableName) || ColumnExists(db, tableName, columnName))
            {
                return;
            }

            db.Database.ExecuteSqlRaw($@"ALTER TABLE ""{tableName}"" ADD COLUMN ""{columnName}"" {columnDefinition};");
        }

        private static bool TableExists(AgvDispatcherDbContext db, string tableName)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName LIMIT 1;";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "$tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);
                return command.ExecuteScalar() is not null;
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private static bool ColumnExists(AgvDispatcherDbContext db, string tableName, string columnName)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $@"PRAGMA table_info(""{tableName}"");";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private static void EnsureReferenceMapSeed(AgvDispatcherDbContext db)
        {
            var hasReferenceMap = db.MapNodes.Any(node => node.NodeId == "PICK-A1")
                && db.MapEdges.Any(edge => edge.EdgeId == "E-PICK-A1-PICK-A2")
                && db.ChargeStations.Any(station => station.NodeId == "CHG-01");

            if (hasReferenceMap)
            {
                EnsureReferenceMapDisplayNames(db);
                EnsureReferenceMapAreas(db);
                EnsureReferenceMapVersion(db);
                MarkReferenceSeedInitialized(db);
                return;
            }

            var hasCurrentMapContent = db.MapNodes.Any(node => node.MapId == "MAIN")
                || db.MapEdges.Any(edge => edge.MapId == "MAIN")
                || db.ChargeStations.Any();
            var hasLegacyDemoMap = HasLegacyDemoMap(db);

            if (IsReferenceSeedInitialized(db) && !hasLegacyDemoMap)
            {
                RemoveOrphanMainMapContentWhenNodesAreEmpty(db);
                EnsureReferenceMapVersion(db);
                return;
            }

            if (hasCurrentMapContent && !hasLegacyDemoMap)
            {
                EnsureReferenceMapVersion(db);
                MarkReferenceSeedInitialized(db);
                return;
            }

            db.MapEdges.RemoveRange(db.MapEdges.Where(edge => edge.MapId == "MAIN"));
            db.MapNodes.RemoveRange(db.MapNodes.Where(node => node.MapId == "MAIN"));
            db.MapAreas.RemoveRange(db.MapAreas.Where(area => area.MapId == "MAIN"));
            db.MapLocationAliases.RemoveRange(db.MapLocationAliases.Where(alias => alias.MapId == "MAIN"));
            db.ChargeStations.RemoveRange(db.ChargeStations.Where(station =>
                station.NodeId.StartsWith("Charge-")
                || station.NodeId == "CHG-01"
                || station.NodeId == "CHG-02"
                || station.NodeId == "CHG-03"
                || station.StationId.StartsWith("C-")));

            db.MapNodes.AddRange(CreateMapNodes());
            db.MapEdges.AddRange(CreateMapEdges());
            db.MapAreas.AddRange(CreateMapAreas());
            db.MapLocationAliases.AddRange(CreateMapLocationAliases());
            db.ChargeStations.AddRange(CreateChargeStations());
            EnsureReferenceMapVersion(db);
            MarkReferenceSeedInitialized(db);
        }

        private static void EnsureReferenceMapVersion(AgvDispatcherDbContext db)
        {
            var hasMainV1Content = db.MapNodes.Any(node => node.MapId == "MAIN" && node.MapVersion == "v1")
                || db.ChangeTracker.Entries<MapNode>().Any(entry => entry.Entity.MapId == "MAIN" && entry.Entity.MapVersion == "v1")
                || db.MapEdges.Any(edge => edge.MapId == "MAIN" && edge.MapVersion == "v1")
                || db.ChangeTracker.Entries<MapEdge>().Any(entry => entry.Entity.MapId == "MAIN" && entry.Entity.MapVersion == "v1");
            if (!hasMainV1Content)
            {
                return;
            }

            var existing = db.MapVersions.FirstOrDefault(version => version.MapId == "MAIN" && version.MapVersion == "v1")
                ?? db.ChangeTracker.Entries<MapVersionEntity>()
                    .Select(entry => entry.Entity)
                    .FirstOrDefault(version => version.MapId == "MAIN" && version.MapVersion == "v1");
            var hasActiveVersion = db.MapVersions.Any(version => version.IsActive || version.State == MapState.Active)
                || db.ChangeTracker.Entries<MapVersionEntity>().Any(entry => entry.Entity.IsActive || entry.Entity.State == MapState.Active);
            var now = DateTimeOffset.Now;

            if (existing is null)
            {
                db.MapVersions.Add(new MapVersionEntity
                {
                    MapId = "MAIN",
                    MapVersion = "v1",
                    Name = "AGV静态参考地图",
                    State = hasActiveVersion ? MapState.Archived : MapState.Active,
                    IsActive = !hasActiveVersion,
                    CreatedAt = now,
                    UpdatedAt = now,
                    PublishedAt = now,
                    ActivatedAt = hasActiveVersion ? null : now,
                    Description = "系统初始化参考地图版本。"
                });
                return;
            }

            existing.Name = string.IsNullOrWhiteSpace(existing.Name) ? "AGV静态参考地图" : existing.Name;
            existing.PublishedAt ??= now;
            existing.UpdatedAt = now;
            if (!hasActiveVersion || existing.IsActive || existing.State == MapState.Active)
            {
                existing.State = MapState.Active;
                existing.IsActive = true;
                existing.ActivatedAt ??= now;
            }
        }

        private static void RemoveOrphanMainMapContentWhenNodesAreEmpty(AgvDispatcherDbContext db)
        {
            if (db.MapNodes.Any(node => node.MapId == "MAIN"))
            {
                return;
            }

            db.MapEdges.RemoveRange(db.MapEdges.Where(edge => edge.MapId == "MAIN"));
            db.MapAreas.RemoveRange(db.MapAreas.Where(area => area.MapId == "MAIN"));
            db.MapLocationAliases.RemoveRange(db.MapLocationAliases.Where(alias => alias.MapId == "MAIN"));
            db.ChargeStations.RemoveRange(db.ChargeStations.Where(station =>
                station.NodeId == "CHG-01"
                || station.NodeId == "CHG-02"
                || station.NodeId == "CHG-03"
                || station.NodeId.StartsWith("Charge-")
                || station.StationId.StartsWith("CHG-")
                || station.StationId.StartsWith("C-")));
        }

        private static bool HasLegacyDemoMap(AgvDispatcherDbContext db)
        {
            var legacyNodeIds = new[] { "A1", "A2", "B2", "B3", "Charge-1", "P1", "P2", "X1", "W1", "D1" };
            return db.MapNodes.Any(node => legacyNodeIds.Contains(node.NodeId))
                || db.MapEdges.Any(edge => edge.EdgeId == "E1" || edge.EdgeId == "E2");
        }

        private static bool IsReferenceSeedInitialized(AgvDispatcherDbContext db)
        {
            return db.SystemParameters.Any(parameter =>
                    parameter.ParamKey == ReferenceSeedInitializedKey && parameter.ParamValue == "true")
                || db.ChangeTracker.Entries<ParameterConfig>().Any(entry =>
                    entry.Entity.ParamKey == ReferenceSeedInitializedKey && entry.Entity.ParamValue == "true");
        }

        private static void MarkReferenceSeedInitialized(AgvDispatcherDbContext db)
        {
            var existing = db.SystemParameters.FirstOrDefault(parameter =>
                    parameter.ParamKey == ReferenceSeedInitializedKey)
                ?? db.ChangeTracker.Entries<ParameterConfig>()
                    .Select(entry => entry.Entity)
                    .FirstOrDefault(parameter => parameter.ParamKey == ReferenceSeedInitializedKey);

            if (existing is null)
            {
                db.SystemParameters.Add(new ParameterConfig
                {
                    ParamKey = ReferenceSeedInitializedKey,
                    ParamName = "参考地图初始化标记",
                    ParamValue = "true",
                    DataType = "Boolean",
                    Description = "本地 SQLite 是否已经完成静态参考地图初始化。发布空地图或自定义地图后，不再自动回填参考地图。",
                    RequiresRestart = false
                });
                return;
            }

            existing.ParamValue = "true";
        }

        private static void EnsureReferenceMapDisplayNames(AgvDispatcherDbContext db)
        {
            var referenceNodes = CreateMapNodes().ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            foreach (var node in db.MapNodes.Where(node => node.MapId == "MAIN"))
            {
                if (referenceNodes.TryGetValue(node.NodeId, out var reference))
                {
                    node.Name = reference.Name;
                    node.AreaCode = reference.AreaCode;
                    node.NodeType = reference.NodeType;
                    node.ParkingCapacity = reference.ParkingCapacity;
                    node.AllowedBrands = reference.AllowedBrands;
                }
            }

            var referenceStations = CreateChargeStations().ToDictionary(station => station.StationId, StringComparer.OrdinalIgnoreCase);
            foreach (var station in db.ChargeStations)
            {
                if (referenceStations.TryGetValue(station.StationId, out var reference))
                {
                    station.Name = reference.Name;
                    station.AreaCode = reference.AreaCode;
                    station.NodeId = reference.NodeId;
                    station.AllowedBrands = reference.AllowedBrands;
                }
            }
        }

        private static void EnsureReferenceMapAreas(AgvDispatcherDbContext db)
        {
            var referenceAreas = CreateMapAreas().ToDictionary(area => area.AreaId, StringComparer.OrdinalIgnoreCase);
            foreach (var reference in referenceAreas.Values)
            {
                var area = db.MapAreas.FirstOrDefault(item =>
                    item.MapId == reference.MapId &&
                    item.MapVersion == reference.MapVersion &&
                    item.AreaId == reference.AreaId);
                if (area is null)
                {
                    db.MapAreas.Add(reference);
                    continue;
                }

                area.AreaName = reference.AreaName;
                area.AreaType = reference.AreaType;
                area.IsEnabled = reference.IsEnabled;
                area.BoundaryJson = reference.BoundaryJson;
                area.Properties = reference.Properties;
            }
        }

        private static void EnsureReferenceVehicleSeed(AgvDispatcherDbContext db)
        {
            foreach (var reference in CreateVehicles())
            {
                var vehicle = db.Vehicles.FirstOrDefault(item => item.VehicleId == reference.VehicleId);
                if (vehicle is null)
                {
                    db.Vehicles.Add(reference);
                    continue;
                }

                vehicle.VehicleCode = reference.VehicleCode;
                vehicle.Name = reference.Name;
                vehicle.Brand = reference.Brand;
                vehicle.Model = reference.Model;
                vehicle.AreaCode = reference.AreaCode;
                vehicle.HomeNodeId = reference.HomeNodeId;
                vehicle.ChargeNodeId = reference.ChargeNodeId;
                vehicle.AdapterType = reference.AdapterType;
                vehicle.ProtocolType = reference.ProtocolType;
                vehicle.Endpoint = reference.Endpoint;
                vehicle.MaxSpeed = reference.MaxSpeed;
                vehicle.RatedLoad = reference.RatedLoad;
                vehicle.CapabilityFlags = reference.CapabilityFlags;
                vehicle.MinDispatchBattery = reference.MinDispatchBattery;
                vehicle.IsEnabled = true;
            }
        }

        private static IReadOnlyList<Vehicle> CreateVehicles()
        {
            return new[]
            {
                new Vehicle { VehicleId = "AGV-002", VehicleCode = "AGV-002", Name = "\u6d77\u5eb7\u642c\u8fd0\u8f66 002", Brand = "RGV-A", Model = "A100", AreaCode = "CAP-A", HomeNodeId = "PICK-A1", ChargeNodeId = "CHG-01", MaxSpeed = 1.5, RatedLoad = 500, AdapterType = "MockBrandA", ProtocolType = "HTTP", Endpoint = "http://192.168.1.10:8000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Lift, MinDispatchBattery = 30.0 },
                new Vehicle { VehicleId = "AGV-003", VehicleCode = "AGV-003", Name = "\u6d77\u5eb7\u642c\u8fd0\u8f66 003", Brand = "RGV-A", Model = "A100", AreaCode = "CAP-A", HomeNodeId = "PICK-A2", ChargeNodeId = "CHG-02", MaxSpeed = 1.5, RatedLoad = 500, AdapterType = "MockBrandA", ProtocolType = "HTTP", Endpoint = "http://192.168.1.11:8000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Lift, MinDispatchBattery = 30.0 },
                new Vehicle { VehicleId = "AGV-008", VehicleCode = "AGV-008", Name = "\u676d\u53c9\u7275\u5f15\u8f66 008", Brand = "RGV-B", Model = "B200", AreaCode = "RAW-B", HomeNodeId = "RAW-IN-01", ChargeNodeId = "CHG-02", MaxSpeed = 1.2, RatedLoad = 800, AdapterType = "MockBrandB", ProtocolType = "TCP", Endpoint = "192.168.1.20:5000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Tow, MinDispatchBattery = 40.0 },
                new Vehicle { VehicleId = "AGV-010", VehicleCode = "AGV-010", Name = "\u676d\u53c9\u7275\u5f15\u8f66 010", Brand = "RGV-B", Model = "B200", AreaCode = "INT-01", HomeNodeId = "INT-C", ChargeNodeId = "CHG-02", MaxSpeed = 1.2, RatedLoad = 800, AdapterType = "MockBrandB", ProtocolType = "TCP", Endpoint = "192.168.1.21:5000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Tow, MinDispatchBattery = 40.0 },
                new Vehicle { VehicleId = "AGV-017", VehicleCode = "AGV-017", Name = "\u6fc0\u5149\u53c9\u53d6\u8f66 017", Brand = "RGV-C", Model = "C300", AreaCode = "STBY-CHG", HomeNodeId = "WAIT-02", ChargeNodeId = "CHG-03", MaxSpeed = 1.0, RatedLoad = 1000, AdapterType = "RealTcp", ProtocolType = "TCP", Endpoint = "192.168.1.30:4000", CapabilityFlags = VehicleCapability.Transfer | VehicleCapability.Fork, MinDispatchBattery = 20.0 }
            };
        }

        private static IReadOnlyList<ChargeStation> CreateChargeStations()
        {
            return new[]
            {
                CreateStation("CHG-01", "1号充电桩", ChargeStationState.Available, null, 3.3, 0, 760, 455, "HANGCHA,RGV-A", "TCP", "192.168.1.100", 502),
                CreateStation("CHG-02", "2号充电桩", ChargeStationState.Available, null, 6.6, 0, 810, 455, "RGV-A,RGV-B", "TCP", "192.168.1.101", 502),
                CreateStation("CHG-03", "3号快充桩", ChargeStationState.Available, null, 12.0, 0, 760, 510, "HANGCHA,RGV-C", "TCP", "192.168.1.102", 502)
            };
        }

        private static IReadOnlyList<MapNode> CreateMapNodes()
        {
            return new[]
            {
                Node("PICK-A1", "\u6210\u54c1\u5e93\u53d6\u8d27\u70b9A1", MapNodeType.Pickup, 85, 85, "CAP-A", 1, "RGV-A,HIK"),
                Node("PICK-A2", "\u6210\u54c1\u5e93\u53d6\u8d27\u70b9A2", MapNodeType.Pickup, 185, 85, "CAP-A", 1, "RGV-A,HIK"),
                Node("PUT-A1", "\u6210\u54c1\u5e93\u653e\u8d27\u70b9A1", MapNodeType.Dropoff, 240, 165, "CAP-A", 1, "RGV-A"),
                Node("RAW-IN-01", "\u539f\u6599\u533a\u5165\u5e93\u70b91", MapNodeType.Pickup, 105, 305, "RAW-B", 1, "HANGCHA,RGV-B"),
                Node("RAW-IN-02", "\u539f\u6599\u533a\u5165\u5e93\u70b92", MapNodeType.Pickup, 185, 305, "RAW-B", 1, "HANGCHA,RGV-B"),
                Node("RAW-OUT-01", "\u539f\u6599\u533a\u51fa\u5e93\u70b91", MapNodeType.Dropoff, 245, 370, "RAW-B", 1, "HANGCHA"),
                Node("QR-01", "\u4e8c\u7ef4\u7801\u5bfc\u822a\u70b91", MapNodeType.Normal, 355, 100, "QR-A", 1, "HIK"),
                Node("QR-02", "\u4e8c\u7ef4\u7801\u5bfc\u822a\u70b92", MapNodeType.Normal, 455, 120, "QR-A", 1, "HIK"),
                Node("QR-03", "\u4e8c\u7ef4\u7801\u5bfc\u822a\u70b93", MapNodeType.Normal, 500, 240, "QR-A", 1, "HIK"),
                Node("SLAM-01", "\u6fc0\u5149\u533a\u5165\u53e3", MapNodeType.Normal, 645, 95, "SLAM-B", 1, "RGV-C"),
                Node("SLAM-02", "\u6fc0\u5149\u5de5\u4f4d", MapNodeType.Station, 760, 165, "SLAM-B", 1, "RGV-C"),
                Node("INT-N", "\u4e92\u65a5\u533a\u5317\u53e3", MapNodeType.Intersection, 390, 255, "INT-01", 1, string.Empty),
                Node("INT-C", "\u4e92\u65a5\u533a\u4e2d\u5fc3", MapNodeType.Intersection, 455, 300, "INT-01", 1, string.Empty),
                Node("INT-S", "\u4e92\u65a5\u533a\u5357\u53e3", MapNodeType.Intersection, 420, 365, "INT-01", 1, string.Empty),
                Node("FIRE-G1", "\u6d88\u9632\u95e8\u524d\u70b9", MapNodeType.Door, 650, 285, "FIRE-01", 1, "RGV-A,RGV-C"),
                Node("FIRE-G2", "\u6d88\u9632\u95e8\u540e\u70b9", MapNodeType.Door, 790, 285, "FIRE-01", 1, "RGV-A,RGV-C"),
                Node("SPEED-IN", "\u9650\u901f\u533a\u5165\u53e3", MapNodeType.Normal, 870, 320, "SPD-01", 1, string.Empty),
                Node("WEIGH-01", "\u5730\u78c5\u79f0\u91cd\u70b9", MapNodeType.Station, 965, 360, "SPD-01", 1, "HANGCHA"),
                Node("WASH-01", "\u6e05\u6d17\u5de5\u4f4d", MapNodeType.Station, 1035, 420, "SPD-01", 1, "HANGCHA"),
                Node("SPEED-OUT", "\u9650\u901f\u533a\u51fa\u53e3", MapNodeType.Normal, 1085, 480, "SPD-01", 1, string.Empty),
                Node("WAIT-01", "\u5f85\u673a\u4f4d1", MapNodeType.Waiting, 595, 430, "STBY-CHG", 1, string.Empty),
                Node("WAIT-02", "\u5f85\u673a\u4f4d2", MapNodeType.Waiting, 645, 430, "STBY-CHG", 1, string.Empty),
                Node("WAIT-03", "\u5f85\u673a\u4f4d3", MapNodeType.Waiting, 595, 500, "STBY-CHG", 1, string.Empty),
                Node("PARK-01", "\u505c\u8f66\u4f4d1", MapNodeType.Waiting, 700, 430, "STBY-CHG", 1, string.Empty),
                Node("CHG-01", "\u5145\u7535\u68691", MapNodeType.Charge, 760, 455, "STBY-CHG", 1, "HANGCHA,RGV-A"),
                Node("CHG-02", "\u5145\u7535\u68692", MapNodeType.Charge, 810, 455, "STBY-CHG", 1, "RGV-A,RGV-B"),
                Node("CHG-03", "\u5feb\u5145\u68693", MapNodeType.Charge, 760, 510, "STBY-CHG", 1, "HANGCHA,RGV-C"),
                Node("MAINT-IN", "\u7ef4\u62a4\u533a\u5165\u53e3", MapNodeType.Normal, 360, 450, "MAINT-01", 1, string.Empty),
                Node("MAINT-OUT", "\u7ef4\u62a4\u533a\u51fa\u53e3", MapNodeType.Normal, 500, 500, "MAINT-01", 1, string.Empty)
            };
        }

        private static IReadOnlyList<MapEdge> CreateMapEdges()
        {
            return new[]
            {
                Edge("PICK-A1", "PICK-A2", 90, "CAP-A", 1.2),
                Edge("PICK-A2", "PUT-A1", 120, "CAP-A", 1.2),
                Edge("PUT-A1", "INT-N", 210, "INT-01", 1.5),
                Edge("RAW-IN-01", "RAW-IN-02", 75, "RAW-B", 1.0),
                Edge("RAW-IN-02", "RAW-OUT-01", 105, "RAW-B", 1.0),
                Edge("RAW-OUT-01", "QR-03", 95, "QR-A", 1.0),
                Edge("QR-01", "QR-02", 110, "QR-A", 1.0),
                Edge("QR-02", "QR-03", 110, "QR-A", 1.0),
                Edge("QR-03", "INT-S", 170, "INT-01", 1.2),
                Edge("SLAM-01", "SLAM-02", 170, "SLAM-B", 1.5),
                Edge("SLAM-02", "INT-N", 170, "INT-01", 1.2),
                Edge("INT-N", "INT-C", 75, "INT-01", 0.8),
                Edge("INT-C", "INT-S", 70, "INT-01", 0.8),
                Edge("INT-C", "FIRE-G1", 230, "FIRE-01", 1.0, EdgeDirection.ForwardOnly),
                Edge("FIRE-G1", "FIRE-G2", 140, "FIRE-01", 0.8, EdgeDirection.ForwardOnly),
                Edge("FIRE-G2", "SPEED-IN", 190, "SPD-01", 1.2),
                Edge("SPEED-IN", "WEIGH-01", 115, "SPD-01", 0.3),
                Edge("WEIGH-01", "WASH-01", 80, "SPD-01", 0.3),
                Edge("WASH-01", "SPEED-OUT", 65, "SPD-01", 0.3),
                Edge("SPEED-OUT", "CHG-02", 200, "STBY-CHG", 0.8),
                Edge("WAIT-01", "WAIT-02", 50, "STBY-CHG", 0.6),
                Edge("WAIT-01", "WAIT-03", 70, "STBY-CHG", 0.6),
                Edge("WAIT-02", "PARK-01", 55, "STBY-CHG", 0.6),
                Edge("PARK-01", "CHG-01", 70, "STBY-CHG", 0.5),
                Edge("CHG-01", "CHG-02", 50, "STBY-CHG", 0.5),
                Edge("WAIT-03", "CHG-03", 165, "STBY-CHG", 0.5),
                Edge("CHG-03", "CHG-01", 70, "STBY-CHG", 0.5),
                Edge("INT-S", "MAINT-IN", 145, "MAINT-01", 0.8),
                Edge("MAINT-IN", "MAINT-OUT", 150, "MAINT-01", 0.5, EdgeDirection.Closed, false),
                Edge("MAINT-OUT", "WAIT-01", 140, "STBY-CHG", 0.6)
            };
        }

        private static IReadOnlyList<MapArea> CreateMapAreas()
        {
            return new[]
            {
                Area("CAP-A", "成品库限流区", AgvDispatcher.Core.Contracts.Map.MapAreaType.WorkArea, "#00BFA6", "CapacityLimited", 3, (40, 40), (300, 40), (300, 210), (40, 210)),
                Area("RAW-B", "原材料限流区", AgvDispatcher.Core.Contracts.Map.MapAreaType.WorkArea, "#32D583", "CapacityLimited", 4, (40, 255), (300, 255), (300, 420), (40, 420)),
                Area("QR-A", "二维码导航区", AgvDispatcher.Core.Contracts.Map.MapAreaType.Normal, "#8EA8C3", "NavigationMedium", 0, (330, 55), (540, 55), (540, 260), (330, 260)),
                Area("SLAM-B", "激光SLAM导航区", AgvDispatcher.Core.Contracts.Map.MapAreaType.Normal, "#5A7FA6", "NavigationMedium", 0, (610, 45), (840, 45), (840, 210), (610, 210)),
                Area("INT-01", "交通互斥区", AgvDispatcher.Core.Contracts.Map.MapAreaType.IntersectionArea, "#FFB020", "Interlocking", 1, (350, 225), (535, 240), (530, 385), (350, 395)),
                Area("FIRE-01", "消防安全联动区", AgvDispatcher.Core.Contracts.Map.MapAreaType.BlockedArea, "#FF4D4F", "FireSafety", 0, (610, 250), (835, 250), (835, 340), (610, 340)),
                Area("SPD-01", "限速工艺区", AgvDispatcher.Core.Contracts.Map.MapAreaType.NarrowArea, "#9B6DFF", "SpeedRestricted", 0, (840, 295), (1130, 310), (1130, 535), (840, 535)),
                Area("STBY-CHG", "待机充电区", AgvDispatcher.Core.Contracts.Map.MapAreaType.ChargingArea, "#FFD700", "StandbyCharging", 5, (560, 395), (850, 395), (850, 545), (560, 545)),
                Area("MAINT-01", "维护阻断预留区", AgvDispatcher.Core.Contracts.Map.MapAreaType.BlockedArea, "#777777", "StaticRestricted", 0, (335, 425), (540, 425), (540, 535), (335, 535))
            };
        }

        private static IReadOnlyList<MapLocationAlias> CreateMapLocationAliases()
        {
            return new[]
            {
                Alias("PICK-A1", "GLOBAL", "STATION-01"),
                Alias("RAW-IN-01", "GLOBAL", "RAW-IN-01"),
                Alias("QR-02", "HIK", "HK_QR_002"),
                Alias("INT-C", "HIK", "HK_CROSS_01"),
                Alias("WEIGH-01", "HANGCHA", "HC_WEIGHT_01"),
                Alias("CHG-01", "HANGCHA", "HC_CHARGE_01"),
                Alias("CHG-03", "HANGCHA", "HC_FAST_CHARGE_03"),
                Alias("FIRE-G1", "RGV-A", "DOOR_SAFE_A")
            };
        }

        private static MapNode Node(
            string id,
            string name,
            MapNodeType type,
            double x,
            double y,
            string areaCode,
            int capacity,
            string allowedBrands)
        {
            return new MapNode
            {
                NodeId = id,
                MapId = "MAIN",
                NodeCode = id,
                Name = name,
                NodeType = type,
                AreaCode = areaCode,
                IsEnabled = true,
                ParkingCapacity = capacity,
                AllowedBrands = allowedBrands,
                Position = new MapPosition { MapId = "MAIN", NodeId = id, X = x, Y = y, AreaCode = areaCode }
            };
        }

        private static MapEdge Edge(
            string from,
            string to,
            double length,
            string areaCode,
            double maxSpeed,
            EdgeDirection direction = EdgeDirection.Bidirectional,
            bool enabled = true)
        {
            return new MapEdge
            {
                EdgeId = $"E-{from}-{to}",
                MapId = "MAIN",
                FromNodeId = from,
                ToNodeId = to,
                Direction = direction,
                Length = length,
                MaxSpeed = maxSpeed,
                Cost = Math.Max(1, (int)Math.Round(length)),
                IsEnabled = enabled,
                AreaCode = areaCode,
                EdgeType = (int)ResolveEdgeType(areaCode),
                MaxVehicleFlow = areaCode is "CAP-A" or "RAW-B" ? 2 : 1,
                Remark = direction == EdgeDirection.Closed ? "Static maintenance candidate edge for map display only" : string.Empty
            };
        }

        private static MapArea Area(
            string areaId,
            string areaName,
            AgvDispatcher.Core.Contracts.Map.MapAreaType areaType,
            string color,
            string zoneKind,
            int capacity,
            params (double X, double Y)[] points)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Color"] = color,
                ["ZoneKind"] = zoneKind,
                ["Capacity"] = capacity.ToString()
            };

            return new MapArea
            {
                AreaId = areaId,
                MapId = "MAIN",
                MapVersion = "v1",
                AreaName = areaName,
                AreaType = areaType,
                IsEnabled = true,
                BoundaryJson = JsonSerializer.Serialize(points.Select(point => new { point.X, point.Y })),
                Properties = properties
            };
        }

        private static AgvDispatcher.Core.Contracts.Map.MapEdgeType ResolveEdgeType(string areaCode) => areaCode switch
        {
            "INT-01" => AgvDispatcher.Core.Contracts.Map.MapEdgeType.Intersection,
            "SPD-01" => AgvDispatcher.Core.Contracts.Map.MapEdgeType.NarrowRoad,
            "STBY-CHG" => AgvDispatcher.Core.Contracts.Map.MapEdgeType.ChargingRoad,
            "CAP-A" or "RAW-B" => AgvDispatcher.Core.Contracts.Map.MapEdgeType.WorkRoad,
            _ => AgvDispatcher.Core.Contracts.Map.MapEdgeType.MainRoad
        };

        private static MapLocationAlias Alias(string nodeId, string brand, string value)
        {
            return new MapLocationAlias
            {
                AliasId = $"{brand}-{nodeId}",
                MapId = "MAIN",
                NodeId = nodeId,
                AliasType = "Vendor",
                AliasValue = value,
                Brand = brand == "GLOBAL" ? string.Empty : brand,
                IsEnabled = true
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
                new ParameterConfig { ParamKey = "MAX_WAIT_TIME", ParamName = "\u6700\u5927\u7b49\u5f85\u65f6\u95f4", ParamValue = "300", DataType = "Int(seconds)", Description = "\u4efb\u52a1\u8282\u70b9\u6700\u5927\u5141\u8bb8\u7b49\u5f85\u65f6\u95f4\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "DEFAULT_SPEED", ParamName = "\u9ed8\u8ba4\u901f\u5ea6", ParamValue = "1.2", DataType = "Float(m/s)", Description = "\u521b\u5efa\u4efb\u52a1\u65f6\u7684\u9ed8\u8ba4\u901f\u5ea6\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "RETRY_COUNT", ParamName = "\u91cd\u8bd5\u6b21\u6570", ParamValue = "3", DataType = "Int", Description = "\u6d3e\u53d1\u5931\u8d25\u540e\u7684\u81ea\u52a8\u91cd\u8bd5\u6b21\u6570\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "LOG_RETENTION_DAYS", ParamName = "\u65e5\u5fd7\u4fdd\u7559\u5929\u6570", ParamValue = "90", DataType = "Int(days)", Description = "\u672c\u5730\u64cd\u4f5c\u65e5\u5fd7\u4fdd\u7559\u7a97\u53e3\u3002", RequiresRestart = true },
                new ParameterConfig { ParamKey = "MIN_DISPATCH_BATTERY", ParamName = "\u6700\u4f4e\u6d3e\u53d1\u7535\u91cf", ParamValue = "20", DataType = "Float(%)", Description = "\u5168\u5c40\u6700\u4f4e\u6d3e\u53d1\u7535\u91cf\uff0c\u4f4e\u4e8e\u6b64\u7535\u91cf\u4e0d\u6d3e\u53d1\u4efb\u52a1\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "LOW_BATTERY_ALARM", ParamName = "\u4f4e\u7535\u91cf\u544a\u8b66\u9608\u503c", ParamValue = "15", DataType = "Float(%)", Description = "\u4f4e\u4e8e\u6b64\u7535\u91cf\u89e6\u53d1\u544a\u8b66\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "TASK_TIMEOUT_MINUTES", ParamName = "\u4efb\u52a1\u8d85\u65f6\u65f6\u95f4", ParamValue = "60", DataType = "Int(minutes)", Description = "\u4efb\u52a1\u6267\u884c\u8d85\u65f6\u65f6\u95f4\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "AUTO_DISPATCH_ENABLED", ParamName = "\u81ea\u52a8\u6d3e\u53d1\u5f00\u5173", ParamValue = "true", DataType = "Boolean", Description = "\u662f\u5426\u5f00\u542f\u81ea\u52a8\u4efb\u52a1\u6d3e\u53d1\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_BATTERY", ParamName = "\u7535\u91cf\u8bc4\u5206\u6743\u91cd", ParamValue = "25.0", DataType = "Float", Description = "\u8c03\u5ea6\u65f6\u7535\u91cf\u7684\u5f97\u5206\u6743\u91cd (0-100)\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_DISTANCE", ParamName = "\u8ddd\u79bb\u8bc4\u5206\u6743\u91cd", ParamValue = "35.0", DataType = "Float", Description = "\u8c03\u5ea6\u65f6\u8ddd\u79bb\u7684\u5f97\u5206\u6743\u91cd (0-100)\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_LOAD", ParamName = "\u8f7d\u91cd\u8bc4\u5206\u6743\u91cd", ParamValue = "10.0", DataType = "Float", Description = "\u8c03\u5ea6\u65f6\u8f7d\u91cd\u80fd\u529b\u7684\u5f97\u5206\u6743\u91cd (0-100)\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_PRIORITY", ParamName = "\u4f18\u5148\u7ea7\u8bc4\u5206\u6743\u91cd", ParamValue = "15.0", DataType = "Float", Description = "\u8c03\u5ea6\u65f6\u4f18\u5148\u7ea7\u7684\u5f97\u5206\u6743\u91cd (0-100)\u3002", RequiresRestart = false },
                new ParameterConfig { ParamKey = "SCORE_WEIGHT_AREA", ParamName = "\u533a\u57df\u8bc4\u5206\u6743\u91cd", ParamValue = "15.0", DataType = "Float", Description = "\u8c03\u5ea6\u65f6\u540c\u533a\u57df\u7684\u5f97\u5206\u6743\u91cd (0-100)\u3002", RequiresRestart = false }
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


