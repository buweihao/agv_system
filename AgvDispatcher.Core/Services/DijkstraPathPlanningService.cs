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
            if (string.IsNullOrWhiteSpace(startNodeId) || string.IsNullOrWhiteSpace(endNodeId))
            {
                return PlannedPath.Unavailable(startNodeId, endNodeId, "Start or end node is empty.");
            }

            var nodeMap = nodes
                .Where(node => node.IsEnabled)
                .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            if (!nodeMap.ContainsKey(startNodeId))
            {
                return PlannedPath.Unavailable(startNodeId, endNodeId, $"Start node '{startNodeId}' does not exist or is disabled.");
            }

            if (!nodeMap.ContainsKey(endNodeId))
            {
                return PlannedPath.Unavailable(startNodeId, endNodeId, $"End node '{endNodeId}' does not exist or is disabled.");
            }

            var adjacency = BuildAdjacency(edges, nodeMap);
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

        private static Dictionary<string, List<(string NextNodeId, MapEdge Edge, double Cost)>> BuildAdjacency(
            IReadOnlyList<MapEdge> edges,
            IReadOnlyDictionary<string, MapNode> nodes)
        {
            var adjacency = new Dictionary<string, List<(string, MapEdge, double)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var edge in edges.Where(edge => edge.IsEnabled && edge.Direction != EdgeDirection.Closed))
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
