namespace AgvDispatcher.Core.Contracts.Reservations.Enums
{
    public enum RouteReservationFailureReason
    {
        None,
        ResourceOccupied,
        ResourceReserved,
        ResourceBlocked,
        MapVersionMismatch,
        SegmentNotFound,
        TrafficAcquireFailed,
        Timeout,
        Canceled,
        Unknown
    }
}
