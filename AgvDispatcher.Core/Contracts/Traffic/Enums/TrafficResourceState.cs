namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Describes the runtime state of a traffic-controlled resource.
    /// </summary>
    public enum TrafficResourceState
    {
        /// <summary>
        /// The current state is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The resource is available for use.
        /// </summary>
        Free = 1,

        /// <summary>
        /// The resource is reserved by an AGV or task.
        /// </summary>
        Reserved = 2,

        /// <summary>
        /// The resource is currently occupied by an AGV.
        /// </summary>
        Occupied = 3,

        /// <summary>
        /// The resource is locked for controlled access.
        /// </summary>
        Locked = 4,

        /// <summary>
        /// The resource is manually or operationally blocked.
        /// </summary>
        Blocked = 5,

        /// <summary>
        /// The resource is disabled and cannot be used.
        /// </summary>
        Disabled = 6
    }
}
