namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Specifies how a caller wants to acquire traffic resources.
    /// </summary>
    public enum TrafficLockMode
    {
        /// <summary>
        /// Reserve the resource for future movement.
        /// </summary>
        Reserve = 1,

        /// <summary>
        /// Mark the resource as occupied by the AGV.
        /// </summary>
        Occupy = 2,

        /// <summary>
        /// Lock the resource for controlled use.
        /// </summary>
        Lock = 3,

        /// <summary>
        /// Block the resource from regular traffic.
        /// </summary>
        Block = 4
    }
}
