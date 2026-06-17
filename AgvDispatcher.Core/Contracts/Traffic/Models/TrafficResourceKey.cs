using AgvDispatcher.Core.Contracts.Traffic.Enums;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Identifies one traffic-controlled runtime resource.
    /// </summary>
    public sealed class TrafficResourceKey
    {
        /// <summary>
        /// Gets the type of the traffic resource.
        /// </summary>
        public TrafficResourceType ResourceType { get; init; }

        /// <summary>
        /// Gets the resource identifier within its type.
        /// </summary>
        public string ResourceId { get; init; } = string.Empty;
    }
}
