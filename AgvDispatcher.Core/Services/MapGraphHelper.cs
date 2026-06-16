using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Services
{
    /// <summary>
    /// 地图拓扑相关的无状态辅助方法，供 <c>IMapService</c> 的各实现复用，
    /// 避免在持久化实现与 Mock 实现之间重复可达性遍历与别名解析逻辑。
    /// </summary>
    public static class MapGraphHelper
    {
        /// <summary>
        /// 从起点出发，沿可通行的边做广度优先遍历，返回包含起点在内的全部可达节点。
        /// 遍历遵循边的启用/锁定/封闭状态与方向，与默认路径规划使用同一套可通行规则。
        /// </summary>
        public static IReadOnlyList<MapNode> FindReachableNodes(
            IReadOnlyList<MapNode> nodes,
            IReadOnlyList<MapEdge> edges,
            string startNodeId)
        {
            if (string.IsNullOrWhiteSpace(startNodeId))
            {
                return Array.Empty<MapNode>();
            }

            var nodeMap = nodes
                .Where(node => node.IsEnabled)
                .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            if (!nodeMap.ContainsKey(startNodeId))
            {
                return Array.Empty<MapNode>();
            }

            var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            void AddNeighbor(string from, string to)
            {
                if (!adjacency.TryGetValue(from, out var list))
                {
                    list = new List<string>();
                    adjacency[from] = list;
                }
                list.Add(to);
            }

            foreach (var edge in edges.Where(e => e.IsEnabled && !e.IsLocked && e.Direction != EdgeDirection.Closed))
            {
                if (!nodeMap.ContainsKey(edge.FromNodeId) || !nodeMap.ContainsKey(edge.ToNodeId))
                {
                    continue;
                }

                if (edge.Direction is EdgeDirection.Bidirectional or EdgeDirection.ForwardOnly)
                {
                    AddNeighbor(edge.FromNodeId, edge.ToNodeId);
                }
                if (edge.Direction is EdgeDirection.Bidirectional or EdgeDirection.ReverseOnly)
                {
                    AddNeighbor(edge.ToNodeId, edge.FromNodeId);
                }
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startNodeId };
            var queue = new Queue<string>();
            queue.Enqueue(startNodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!adjacency.TryGetValue(current, out var neighbors))
                {
                    continue;
                }

                foreach (var next in neighbors)
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return visited.Select(id => nodeMap[id]).ToList();
        }

        /// <summary>
        /// 在别名集合中将厂商别名值解析为系统节点编号。
        /// 指定 <paramref name="brand"/> 时优先匹配该品牌限定的启用别名，未命中再回退到全局（无品牌）别名。
        /// </summary>
        public static string? ResolveNodeIdByAlias(
            IEnumerable<MapLocationAlias> aliases,
            string aliasValue,
            string? brand)
        {
            if (string.IsNullOrWhiteSpace(aliasValue))
            {
                return null;
            }

            var enabled = aliases
                .Where(a => a.IsEnabled && string.Equals(a.AliasValue, aliasValue, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrWhiteSpace(brand))
            {
                var branded = enabled.FirstOrDefault(a =>
                    !string.IsNullOrWhiteSpace(a.Brand) &&
                    string.Equals(a.Brand, brand, StringComparison.OrdinalIgnoreCase));
                if (branded != null)
                {
                    return branded.NodeId;
                }
            }

            var global = enabled.FirstOrDefault(a => string.IsNullOrWhiteSpace(a.Brand));
            return global?.NodeId;
        }
    }
}
