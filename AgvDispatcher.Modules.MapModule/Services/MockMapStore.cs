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
            var now = DateTimeOffset.Now;
            var snapshot = new MapSnapshotDto
            {
                MapId = "MAIN",
                MapName = "AGV 主地图",
                Version = "1.0.0",
                UpdatedAt = now,
                Areas =
                [
                    new MapAreaDto
                    {
                        AreaId = "A",
                        AreaName = "作业区 A",
                        AreaType = MapAreaType.WorkArea,
                        BoundaryPoints =
                        [
                            new MapPointDto { X = 0, Y = 0 },
                            new MapPointDto { X = 500, Y = 0 },
                            new MapPointDto { X = 500, Y = 500 },
                            new MapPointDto { X = 0, Y = 500 }
                        ],
                        Properties = new Dictionary<string, string> { ["Capacity"] = "4" }
                    },
                    new MapAreaDto
                    {
                        AreaId = "B",
                        AreaName = "缓存区 B",
                        AreaType = MapAreaType.WaitingArea,
                        BoundaryPoints =
                        [
                            new MapPointDto { X = 500, Y = 0 },
                            new MapPointDto { X = 850, Y = 0 },
                            new MapPointDto { X = 850, Y = 500 },
                            new MapPointDto { X = 500, Y = 500 }
                        ],
                        Properties = new Dictionary<string, string> { ["Capacity"] = "3" }
                    }
                ],
                Nodes =
                [
                    new MapNodeDto { NodeId = "P1", NodeCode = "P1", NodeName = "P1取货点", NodeType = MapNodeType.PickPoint, X = 80, Y = 80, AreaId = "A", Enabled = true, Properties = new Dictionary<string, string> { ["Capacity"] = "1" } },
                    new MapNodeDto { NodeId = "P2", NodeCode = "P2", NodeName = "P2取货点", NodeType = MapNodeType.PickPoint, X = 80, Y = 300, AreaId = "A", Enabled = true, Properties = new Dictionary<string, string> { ["Capacity"] = "1" } },
                    new MapNodeDto { NodeId = "X1", NodeCode = "X1", NodeName = "中央路口", NodeType = MapNodeType.Normal, X = 400, Y = 200, AreaId = "A", Enabled = true, Properties = new Dictionary<string, string> { ["Capacity"] = "1" } },
                    new MapNodeDto { NodeId = "W1", NodeCode = "W1", NodeName = "等待点", NodeType = MapNodeType.WaitingPoint, X = 400, Y = 420, AreaId = "A", Enabled = true, Properties = new Dictionary<string, string> { ["Capacity"] = "2" } },
                    new MapNodeDto { NodeId = "D1", NodeCode = "D1", NodeName = "D1放货点", NodeType = MapNodeType.PutPoint, X = 720, Y = 80, AreaId = "B", Enabled = true, Properties = new Dictionary<string, string> { ["Capacity"] = "1", ["AllowedBrands"] = "RGV-A" } },
                    new MapNodeDto { NodeId = "Charge-1", NodeCode = "Charge-1", NodeName = "1号充电位", NodeType = MapNodeType.ChargeStation, X = 720, Y = 480, AreaId = "B", Enabled = true, Properties = new Dictionary<string, string> { ["Capacity"] = "1" } }
                ],
                Edges =
                [
                    new MapEdgeDto { EdgeId = "E-P1-X1", FromNodeId = "P1", ToNodeId = "X1", Direction = MapEdgeDirection.Bidirectional, Distance = 360, Enabled = true, AreaId = "A", SpeedLimit = 1.5, Properties = new Dictionary<string, string> { ["MaxVehicleFlow"] = "1" } },
                    new MapEdgeDto { EdgeId = "E-P2-X1", FromNodeId = "P2", ToNodeId = "X1", Direction = MapEdgeDirection.Bidirectional, Distance = 340, Enabled = true, AreaId = "A", SpeedLimit = 1.5, Properties = new Dictionary<string, string> { ["MaxVehicleFlow"] = "1" } },
                    new MapEdgeDto { EdgeId = "E-X1-D1", FromNodeId = "X1", ToNodeId = "D1", Direction = MapEdgeDirection.OneWay, Distance = 360, Enabled = true, AreaId = "B", SpeedLimit = 1.5, Properties = new Dictionary<string, string> { ["MaxVehicleFlow"] = "1" } },
                    new MapEdgeDto { EdgeId = "E-X1-W1", FromNodeId = "X1", ToNodeId = "W1", Direction = MapEdgeDirection.Bidirectional, Distance = 220, Enabled = true, AreaId = "A", SpeedLimit = 1.0, Properties = new Dictionary<string, string> { ["MaxVehicleFlow"] = "1" } }
                ],
                VendorNodeMappings =
                [
                    new VendorNodeMappingDto { VendorCode = "GLOBAL", SystemNodeId = "P1", VendorNodeCode = "STATION-01" },
                    new VendorNodeMappingDto { VendorCode = "RGV-A", SystemNodeId = "D1", VendorNodeCode = "DOCK-A" }
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
