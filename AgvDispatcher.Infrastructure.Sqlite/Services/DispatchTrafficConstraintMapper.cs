using AgvDispatcher.Core.Contracts.Planning.Constraints;
using AgvDispatcher.Core.Contracts.Traffic.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    /// <summary>
    /// Maps a runtime traffic snapshot into the dynamic constraints consumed by the path planner.
    /// </summary>
    public static class DispatchTrafficConstraintMapper
    {
        /// <summary>
        /// Creates planning constraints while excluding resources already owned by the current task or vehicle.
        /// </summary>
        public static PathPlanConstraint Map(
            TrafficSnapshotDto snapshot,
            string taskId,
            string vehicleId)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var forbiddenNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var forbiddenEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var occupiedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var occupiedEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reservedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reservedEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var status in snapshot.Resources)
            {
                if (IsOwnedByCurrentDispatch(status, taskId, vehicleId))
                {
                    continue;
                }

                // Only node and edge resources participate in graph planning. Zones and
                // device resources remain the responsibility of traffic control/reservation.
                var target = SelectTargetSet(
                    status,
                    forbiddenNodes,
                    forbiddenEdges,
                    occupiedNodes,
                    occupiedEdges,
                    reservedNodes,
                    reservedEdges);
                if (target is not null && !string.IsNullOrWhiteSpace(status.Resource.ResourceId))
                {
                    target.Add(status.Resource.ResourceId);
                }
            }

            return new PathPlanConstraint
            {
                ForbiddenNodeIds = forbiddenNodes,
                ForbiddenEdgeIds = forbiddenEdges,
                OccupiedNodeIds = occupiedNodes,
                OccupiedEdgeIds = occupiedEdges,
                ReservedNodeIds = reservedNodes,
                ReservedEdgeIds = reservedEdges,
                AvoidOccupiedResources = true,
                AvoidReservedResources = true
            };
        }

        private static bool IsOwnedByCurrentDispatch(
            TrafficResourceStatusDto status,
            string taskId,
            string vehicleId) =>
            string.Equals(status.TaskId, taskId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status.OccupiedByAgvId, vehicleId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status.ReservedByAgvId, vehicleId, StringComparison.OrdinalIgnoreCase);

        private static HashSet<string>? SelectTargetSet(
            TrafficResourceStatusDto status,
            HashSet<string> forbiddenNodes,
            HashSet<string> forbiddenEdges,
            HashSet<string> occupiedNodes,
            HashSet<string> occupiedEdges,
            HashSet<string> reservedNodes,
            HashSet<string> reservedEdges)
        {
            var isNode = status.Resource.ResourceType == TrafficResourceType.Node;
            var isEdge = status.Resource.ResourceType == TrafficResourceType.Edge;
            if (!isNode && !isEdge)
            {
                return null;
            }

            return status.State switch
            {
                TrafficResourceState.Occupied => isNode ? occupiedNodes : occupiedEdges,
                TrafficResourceState.Reserved or TrafficResourceState.Locked =>
                    isNode ? reservedNodes : reservedEdges,
                TrafficResourceState.Blocked or TrafficResourceState.Disabled =>
                    isNode ? forbiddenNodes : forbiddenEdges,
                _ => null
            };
        }
    }
}
