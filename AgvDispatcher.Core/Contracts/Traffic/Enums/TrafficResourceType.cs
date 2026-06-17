namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Identifies the category of a traffic-controlled resource.
    /// </summary>
    public enum TrafficResourceType
    {
        /// <summary>
        /// A map node resource.
        /// </summary>
        Node = 1,

        /// <summary>
        /// A map edge resource.
        /// </summary>
        Edge = 2,

        /// <summary>
        /// A logical traffic zone resource.
        /// </summary>
        Zone = 3,

        /// <summary>
        /// A work station resource.
        /// </summary>
        Station = 4,

        /// <summary>
        /// A charger resource.
        /// </summary>
        Charger = 5,

        /// <summary>
        /// An elevator resource.
        /// </summary>
        Elevator = 6,

        /// <summary>
        /// A door resource.
        /// </summary>
        Door = 7
    }
}
