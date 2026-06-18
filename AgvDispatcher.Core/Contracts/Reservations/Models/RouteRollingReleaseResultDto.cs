using AgvDispatcher.Core.Contracts.Reservations.Enums;
using AgvDispatcher.Core.Contracts.Traffic.Models;

namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    public sealed class RouteRollingReleaseResultDto
    {
        public string ReservationId { get; init; } = string.Empty;

        public RouteReservationState State { get; init; }

        public IReadOnlyList<int> ReleasedSegmentSequences { get; init; } = Array.Empty<int>();

        public IReadOnlyList<TrafficResourceKey> ReleasedResources { get; init; } = Array.Empty<TrafficResourceKey>();

        public RouteRollingWindowDto? CurrentWindow { get; init; }
    }
}
