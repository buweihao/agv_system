using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Services
{
    public class DijkstraPathPlanningService : IPathPlanningService
    {
        public PlannedPath PlanPath(
            IReadOnlyList<MapNode> nodes,
            IReadOnlyList<MapEdge> edges,
            string startNodeId,
            string endNodeId)
        {
            // 无约束规划：转调约束重载，传入一个不限制品牌/能力的请求，保持原有行为不变。
            return PlanPath(nodes, edges, new PathPlanningRequest
            {
                StartNodeId = startNodeId,
                EndNodeId = endNodeId
            });
        }

        public PlannedPath PlanPath(
            IReadOnlyList<MapNode> nodes,
            IReadOnlyList<MapEdge> edges,
            PathPlanningRequest request)
        {
            var startNodeId = request.StartNodeId;
            var endNodeId = request.EndNodeId;

            if (string.IsNullOrWhiteSpace(startNodeId) || string.IsNullOrWhiteSpace(endNodeId))
            {
                return PlannedPath.Unavailable(startNodeId, endNodeId, "Start or end node is empty.");
            }

            var nodeMap = nodes
                .Where(node => IsNodePassable(node, request))
                .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            if (!nodeMap.ContainsKey(startNodeId))
            {
                return PlannedPath.Unavailable(startNodeId, endNodeId, $"Start node '{startNodeId}' does not exist or is not passable.");
            }

            if (!nodeMap.ContainsKey(endNodeId))
            {
                return PlannedPath.Unavailable(startNodeId, endNodeId, $"End node '{endNodeId}' does not exist or is not passable.");
            }

            var adjacency = BuildAdjacency(edges, nodeMap, request);
            var distance = nodeMap.Keys.ToDictionary(nodeId => nodeId, _ => double.PositiveInfinity, StringComparer.OrdinalIgnoreCase);
            var previousNode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var previousEdge = new Dictionary<string, MapEdge>(StringComparer.OrdinalIgnoreCase);
            var queue = new PriorityQueue<string, double>();

            distance[startNodeId] = 0;
            queue.Enqueue(startNodeId, 0);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (string.Equals(current, endNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!adjacency.TryGetValue(current, out var neighbors))
                {
                    continue;
                }

                foreach (var (nextNodeId, edge, cost) in neighbors)
                {
                    var nextDistance = distance[current] + cost;
                    if (nextDistance >= distance[nextNodeId])
                    {
                        continue;
                    }

                    distance[nextNodeId] = nextDistance;
                    previousNode[nextNodeId] = current;
                    previousEdge[nextNodeId] = edge;
                    queue.Enqueue(nextNodeId, nextDistance);
                }
            }

            if (!previousNode.ContainsKey(endNodeId) && !string.Equals(startNodeId, endNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return PlannedPath.Unavailable(startNodeId, endNodeId, "No available path.");
            }

            var pathNodeIds = RebuildNodeIds(startNodeId, endNodeId, previousNode);
            var pathEdges = RebuildEdges(startNodeId, endNodeId, previousNode, previousEdge);

            return new PlannedPath
            {
                StartNodeId = startNodeId,
                EndNodeId = endNodeId,
                Nodes = pathNodeIds.Select(nodeId => nodeMap[nodeId]).ToArray(),
                Edges = pathEdges,
                TotalLength = pathEdges.Sum(edge => edge.Length > 0 ? edge.Length : edge.Cost),
                IsAvailable = true,
                Message = "Path planned."
            };
        }

        /// <summary>节点是否可通行：受请求中的禁用策略、品牌与能力约束共同决定。</summary>
        private static bool IsNodePassable(MapNode node, PathPlanningRequest request)
        {
            if (request.RespectDisabled && !node.IsEnabled)
            {
                return false;
            }

            if (!IsBrandAllowed(node.AllowedBrands, request.Brand))
            {
                return false;
            }

            // 节点要求的能力必须被车辆能力完全包含。
            if (node.RequiredCapabilities != VehicleCapability.None &&
                (request.Capabilities & node.RequiredCapabilities) != node.RequiredCapabilities)
            {
                return false;
            }

            return true;
        }

        /// <summary>边是否可通行：受请求中的锁定/封闭/禁用策略与品牌约束共同决定。</summary>
        private static bool IsEdgePassable(MapEdge edge, PathPlanningRequest request)
        {
            if (request.RespectDisabled && !edge.IsEnabled)
            {
                return false;
            }

            if (request.RespectLocked && edge.IsLocked)
            {
                return false;
            }

            if (request.RespectClosed && edge.Direction == EdgeDirection.Closed)
            {
                return false;
            }

            if (!IsBrandAllowed(edge.AllowedBrands, request.Brand))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判断品牌是否被允许：<paramref name="allowedBrands"/> 为空表示不限制（始终允许）；
        /// 否则按 <c>,</c>/<c>;</c> 分割，与 <paramref name="brand"/> 不区分大小写比较。
        /// 当限制非空而未提供品牌时，按不允许处理。
        /// </summary>
        private static bool IsBrandAllowed(string allowedBrands, string? brand)
        {
            if (string.IsNullOrWhiteSpace(allowedBrands))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(brand))
            {
                return false;
            }

            var allowed = allowedBrands.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            return allowed.Any(b => string.Equals(b.Trim(), brand, StringComparison.OrdinalIgnoreCase));
        }

        private static Dictionary<string, List<(string NextNodeId, MapEdge Edge, double Cost)>> BuildAdjacency(
            IReadOnlyList<MapEdge> edges,
            IReadOnlyDictionary<string, MapNode> nodes,
            PathPlanningRequest request)
        {
            var adjacency = new Dictionary<string, List<(string, MapEdge, double)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var edge in edges.Where(edge => IsEdgePassable(edge, request)))
            {
                if (!nodes.ContainsKey(edge.FromNodeId) || !nodes.ContainsKey(edge.ToNodeId))
                {
                    continue;
                }

                var cost = edge.Length > 0 ? edge.Length : Math.Max(edge.Cost, 1);

                if (edge.Direction is EdgeDirection.Bidirectional or EdgeDirection.ForwardOnly)
                {
                    AddNeighbor(adjacency, edge.FromNodeId, edge.ToNodeId, edge, cost);
                }

                if (edge.Direction is EdgeDirection.Bidirectional or EdgeDirection.ReverseOnly)
                {
                    AddNeighbor(adjacency, edge.ToNodeId, edge.FromNodeId, edge, cost);
                }
            }

            return adjacency;
        }

        private static void AddNeighbor(
            Dictionary<string, List<(string, MapEdge, double)>> adjacency,
            string fromNodeId,
            string toNodeId,
            MapEdge edge,
            double cost)
        {
            if (!adjacency.TryGetValue(fromNodeId, out var neighbors))
            {
                neighbors = new List<(string, MapEdge, double)>();
                adjacency[fromNodeId] = neighbors;
            }

            neighbors.Add((toNodeId, edge, cost));
        }

        private static IReadOnlyList<string> RebuildNodeIds(
            string startNodeId,
            string endNodeId,
            IReadOnlyDictionary<string, string> previousNode)
        {
            var result = new List<string> { endNodeId };
            var current = endNodeId;

            while (!string.Equals(current, startNodeId, StringComparison.OrdinalIgnoreCase))
            {
                current = previousNode[current];
                result.Add(current);
            }

            result.Reverse();
            return result;
        }

        private static IReadOnlyList<MapEdge> RebuildEdges(
            string startNodeId,
            string endNodeId,
            IReadOnlyDictionary<string, string> previousNode,
            IReadOnlyDictionary<string, MapEdge> previousEdge)
        {
            var result = new List<MapEdge>();
            var current = endNodeId;

            while (!string.Equals(current, startNodeId, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(previousEdge[current]);
                current = previousNode[current];
            }

            result.Reverse();
            return result;
        }
    }
}
