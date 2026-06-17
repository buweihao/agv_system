namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Defines event types emitted for traffic resource state changes.
    /// </summary>
    public enum TrafficEventType
    {
        /// <summary>
        /// A resource was reserved.
        /// </summary>
        ResourceReserved = 1,

        /// <summary>
        /// A resource was occupied.
        /// </summary>
        ResourceOccupied = 2,

        /// <summary>
        /// A resource was released.
        /// </summary>
        ResourceReleased = 3,

        /// <summary>
        /// A resource was blocked.
        /// </summary>
        ResourceBlocked = 4,

        /// <summary>
        /// A resource was unblocked.
        /// </summary>
        ResourceUnblocked = 5,

        /// <summary>
        /// A reservation expired.
        /// </summary>
        ReservationExpired = 6,

        /// <summary>
        /// A traffic conflict was detected.
        /// </summary>
        ConflictDetected = 7,

        /// <summary>
        /// An AGV occupancy report changed resource state.
        /// </summary>
        AgvOccupancyChanged = 8
    }
}
