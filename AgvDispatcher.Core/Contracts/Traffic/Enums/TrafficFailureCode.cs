namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Provides traffic-control-specific failure reasons without changing common failure codes.
    /// </summary>
    public enum TrafficFailureCode
    {
        /// <summary>
        /// No traffic failure occurred.
        /// </summary>
        None = 0,

        /// <summary>
        /// The requested resource was not found.
        /// </summary>
        ResourceNotFound = 1,

        /// <summary>
        /// The requested resource is already occupied.
        /// </summary>
        ResourceAlreadyOccupied = 2,

        /// <summary>
        /// The requested resource is already reserved.
        /// </summary>
        ResourceAlreadyReserved = 3,

        /// <summary>
        /// The requested resource is blocked.
        /// </summary>
        ResourceBlocked = 4,

        /// <summary>
        /// The requested resource is disabled.
        /// </summary>
        ResourceDisabled = 5,

        /// <summary>
        /// The request conflicts with another AGV.
        /// </summary>
        ConflictWithOtherAgv = 6,

        /// <summary>
        /// The request conflicts with another task.
        /// </summary>
        ConflictWithOtherTask = 7,

        /// <summary>
        /// The related reservation has expired.
        /// </summary>
        ReservationExpired = 8,

        /// <summary>
        /// The reservation could not be found.
        /// </summary>
        ReservationNotFound = 9,

        /// <summary>
        /// The resource type is invalid for the requested operation.
        /// </summary>
        InvalidResourceType = 10,

        /// <summary>
        /// The request is invalid.
        /// </summary>
        InvalidRequest = 11,

        /// <summary>
        /// The expected map version does not match the active map version.
        /// </summary>
        MapVersionMismatch = 12,

        /// <summary>
        /// An internal traffic-control error occurred.
        /// </summary>
        InternalError = 13
    }
}
