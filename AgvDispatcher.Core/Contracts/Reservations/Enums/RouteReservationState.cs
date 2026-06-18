namespace AgvDispatcher.Core.Contracts.Reservations.Enums
{
    public enum RouteReservationState
    {
        Created,
        PartiallyLocked,
        FullyLocked,
        Waiting,
        Completed,
        Canceled,
        Failed,
        Released
    }
}
