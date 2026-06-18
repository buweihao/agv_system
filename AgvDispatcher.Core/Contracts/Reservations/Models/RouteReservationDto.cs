using AgvDispatcher.Core.Contracts.Reservations.Enums;

namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    public sealed class RouteReservationDto
    {
        public string ReservationId { get; init; } = string.Empty;

        public string TaskId { get; init; } = string.Empty;

        public string VehicleId { get; init; } = string.Empty;

        public string PlanId { get; init; } = string.Empty;

        public string MapId { get; init; } = string.Empty;

        public string MapVersion { get; init; } = string.Empty;

        public int RollingWindowSize { get; init; }

        public RouteReservationPolicy ReservationPolicy { get; init; } = RouteReservationPolicy.RollingWindow;

        public RouteReservationState State { get; init; } = RouteReservationState.Created;

        public IReadOnlyList<RouteReservedSegmentDto> Segments { get; init; } = Array.Empty<RouteReservedSegmentDto>();

        public RouteRollingWindowDto? CurrentWindow { get; init; }

        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

        public DateTimeOffset? ReleasedAt { get; init; }
    }
}
