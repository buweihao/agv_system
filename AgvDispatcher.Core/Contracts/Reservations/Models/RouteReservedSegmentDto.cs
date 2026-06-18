using AgvDispatcher.Core.Contracts.Planning.Results;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    public sealed class RouteReservedSegmentDto
    {
        public PathSegmentDto Segment { get; init; } = new PathSegmentDto();

        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        public IReadOnlyList<string> TrafficReservationIds { get; init; } = Array.Empty<string>();

        public bool IsReserved { get; init; }

        public bool IsLocked { get; init; }

        public bool IsReleased { get; init; }

        public DateTimeOffset? LockedAt { get; init; }

        public DateTimeOffset? ReleasedAt { get; init; }
    }
}
