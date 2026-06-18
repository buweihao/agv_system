using AgvDispatcher.Core.Contracts.Common;
using AgvDispatcher.Core.Contracts.Map;
using AgvDispatcher.Core.Contracts.Planning.Constraints;
using AgvDispatcher.Core.Contracts.Planning.Enums;
using AgvDispatcher.Core.Contracts.Planning.Interfaces;
using AgvDispatcher.Core.Contracts.Planning.Requests;
using AgvDispatcher.Core.Contracts.Planning.Results;

namespace AgvDispatcher.Infrastructure.Mock.Planning
{
    /// <summary>
    /// Basic in-memory Dijkstra planner. All map and dynamic state must be supplied by the caller.
    /// </summary>
    public sealed class DijkstraPathPlanner : IPathPlanner
    {
        private const double DefaultSpeed = 1.0;

        public Task<AgvResult<PathPlanResult>> PlanAsync(
            PathPlanRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Plan(request, cancellationToken));
        }

        public async Task<AgvResult<IReadOnlyList<PathPlanResult>>> PlanAlternativesAsync(
            PathPlanRequest request,
            CancellationToken cancellationToken = default)
        {
            var planResult = await PlanAsync(request, cancellationToken).ConfigureAwait(false);
            if (!planResult.Success || planResult.Data is null)
            {
                return AgvResult<IReadOnlyList<PathPlanResult>>.Fail(
                    planResult.Code,
                    planResult.Message,
                    planResult.Retryable);
            }

            var plan = planResult.Data;
            if (request.MaxAlternativeCount > 1)
            {
                plan = CopyWithWarning(plan, "Alternative path generation is not implemented; only the primary path is returned.");
            }

            return AgvResult<IReadOnlyList<PathPlanResult>>.Ok(new[] { plan });
        }

        public async Task<AgvResult<PathReachabilityResult>> CheckReachabilityAsync(
            PathReachabilityRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return AgvResult<PathReachabilityResult>.Fail(FailureCode.InvalidRequest, "Request is required.");
            }

            var planResult = await PlanAsync(new PathPlanRequest
            {
                Context = request.Context,
                MapSnapshot = request.MapSnapshot,
                VehicleId = request.VehicleId,
                StartNodeId = request.StartNodeId,
                TargetNodeId = request.TargetNodeId,
                Constraint = request.Constraint
            }, cancellationToken).ConfigureAwait(false);

            if (!planResult.Success || planResult.Data is null)
            {
                return AgvResult<PathReachabilityResult>.Ok(new PathReachabilityResult
                {
                    StartNodeId = request.StartNodeId,
                    TargetNodeId = request.TargetNodeId,
                    IsReachable = false,
                    Reason = planResult.Message
                });
            }

