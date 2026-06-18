namespace AgvDispatcher.Core.Contracts.Reservations.Enums
{
    public enum RouteReservationPolicy
    {
        ReserveOnly,
        LockBeforeMove,
        LockFullPath,
        RollingWindow
    }
}
