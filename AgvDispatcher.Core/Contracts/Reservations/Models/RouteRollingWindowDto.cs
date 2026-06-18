namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    public sealed class RouteRollingWindowDto
    {
        public int StartSegmentSequence { get; init; }

        public int EndSegmentSequence { get; init; }

        public IReadOnlyList<RouteReservedSegmentDto> Segments { get; init; } = Array.Empty<RouteReservedSegmentDto>();

        public bool IsCompletePathLocked { get; init; }
    }
}
