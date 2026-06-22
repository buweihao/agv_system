using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.MapManagement.Results;

namespace AgvDispatcher.Modules.MapModule.Services
{
    /// <summary>
    /// Mock 地图共享内核：草稿区和运行图区分存储，发布只切换当前运行版本指针。
    /// </summary>
    public sealed class MockMapStore
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, MapDraftDto> _drafts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapSnapshotDto> _publishedSnapshots = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapVersionDto> _versions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _currentVersionByMapId = new(StringComparer.OrdinalIgnoreCase);

        public MockMapStore()
        {
            Seed();
        }

        public MapSnapshotDto GetCurrentMap()
        {
            lock (_syncRoot)
            {
                var currentMapId = _currentVersionByMapId.Keys.FirstOrDefault();
                if (currentMapId is null)
                {
                    return new MapSnapshotDto();
                }

                var version = _currentVersionByMapId[currentMapId];
                return Clone(_publishedSnapshots[VersionKey(currentMapId, version)]);
            }
        }

        public MapSnapshotDto? GetPublishedSnapshot(string mapId, string version)
        {
            lock (_syncRoot)
            {
                return _publishedSnapshots.TryGetValue(VersionKey(mapId, version), out var snapshot)
                    ? Clone(snapshot)
                    : null;
            }
        }

        public MapDraftDto CreateDraft(string mapName, string? sourceVersion, string? operatorId)
        {
            lock (_syncRoot)
            {
                var source = ResolveSourceSnapshot(sourceVersion);
                var now = DateTimeOffset.Now;
                var map = source is null
                    ? new MapSnapshotDto
                    {
                        MapId = Guid.NewGuid().ToString("N"),
                        MapName = string.IsNullOrWhiteSpace(mapName) ? "未命名地图" : mapName,
                        Version = "draft",
                        UpdatedAt = now
                    }
                    : CopySnapshot(Clone(source), version: "draft", updatedAt: now, mapName: string.IsNullOrWhiteSpace(mapName) ? source.MapName : mapName);

                var draft = new MapDraftDto
                {
                    DraftId = Guid.NewGuid().ToString("N"),
                    Map = map,
                    CreatedAt = now,
                    UpdatedAt = now,
                    OperatorId = operatorId
                };

                _drafts[draft.DraftId] = draft;
                return CloneDraft(draft);
            }
        }

        public MapDraftDto? GetDraft(string draftId)
        {
            lock (_syncRoot)
            {
                return _drafts.TryGetValue(draftId, out var draft) ? CloneDraft(draft) : null;
            }
        }

        public MapDraftDto SaveDraft(string draftId, MapSnapshotDto map, string? operatorId)
        {
            lock (_syncRoot)
            {
                var now = DateTimeOffset.Now;
                var draft = new MapDraftDto
                {
                    DraftId = draftId,
                    Map = CopySnapshot(Clone(map), version: "draft", updatedAt: now),
                    CreatedAt = _drafts.TryGetValue(draftId, out var existing) ? existing.CreatedAt : now,
                    UpdatedAt = now,
                    OperatorId = operatorId
                };

                _drafts[draftId] = draft;
                return CloneDraft(draft);
            }
        }

        public (MapSnapshotDto Snapshot, MapVersionDto Version) PublishDraft(string draftId, string? operatorId)
        {
            lock (_syncRoot)
            {
                if (!_drafts.TryGetValue(draftId, out var draft))
                {
                    throw new InvalidOperationException($"Draft '{draftId}' was not found.");
                }

                var now = DateTimeOffset.Now;
                var version = now.ToString("yyyyMMddHHmmss");
                var snapshot = CopySnapshot(Clone(draft.Map), version: version, updatedAt: now);

                var versionDto = new MapVersionDto
                {
                    MapId = snapshot.MapId,
                    MapName = snapshot.MapName,
                    Version = version,
                    IsCurrent = true,
                    PublishedAt = now,
                    OperatorId = operatorId
                };

                SetCurrentVersion(snapshot, versionDto);
                return (Clone(snapshot), versionDto);
            }
        }

