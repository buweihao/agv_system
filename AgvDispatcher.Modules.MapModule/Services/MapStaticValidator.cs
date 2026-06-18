using System;
using System.Collections.Generic;
using System.Linq;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.MapManagement.Results;

namespace AgvDispatcher.Modules.MapModule.Services
{
    /// <summary>
    /// 只做静态地图结构一致性校验，不做路径规划、可达性、交通资源或车辆状态判断。
    /// </summary>
    public sealed class MapStaticValidator
    {
        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
        private static readonly string[] RuntimeFieldNames =
        [
            "IsOccupied",
            "OccupiedBy",
            "CurrentAgvId",
            "CurrentVehicleId",
            "IsLocked",
            "ReservationId",
            "TrafficLock",
            "TrafficLockState"
        ];

        public MapValidationResultDto Validate(MapSnapshotDto map, IEnumerable<string>? payloadFieldNames = null)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            AddRuntimeFieldMessages(payloadFieldNames, errors);

            if (string.IsNullOrWhiteSpace(map.MapId))
            {
                errors.Add("P0 Map: MapId 不能为空。");
            }

            if (map.Nodes.Count == 0)
            {
                errors.Add("P0 Map: 地图至少需要配置一个静态点位。");
            }

            if (map.Edges.Count == 0)
            {
                warnings.Add("P1 Map: 地图没有配置任何静态路线。");
            }

            ValidateIds("Area", map.Areas.Select(area => area.AreaId), errors);
            ValidateIds("Node", map.Nodes.Select(node => node.NodeId), errors);
            ValidateIds("Edge", map.Edges.Select(edge => edge.EdgeId), errors);

            var areaIds = map.Areas
                .Where(area => !string.IsNullOrWhiteSpace(area.AreaId))
                .Select(area => area.AreaId)
                .ToHashSet(KeyComparer);
            var nodeIds = map.Nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.NodeId))
                .Select(node => node.NodeId)
                .ToHashSet(KeyComparer);

            foreach (var node in map.Nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.AreaId) && !areaIds.Contains(node.AreaId))
                {
                    errors.Add($"P0 Node {node.NodeId}: 所属区域 {node.AreaId} 不存在。");
                }

                if (TryGetInt(node.Properties, "Capacity", out var capacity) && capacity < 0)
                {
                    errors.Add($"P0 Node {node.NodeId}: 点位容量不能为负数。");
                }
            }

            foreach (var edge in map.Edges)
            {
                if (!nodeIds.Contains(edge.FromNodeId))
                {
                    errors.Add($"P0 Edge {edge.EdgeId}: 起点 {edge.FromNodeId} 不存在。");
                }

                if (!nodeIds.Contains(edge.ToNodeId))
                {
                    errors.Add($"P0 Edge {edge.EdgeId}: 终点 {edge.ToNodeId} 不存在。");
                }

                if (!string.IsNullOrWhiteSpace(edge.FromNodeId)
                    && KeyComparer.Equals(edge.FromNodeId, edge.ToNodeId))
                {
                    errors.Add($"P0 Edge {edge.EdgeId}: 起点和终点不能相同。");
                }

                if (!string.IsNullOrWhiteSpace(edge.AreaId) && !areaIds.Contains(edge.AreaId))
                {
                    errors.Add($"P0 Edge {edge.EdgeId}: 所属区域 {edge.AreaId} 不存在。");
                }

                if (edge.Distance <= 0)
                {
                    warnings.Add($"P1 Edge {edge.EdgeId}: 路线距离未配置或小于等于 0。");
                }

                if (TryGetInt(edge.Properties, "MaxVehicleFlow", out var flow) && flow < 0)
                {
                    errors.Add($"P0 Edge {edge.EdgeId}: 最大车流量不能为负数。");
                }
            }

            foreach (var area in map.Areas)
            {
                if (TryGetInt(area.Properties, "Capacity", out var capacity) && capacity < 0)
                {
                    errors.Add($"P0 Area {area.AreaId}: 区域容量不能为负数。");
                }
            }

            foreach (var mapping in map.VendorNodeMappings)
            {
                if (!nodeIds.Contains(mapping.SystemNodeId))
                {
                    errors.Add($"P0 VendorMapping {mapping.VendorCode}/{mapping.VendorNodeCode}: 系统点位 {mapping.SystemNodeId} 不存在。");
                }
            }

            var duplicateVendorCodes = map.VendorNodeMappings
                .Where(mapping => !string.IsNullOrWhiteSpace(mapping.VendorCode)
                    && !string.IsNullOrWhiteSpace(mapping.VendorNodeCode))
                .GroupBy(mapping => $"{mapping.VendorCode}:{mapping.VendorNodeCode}", KeyComparer)
                .Where(group => group.Count() > 1);
            foreach (var duplicate in duplicateVendorCodes)
            {
                errors.Add($"P0 VendorMapping {duplicate.Key}: 同一厂商点位编码不能重复。");
            }

            AddTopologyWarnings(map, warnings);

            return new MapValidationResultDto
            {
                IsValid = errors.Count == 0,
                Messages = errors.Concat(warnings).ToList()
            };
        }

        private static void ValidateIds(string elementType, IEnumerable<string> ids, ICollection<string> errors)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"P0 {elementType}: ID 不能为空。");
                }
            }

            var duplicates = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, KeyComparer)
                .Where(group => group.Count() > 1);
            foreach (var duplicate in duplicates)
            {
                errors.Add($"P0 {elementType} {duplicate.Key}: ID 不能重复。");
            }
        }

        private static void AddTopologyWarnings(MapSnapshotDto map, ICollection<string> warnings)
        {
            var usedNodes = map.Edges
                .SelectMany(edge => new[] { edge.FromNodeId, edge.ToNodeId })
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(KeyComparer);

            foreach (var node in map.Nodes.Where(node => !usedNodes.Contains(node.NodeId)))
            {
                warnings.Add($"P1 Node {node.NodeId}: 静态节点没有被任何路线引用。");
            }

            var usedAreas = map.Nodes.Select(node => node.AreaId)
                .Concat(map.Edges.Select(edge => edge.AreaId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(KeyComparer);
            foreach (var area in map.Areas.Where(area => !usedAreas.Contains(area.AreaId)))
            {
                warnings.Add($"P1 Area {area.AreaId}: 区域没有被点位或路线引用。");
            }
        }

        private static void AddRuntimeFieldMessages(IEnumerable<string>? fieldNames, ICollection<string> errors)
        {
            if (fieldNames is null)
            {
                return;
            }

            foreach (var fieldName in fieldNames.Distinct(KeyComparer))
            {
                if (RuntimeFieldNames.Any(name => KeyComparer.Equals(name, fieldName)))
                {
                    errors.Add($"P0 Payload: 导入内容包含运行态字段 {fieldName}，地图 DTO 只能保存静态配置。");
                }
            }
        }

        private static bool TryGetInt(
            IReadOnlyDictionary<string, string> properties,
            string key,
            out int value)
        {
            value = 0;
            return properties.TryGetValue(key, out var raw)
                && int.TryParse(raw, out value);
        }
    }
}