            var plan = planResult.Data;
            return AgvResult<PathReachabilityResult>.Ok(new PathReachabilityResult
            {
                StartNodeId = request.StartNodeId,
                TargetNodeId = request.TargetNodeId,
                IsReachable = plan.IsReachable,
                EstimatedDistance = plan.IsReachable ? plan.TotalDistance : null,
                EstimatedSeconds = plan.IsReachable ? plan.EstimatedSeconds : null,
                Reason = plan.IsReachable ? null : "No available path satisfies the supplied map and constraints."
            });
        }

        public async Task<AgvResult<NearestNodeResult>> FindNearestReachableNodeAsync(
            NearestNodeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                return AgvResult<NearestNodeResult>.Fail(FailureCode.InvalidRequest, "Request is required.");
            }

            if (request.CandidateNodeIds is null || request.CandidateNodeIds.Count == 0)
            {
                return AgvResult<NearestNodeResult>.Fail(FailureCode.InvalidRequest, "At least one candidate node is required.");
            }

            PathPlanResult? nearest = null;
            foreach (var candidateNodeId in request.CandidateNodeIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var planResult = await PlanAsync(new PathPlanRequest
                {
                    Context = request.Context,
                    MapSnapshot = request.MapSnapshot,
                    VehicleId = request.VehicleId,
                    StartNodeId = request.FromNodeId,
                    TargetNodeId = candidateNodeId,
                    Constraint = request.Constraint
                }, cancellationToken).ConfigureAwait(false);

                if (!planResult.Success || planResult.Data is not { IsReachable: true } plan)
                {
                    continue;
                }

                if (nearest is null || plan.TotalDistance < nearest.TotalDistance)
                {
                    nearest = plan;
                }
            }

            return AgvResult<NearestNodeResult>.Ok(new NearestNodeResult
            {
                FromNodeId = request.FromNodeId,
                NearestNodeId = nearest?.TargetNodeId,
                Found = nearest is not null,
                Distance = nearest?.TotalDistance,
                EstimatedSeconds = nearest?.EstimatedSeconds,
                Path = nearest
            });
        }

        private static AgvResult<PathPlanResult> Plan(PathPlanRequest request, CancellationToken cancellationToken)
        {
            var validationFailure = Validate(request);
            if (validationFailure is not null)
            {
                return validationFailure;
            }

            var snapshot = request.MapSnapshot!;
            var nodes = snapshot.Nodes
                .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            if (!nodes.TryGetValue(request.StartNodeId, out var startNode))
            {
                return AgvResult<PathPlanResult>.Fail(
                    FailureCode.MapNodeNotFound,
                    $"Start node '{request.StartNodeId}' was not found.");
            }

            if (!nodes.TryGetValue(request.TargetNodeId, out var targetNode))
            {
                return AgvResult<PathPlanResult>.Fail(
                    FailureCode.MapNodeNotFound,
                    $"Target node '{request.TargetNodeId}' was not found.");
            }

            if (!startNode.Enabled)
            {
                return AgvResult<PathPlanResult>.Fail(
                    FailureCode.MapNodeDisabled,
                    $"Start node '{request.StartNodeId}' is disabled.");
            }

            if (!targetNode.Enabled)
            {
                return AgvResult<PathPlanResult>.Fail(
                    FailureCode.MapNodeDisabled,
                    $"Target node '{request.TargetNodeId}' is disabled.");
            }

            var constraint = request.Constraint ?? new PathPlanConstraint();
            var traversableNodes = nodes.Values
                .Where(node => node.Enabled && !IsNodeBlocked(node.NodeId, constraint))
                .ToDictionary(node => node.NodeId, StringComparer.OrdinalIgnoreCase);
            var adjacency = BuildAdjacency(snapshot.Edges, traversableNodes, constraint);
            var routeStops = request.WaypointNodeIds
                .Concat(new[] { request.TargetNodeId })
                .ToArray();

            var route = new List<Traversal>();
            var currentNodeId = request.StartNodeId;
            foreach (var routeStop in routeStops)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!nodes.ContainsKey(routeStop))
                {
                    return AgvResult<PathPlanResult>.Fail(
                        FailureCode.MapNodeNotFound,
                        $"Waypoint or target node '{routeStop}' was not found.");
                }

                var leg = FindShortestPath(currentNodeId, routeStop, traversableNodes, adjacency, cancellationToken);
                if (leg is null)
                {
                    return AgvResult<PathPlanResult>.Ok(CreateUnreachableResult(request, snapshot.Version));
                }

                route.AddRange(leg);
                currentNodeId = routeStop;
            }

            var segments = route.Select((step, index) => new PathSegmentDto
            {
                Sequence = index + 1,
                FromNodeId = step.FromNodeId,
                ToNodeId = step.ToNodeId,
                EdgeId = step.Edge.EdgeId,
                Distance = step.Edge.Distance,
                EstimatedSeconds = EstimateSeconds(step.Edge),
                Direction = MoveDirection.Forward,
                IsReverseMove = false,
                SpeedLimit = step.Edge.SpeedLimit
            }).ToArray();
            var totalDistance = route.Sum(step => step.Edge.Distance);
            var totalSeconds = route.Sum(step => EstimateSeconds(step.Edge));
            var totalCost = route.Sum(step => GetWeight(step.Edge));

            return AgvResult<PathPlanResult>.Ok(new PathPlanResult
            {
                PlanId = Guid.NewGuid().ToString("N"),
                StartNodeId = request.StartNodeId,
                TargetNodeId = request.TargetNodeId,
                Segments = segments,
                TotalDistance = totalDistance,
                EstimatedSeconds = totalSeconds,
                TurnCount = 0,
                Cost = new PathPlanCost
                {
                    DistanceCost = totalDistance,
                    TimeCost = totalSeconds,
                    TotalCost = totalCost
                },
                MapVersion = snapshot.Version,
                IsReachable = true,
                Warnings = Array.Empty<string>()
            });
        }

        private static AgvResult<PathPlanResult>? Validate(PathPlanRequest request)
        {
            if (request is null)
            {
                return AgvResult<PathPlanResult>.Fail(FailureCode.InvalidRequest, "Request is required.");
            }

            if (request.MapSnapshot is null)
            {
                return AgvResult<PathPlanResult>.Fail(FailureCode.MapNotLoaded, "Map snapshot is required.");
            }

            if (string.IsNullOrWhiteSpace(request.StartNodeId))
            {
                return AgvResult<PathPlanResult>.Fail(FailureCode.InvalidRequest, "Start node id is required.");
            }

            if (string.IsNullOrWhiteSpace(request.TargetNodeId))
            {
                return AgvResult<PathPlanResult>.Fail(FailureCode.InvalidRequest, "Target node id is required.");
            }

            if (request.MapSnapshot.Nodes is null)
            {
                return AgvResult<PathPlanResult>.Fail(FailureCode.InvalidRequest, "Map nodes are required.");
            }

            return null;
        }

        private static Dictionary<string, List<Traversal>> BuildAdjacency(
            IReadOnlyList<MapEdgeDto>? edges,
            IReadOnlyDictionary<string, MapNodeDto> nodes,
            PathPlanConstraint constraint)
        {
            var adjacency = new Dictionary<string, List<Traversal>>(StringComparer.OrdinalIgnoreCase);
            if (edges is null)
            {
                return adjacency;
            }

            foreach (var edge in edges)
            {
                if (!edge.Enabled || IsEdgeBlocked(edge.EdgeId, constraint) ||
                    !nodes.ContainsKey(edge.FromNodeId) || !nodes.ContainsKey(edge.ToNodeId))
                {
                    continue;
                }

                if (edge.Direction is MapEdgeDirection.OneWay or MapEdgeDirection.Bidirectional)
                {
                    AddTraversal(adjacency, edge.FromNodeId, edge.ToNodeId, edge);
                }

                if (edge.Direction == MapEdgeDirection.Bidirectional)
                {
                    AddTraversal(adjacency, edge.ToNodeId, edge.FromNodeId, edge);
                }
            }

            return adjacency;
        }

        private static IReadOnlyList<Traversal>? FindShortestPath(
            string startNodeId,
            string targetNodeId,
            IReadOnlyDictionary<string, MapNodeDto> nodes,
            IReadOnlyDictionary<string, List<Traversal>> adjacency,
            CancellationToken cancellationToken)
        {
            if (!nodes.ContainsKey(startNodeId) || !nodes.ContainsKey(targetNodeId))
            {
                return null;
            }

            if (string.Equals(startNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<Traversal>();
            }

            var distances = nodes.Keys.ToDictionary(id => id, _ => double.PositiveInfinity, StringComparer.OrdinalIgnoreCase);
            var previous = new Dictionary<string, Traversal>(StringComparer.OrdinalIgnoreCase);
            var queue = new PriorityQueue<string, double>();
            distances[startNodeId] = 0;
            queue.Enqueue(startNodeId, 0);

            while (queue.TryDequeue(out var currentNodeId, out var queuedDistance))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (queuedDistance > distances[currentNodeId])
                {
                    continue;
                }

                if (string.Equals(currentNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!adjacency.TryGetValue(currentNodeId, out var neighbors))
                {
                    continue;
                }

                foreach (var traversal in neighbors)
                {
                    var candidateDistance = distances[currentNodeId] + GetWeight(traversal.Edge);
                    if (candidateDistance >= distances[traversal.ToNodeId])
                    {
                        continue;
                    }

                    distances[traversal.ToNodeId] = candidateDistance;
                    previous[traversal.ToNodeId] = traversal;
                    queue.Enqueue(traversal.ToNodeId, candidateDistance);
                }
            }

            if (!previous.ContainsKey(targetNodeId))
            {
                return null;
            }

            var route = new List<Traversal>();
            var current = targetNodeId;
            while (!string.Equals(current, startNodeId, StringComparison.OrdinalIgnoreCase))
            {
                var traversal = previous[current];
                route.Add(traversal);
                current = traversal.FromNodeId;
            }

            route.Reverse();
            return route;
        }

        private static void AddTraversal(
            IDictionary<string, List<Traversal>> adjacency,
            string fromNodeId,
            string toNodeId,
            MapEdgeDto edge)
        {
            if (!adjacency.TryGetValue(fromNodeId, out var traversals))
            {
                traversals = new List<Traversal>();
                adjacency[fromNodeId] = traversals;
            }

            traversals.Add(new Traversal(fromNodeId, toNodeId, edge));
        }

        private static bool IsNodeBlocked(string nodeId, PathPlanConstraint constraint)
        {
            return Contains(constraint.ForbiddenNodeIds, nodeId) ||
                   (constraint.AvoidOccupiedResources && Contains(constraint.OccupiedNodeIds, nodeId)) ||
                   (constraint.AvoidReservedResources && Contains(constraint.ReservedNodeIds, nodeId));
        }

        private static bool IsEdgeBlocked(string edgeId, PathPlanConstraint constraint)
        {
            return Contains(constraint.ForbiddenEdgeIds, edgeId) ||
                   (constraint.AvoidOccupiedResources && Contains(constraint.OccupiedEdgeIds, edgeId)) ||
                   (constraint.AvoidReservedResources && Contains(constraint.ReservedEdgeIds, edgeId));
        }

        private static bool Contains(IReadOnlySet<string>? values, string value)
        {
            return values?.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static double GetWeight(MapEdgeDto edge)
        {
            if (double.IsFinite(edge.Cost) && edge.Cost > 0)
            {
                return edge.Cost;
            }

            if (double.IsFinite(edge.Distance) && edge.Distance >= 0)
            {
                return edge.Distance;
            }

            return double.MaxValue;
        }

        private static double EstimateSeconds(MapEdgeDto edge)
        {
            var speed = edge.SpeedLimit is > 0 and var speedLimit && double.IsFinite(speedLimit)
                ? speedLimit
                : DefaultSpeed;
            return edge.Distance > 0 && double.IsFinite(edge.Distance) ? edge.Distance / speed : 0;
        }

        private static PathPlanResult CreateUnreachableResult(PathPlanRequest request, string mapVersion)
        {
            return new PathPlanResult
            {
                PlanId = Guid.NewGuid().ToString("N"),
                StartNodeId = request.StartNodeId,
                TargetNodeId = request.TargetNodeId,
                Segments = Array.Empty<PathSegmentDto>(),
                Cost = new PathPlanCost(),
                MapVersion = mapVersion,
                IsReachable = false,
                Warnings = new[] { "No available path satisfies the supplied map and constraints." }
            };
        }

        private static PathPlanResult CopyWithWarning(PathPlanResult source, string warning)
        {
            return new PathPlanResult
            {
                PlanId = source.PlanId,
                StartNodeId = source.StartNodeId,
                TargetNodeId = source.TargetNodeId,
                Segments = source.Segments,
                TotalDistance = source.TotalDistance,
                EstimatedSeconds = source.EstimatedSeconds,
                TurnCount = source.TurnCount,
                Cost = source.Cost,
                MapVersion = source.MapVersion,
                IsReachable = source.IsReachable,
                Warnings = (source.Warnings ?? Array.Empty<string>()).Concat(new[] { warning }).ToArray()
            };
        }

        private sealed record Traversal(string FromNodeId, string ToNodeId, MapEdgeDto Edge);
    }
}