        public MapVersionDto? Rollback(string mapId, string version, string? operatorId)
        {
            lock (_syncRoot)
            {
                var key = VersionKey(mapId, version);
                if (!_publishedSnapshots.TryGetValue(key, out var snapshot)
                    || !_versions.TryGetValue(key, out var versionDto))
                {
                    return null;
                }

                var rollbackVersion = new MapVersionDto
                {
                    MapId = versionDto.MapId,
                    MapName = versionDto.MapName,
                    Version = versionDto.Version,
                    IsCurrent = true,
                    PublishedAt = versionDto.PublishedAt,
                    OperatorId = operatorId ?? versionDto.OperatorId
                };

                SetCurrentVersion(snapshot, rollbackVersion);
                return rollbackVersion;
            }
        }

        public IReadOnlyList<MapVersionDto> GetVersions(string? mapId)
        {
            lock (_syncRoot)
            {
                return _versions.Values
                    .Where(version => string.IsNullOrWhiteSpace(mapId)
                        || string.Equals(version.MapId, mapId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(version => version.PublishedAt)
                    .Select(CloneVersion)
                    .ToList();
            }
        }

        private MapSnapshotDto? ResolveSourceSnapshot(string? sourceVersion)
        {
            if (string.IsNullOrWhiteSpace(sourceVersion))
            {
                return GetCurrentMap();
            }

            var match = _publishedSnapshots
                .FirstOrDefault(item => item.Value.Version.Equals(sourceVersion, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(match.Key) ? null : Clone(match.Value);
        }

        private void SetCurrentVersion(MapSnapshotDto snapshot, MapVersionDto currentVersion)
        {
            var key = VersionKey(snapshot.MapId, snapshot.Version);
            _publishedSnapshots[key] = Clone(snapshot);
            _currentVersionByMapId[snapshot.MapId] = snapshot.Version;

            foreach (var versionKey in _versions.Keys.ToList())
            {
                var item = _versions[versionKey];
                if (!string.Equals(item.MapId, snapshot.MapId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _versions[versionKey] = CopyVersion(item, isCurrent: false);
            }

            _versions[key] = currentVersion;
        }

        private void Seed()
        {
            static MapPointDto P(double x, double y) => new() { X = x, Y = y };
                static Dictionary<string, string> Props(params (string Key, string Value)[] values)
                    => values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
                static MapAreaDto Area(string id, string name, MapAreaType type, string color, IReadOnlyList<MapPointDto> points, params (string Key, string Value)[] properties) => new()
                {
                    AreaId = id,
                    AreaName = name,
                    AreaType = type,
                    BoundaryPoints = points,
                    Enabled = true,
                    Properties = Props(properties.Prepend(("Color", color)).ToArray())
                };
                static MapNodeDto Node(string id, string name, MapNodeType type, double x, double y, string areaId, params (string Key, string Value)[] properties) => new()
                {
                    NodeId = id,
                    NodeCode = id,
                    NodeName = name,
                    NodeType = type,
                    X = x,
                    Y = y,
                    AreaId = areaId,
                    Enabled = true,
                    Properties = Props(properties)
                };
                static MapEdgeDto Edge(string from, string to, double distance, string areaId, MapEdgeType edgeType, double speedLimit, MapEdgeDirection direction = MapEdgeDirection.Bidirectional, bool enabled = true, params (string Key, string Value)[] properties) => new()
                {
                    EdgeId = $"E-{from}-{to}",
                    FromNodeId = from,
                    ToNodeId = to,
                    Direction = direction,
                    Distance = distance,
                    EdgeType = edgeType,
                    SpeedLimit = speedLimit,
                    Enabled = enabled,
                    AreaId = areaId,
                    Properties = Props(properties)
                };

                var now = DateTimeOffset.Now;
                var snapshot = new MapSnapshotDto
                {
                    MapId = "MAIN",
                    MapName = "AGV 主地图",
                    Version = "1.0.0",
                    UpdatedAt = now,
                    Areas =
                    [
                        Area("CAP-A", "成品库A 限流 0/3", MapAreaType.WorkArea, "#00BFA6",
                            [P(40, 40), P(300, 40), P(300, 210), P(40, 210)],
                            ("ZoneKind", "CapacityLimited"), ("Legend", "避流/限流控制区域"), ("Capacity", "3"), ("Label", "库区A: 0/3")),
                        Area("RAW-B", "原料库B 限流 0/4", MapAreaType.WorkArea, "#32D583",
                            [P(40, 440), P(300, 440), P(300, 545), P(40, 545)],
                            ("ZoneKind", "CapacityLimited"), ("Legend", "避流/限流控制区域"), ("Capacity", "4"), ("Label", "原料库B: 0/4")),
                        Area("QR-A", "二维码导航区", MapAreaType.Normal, "#8EA8C3",
                            [P(40, 230), P(300, 230), P(300, 420), P(40, 420)],
                            ("ZoneKind", "NavigationMedium"), ("Legend", "物理介质/导航模式区域"), ("NavigationMedium", "QRCode"), ("LayerHint", "GridDots")),
                        Area("SLAM-B", "激光SLAM/CAD区", MapAreaType.Normal, "#5A7FA6",
                            [P(320, 40), P(570, 40), P(570, 150), P(320, 150)],
                            ("ZoneKind", "NavigationMedium"), ("Legend", "物理介质/导航模式区域"), ("NavigationMedium", "LaserSLAM"), ("LayerHint", "PgmCad")),
                        Area("INT-01", "互斥路口区 0/1", MapAreaType.IntersectionArea, "#FFB020",
                            [P(330, 170), P(500, 170), P(535, 260), P(455, 325), P(330, 300)],
                            ("ZoneKind", "Interlocking"), ("Legend", "交通互斥/管制区域"), ("Capacity", "1"), ("Label", "互斥区: 0/1")),
                        Area("FIRE-01", "消防门安全联动区", MapAreaType.BlockedArea, "#FF4D4F",
                            [P(610, 40), P(830, 40), P(830, 180), P(610, 180)],
                            ("ZoneKind", "FireSafety"), ("Legend", "消防/安全联动报警区域"), ("AlarmSource", "PLC-FIRE-01"), ("VisualHint", "RedDashedIdle")),
                        Area("SPD-01", "地磅/湿滑限速区 Max 300mm/s", MapAreaType.NarrowArea, "#9B6DFF",
                            [P(515, 220), P(830, 220), P(830, 340), P(515, 340)],
                            ("ZoneKind", "SpeedRestricted"), ("Legend", "物理限速/特殊工艺操作区域"), ("SpeedLimit", "0.3"), ("Process", "Weighing")),
                        Area("STBY-CHG", "待机/充电保障区", MapAreaType.ChargingArea, "#FFD700",
                            [P(560, 380), P(830, 380), P(830, 540), P(560, 540)],
                            ("ZoneKind", "StandbyCharging"), ("Legend", "车辆待机排队与能量保障区域"), ("Capacity", "5")),
                        Area("MAINT-01", "静态禁行/维护候选区", MapAreaType.BlockedArea, "#777777",
                            [P(335, 390), P(520, 390), P(520, 535), P(335, 535)],
                            ("ZoneKind", "StaticRestricted"), ("Legend", "静态禁行/维护候选区域"), ("VisualHint", "NoEntryHatch"))
                    ],
                    Nodes =
                    [
                        Node("PICK-A1", "库区A取货口1", MapNodeType.PickPoint, 80, 80, "CAP-A", ("Capacity", "1"), ("AllowedBrands", "HIK,HANGCHA")),
                        Node("PICK-A2", "库区A取货口2", MapNodeType.PickPoint, 170, 80, "CAP-A", ("Capacity", "1"), ("AllowedBrands", "HIK")),
                        Node("PUT-A1", "库区A放货口", MapNodeType.PutPoint, 250, 160, "CAP-A", ("Capacity", "1")),
                        Node("RAW-IN-01", "原料入库口1", MapNodeType.PickPoint, 70, 485, "RAW-B", ("Capacity", "1"), ("AllowedBrands", "HANGCHA")),
                        Node("RAW-IN-02", "原料入库口2", MapNodeType.PickPoint, 145, 485, "RAW-B", ("Capacity", "1"), ("AllowedBrands", "HANGCHA")),
                        Node("RAW-OUT-01", "原料出库口", MapNodeType.PutPoint, 245, 505, "RAW-B", ("Capacity", "1")),
                        Node("QR-01", "二维码入口", MapNodeType.Normal, 80, 280, "QR-A", ("NavigationMedium", "QRCode")),
                        Node("QR-02", "二维码中继", MapNodeType.Normal, 175, 330, "QR-A", ("NavigationMedium", "QRCode")),
                        Node("QR-03", "二维码出口", MapNodeType.Normal, 270, 385, "QR-A", ("NavigationMedium", "QRCode")),
                        Node("SLAM-01", "SLAM入口", MapNodeType.Normal, 350, 90, "SLAM-B", ("NavigationMedium", "LaserSLAM")),
                        Node("SLAM-02", "SLAM墙边点", MapNodeType.Normal, 520, 90, "SLAM-B", ("NavigationMedium", "LaserSLAM")),
                        Node("INT-N", "互斥北口", MapNodeType.Normal, 410, 175, "INT-01", ("Capacity", "1")),
                        Node("INT-C", "互斥中心", MapNodeType.Normal, 430, 250, "INT-01", ("Capacity", "1")),
                        Node("INT-S", "互斥南口", MapNodeType.Normal, 420, 315, "INT-01", ("Capacity", "1")),
                        Node("FIRE-G1", "消防门前", MapNodeType.Door, 650, 95, "FIRE-01", ("PlcPoint", "FIRE-DOOR-01")),
                        Node("FIRE-G2", "消防门后", MapNodeType.Door, 790, 95, "FIRE-01", ("PlcPoint", "FIRE-DOOR-02")),
                        Node("SPEED-IN", "限速入口", MapNodeType.Normal, 550, 270, "SPD-01", ("SpeedLimit", "0.3")),
                        Node("WEIGH-01", "自动称重点", MapNodeType.WorkStation, 665, 270, "SPD-01", ("SpeedLimit", "0.3"), ("Process", "Weighing")),
                        Node("WASH-01", "清洗/喷淋工艺点", MapNodeType.WorkStation, 735, 315, "SPD-01", ("SpeedLimit", "0.3"), ("Process", "Wash")),
                        Node("SPEED-OUT", "限速出口", MapNodeType.Normal, 790, 270, "SPD-01", ("SpeedLimit", "0.3")),
                        Node("WAIT-01", "待机位01", MapNodeType.WaitingPoint, 595, 430, "STBY-CHG", ("Capacity", "1")),
                        Node("WAIT-02", "待机位02", MapNodeType.WaitingPoint, 645, 430, "STBY-CHG", ("Capacity", "1")),
                        Node("WAIT-03", "待机位03", MapNodeType.WaitingPoint, 595, 500, "STBY-CHG", ("Capacity", "1")),
                        Node("PARK-01", "停车位01", MapNodeType.ParkingPoint, 700, 430, "STBY-CHG", ("Capacity", "1")),
                        Node("CHG-01", "充电桩01", MapNodeType.ChargeStation, 760, 455, "STBY-CHG", ("Capacity", "1"), ("RatedPowerKw", "3.3")),
                        Node("CHG-02", "充电桩02", MapNodeType.ChargeStation, 810, 455, "STBY-CHG", ("Capacity", "1"), ("RatedPowerKw", "6.6")),
                        Node("CHG-03", "快充桩03", MapNodeType.ChargeStation, 760, 510, "STBY-CHG", ("Capacity", "1"), ("RatedPowerKw", "12.0")),
                        Node("MAINT-IN", "维护区入口", MapNodeType.Normal, 360, 450, "MAINT-01", ("MaintenanceCandidate", "true")),
                        Node("MAINT-OUT", "维护区出口", MapNodeType.Normal, 500, 500, "MAINT-01", ("MaintenanceCandidate", "true"))
                    ],
                    Edges =
                    [
                        Edge("PICK-A1", "PICK-A2", 90, "CAP-A", MapEdgeType.WorkRoad, 1.2, MapEdgeDirection.Bidirectional, true, ("MaxVehicleFlow", "2")),
                        Edge("PICK-A2", "PUT-A1", 120, "CAP-A", MapEdgeType.WorkRoad, 1.2, MapEdgeDirection.Bidirectional, true, ("MaxVehicleFlow", "2")),
                        Edge("PUT-A1", "INT-N", 210, "INT-01", MapEdgeType.MainRoad, 1.5, MapEdgeDirection.Bidirectional, true, ("ControlZone", "INT-01")),
                        Edge("RAW-IN-01", "RAW-IN-02", 75, "RAW-B", MapEdgeType.WorkRoad, 1.0, MapEdgeDirection.Bidirectional, true, ("MaxVehicleFlow", "2")),
                        Edge("RAW-IN-02", "RAW-OUT-01", 105, "RAW-B", MapEdgeType.WorkRoad, 1.0, MapEdgeDirection.Bidirectional, true, ("MaxVehicleFlow", "2")),
                        Edge("RAW-OUT-01", "QR-03", 95, "QR-A", MapEdgeType.MainRoad, 1.0, MapEdgeDirection.Bidirectional, true, ("NavigationMedium", "QRCode")),
                        Edge("QR-01", "QR-02", 110, "QR-A", MapEdgeType.Normal, 1.0, MapEdgeDirection.Bidirectional, true, ("NavigationMedium", "QRCode")),
                        Edge("QR-02", "QR-03", 110, "QR-A", MapEdgeType.Normal, 1.0, MapEdgeDirection.Bidirectional, true, ("NavigationMedium", "QRCode")),
                        Edge("QR-03", "INT-S", 170, "INT-01", MapEdgeType.MainRoad, 1.2, MapEdgeDirection.Bidirectional, true, ("ControlZone", "INT-01")),
                        Edge("SLAM-01", "SLAM-02", 170, "SLAM-B", MapEdgeType.MainRoad, 1.5, MapEdgeDirection.Bidirectional, true, ("NavigationMedium", "LaserSLAM")),
                        Edge("SLAM-02", "INT-N", 170, "INT-01", MapEdgeType.MainRoad, 1.2, MapEdgeDirection.Bidirectional, true, ("ControlZone", "INT-01")),
                        Edge("INT-N", "INT-C", 75, "INT-01", MapEdgeType.Intersection, 0.8, MapEdgeDirection.Bidirectional, true, ("ZoneKind", "Interlocking")),
                        Edge("INT-C", "INT-S", 70, "INT-01", MapEdgeType.Intersection, 0.8, MapEdgeDirection.Bidirectional, true, ("ZoneKind", "Interlocking")),
                        Edge("INT-C", "FIRE-G1", 230, "FIRE-01", MapEdgeType.NarrowRoad, 1.0, MapEdgeDirection.OneWay, true, ("SafetyZone", "FIRE-01")),
                        Edge("FIRE-G1", "FIRE-G2", 140, "FIRE-01", MapEdgeType.NarrowRoad, 0.8, MapEdgeDirection.OneWay, true, ("PlcPoint", "FIRE-DOOR-01")),
                        Edge("FIRE-G2", "SPEED-IN", 190, "SPD-01", MapEdgeType.MainRoad, 1.2),
                        Edge("SPEED-IN", "WEIGH-01", 115, "SPD-01", MapEdgeType.NarrowRoad, 0.3, MapEdgeDirection.Bidirectional, true, ("Process", "Weighing")),
                        Edge("WEIGH-01", "WASH-01", 80, "SPD-01", MapEdgeType.NarrowRoad, 0.3, MapEdgeDirection.Bidirectional, true, ("Process", "WeighingWash")),
                        Edge("WASH-01", "SPEED-OUT", 65, "SPD-01", MapEdgeType.NarrowRoad, 0.3, MapEdgeDirection.Bidirectional, true, ("Process", "Wash")),
                        Edge("SPEED-OUT", "CHG-02", 200, "STBY-CHG", MapEdgeType.ChargingRoad, 0.8),
                        Edge("WAIT-01", "WAIT-02", 50, "STBY-CHG", MapEdgeType.ChargingRoad, 0.6),
                        Edge("WAIT-01", "WAIT-03", 70, "STBY-CHG", MapEdgeType.ChargingRoad, 0.6),
                        Edge("WAIT-02", "PARK-01", 55, "STBY-CHG", MapEdgeType.ChargingRoad, 0.6),
                        Edge("PARK-01", "CHG-01", 70, "STBY-CHG", MapEdgeType.ChargingRoad, 0.5),
                        Edge("CHG-01", "CHG-02", 50, "STBY-CHG", MapEdgeType.ChargingRoad, 0.5),
                        Edge("WAIT-03", "CHG-03", 165, "STBY-CHG", MapEdgeType.ChargingRoad, 0.5),
                        Edge("CHG-03", "CHG-01", 70, "STBY-CHG", MapEdgeType.ChargingRoad, 0.5),
                        Edge("INT-S", "MAINT-IN", 145, "MAINT-01", MapEdgeType.NarrowRoad, 0.8, MapEdgeDirection.Bidirectional, true, ("MaintenanceCandidate", "true")),
                        Edge("MAINT-IN", "MAINT-OUT", 150, "MAINT-01", MapEdgeType.NarrowRoad, 0.5, MapEdgeDirection.Bidirectional, false, ("StaticBlockedDemo", "true")),
                        Edge("MAINT-OUT", "WAIT-01", 140, "STBY-CHG", MapEdgeType.ChargingRoad, 0.6)
                    ],
                    VendorNodeMappings =
                    [
                        new VendorNodeMappingDto { VendorCode = "GLOBAL", SystemNodeId = "PICK-A1", VendorNodeCode = "STATION-01" },
                        new VendorNodeMappingDto { VendorCode = "GLOBAL", SystemNodeId = "RAW-IN-01", VendorNodeCode = "RAW-IN-01" },
                        new VendorNodeMappingDto { VendorCode = "HIK", SystemNodeId = "QR-02", VendorNodeCode = "HK_QR_002" },
                        new VendorNodeMappingDto { VendorCode = "HIK", SystemNodeId = "INT-C", VendorNodeCode = "HK_CROSS_01" },
                        new VendorNodeMappingDto { VendorCode = "HANGCHA", SystemNodeId = "WEIGH-01", VendorNodeCode = "HC_WEIGHT_01" },
                        new VendorNodeMappingDto { VendorCode = "HANGCHA", SystemNodeId = "CHG-01", VendorNodeCode = "HC_CHARGE_01" },
                        new VendorNodeMappingDto { VendorCode = "HANGCHA", SystemNodeId = "CHG-03", VendorNodeCode = "HC_FAST_CHARGE_03" },
                        new VendorNodeMappingDto { VendorCode = "RGV-A", SystemNodeId = "FIRE-G1", VendorNodeCode = "DOOR_SAFE_A" }
                    ]
                };

                SetCurrentVersion(snapshot, new MapVersionDto
                {
                    MapId = snapshot.MapId,
                    MapName = snapshot.MapName,
                    Version = snapshot.Version,
                    IsCurrent = true,
                    PublishedAt = now,
                    OperatorId = "system"
                });
        }

        private static string VersionKey(string mapId, string version) => $"{mapId}:{version}";

        private static MapDraftDto CloneDraft(MapDraftDto draft) => new()
        {
            DraftId = draft.DraftId,
            Map = Clone(draft.Map),
            CreatedAt = draft.CreatedAt,
            UpdatedAt = draft.UpdatedAt,
            OperatorId = draft.OperatorId
        };

        private static MapVersionDto CloneVersion(MapVersionDto version) => new()
        {
            MapId = version.MapId,
            MapName = version.MapName,
            Version = version.Version,
            IsCurrent = version.IsCurrent,
            PublishedAt = version.PublishedAt,
            OperatorId = version.OperatorId
        };

        private static MapVersionDto CopyVersion(MapVersionDto version, bool isCurrent) => new()
        {
            MapId = version.MapId,
            MapName = version.MapName,
            Version = version.Version,
            IsCurrent = isCurrent,
            PublishedAt = version.PublishedAt,
            OperatorId = version.OperatorId
        };

        private static MapSnapshotDto CopySnapshot(
            MapSnapshotDto snapshot,
            string version,
            DateTimeOffset updatedAt,
            string? mapName = null) => new()
        {
            MapId = snapshot.MapId,
            MapName = mapName ?? snapshot.MapName,
            Version = version,
            Nodes = snapshot.Nodes,
            Edges = snapshot.Edges,
            Areas = snapshot.Areas,
            VendorNodeMappings = snapshot.VendorNodeMappings,
            UpdatedAt = updatedAt
        };

        private static MapSnapshotDto Clone(MapSnapshotDto snapshot)
        {
            var json = JsonSerializer.Serialize(snapshot);
            return JsonSerializer.Deserialize<MapSnapshotDto>(json) ?? new MapSnapshotDto();
        }
    }
}
