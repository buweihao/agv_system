using AgvDispatcher.Core.Contracts.Reservations.Enums;

namespace AgvDispatcher.Core.Contracts.Reservations.Models
{
    public sealed class RouteRollingLockResultDto
    {
        public string ReservationId { get; init; } = string.Empty;

        public RouteReservationState State { get; init; }

        public RouteRollingWindowDto? AcquiredWindow { get; init; }

        public bool Acquired { get; init; }

        public bool ShouldWait { get; init; }

        public bool RequiresReplan { get; init; }

        public RouteReservationFailureReason FailureReason { get; init; } = RouteReservationFailureReason.None;

        public string Message { get; init; } = string.Empty;
    }
}
