namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Describes the lifecycle state of a traffic reservation.
    /// </summary>
    public enum TrafficReservationStatus
    {
        /// <summary>
        /// The reservation is active.
        /// </summary>
        Active = 1,

        /// <summary>
        /// The reservation has been released.
        /// </summary>
        Released = 2,

        /// <summary>
        /// The reservation has expired.
        /// </summary>
        Expired = 3,

        /// <summary>
        /// The reservation request failed.
        /// </summary>
        Failed = 4
    }
}
