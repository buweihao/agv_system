using System;

namespace AgvDispatcher.Core.Events
{
    /// <summary>
    /// Raised after a map draft is successfully published as the current runtime map.
    /// </summary>
    /// <remarks>
    /// Saving a draft must not raise this event. Subscribers should refresh cached map data or call IMapService.GetCurrentMap.
    /// </remarks>
    public sealed class MapPublishedEvent : IDomainEvent
    {
        /// <summary>
        /// Gets the published map identifier.
        /// </summary>
        public string MapId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the published map version.
        /// </summary>
        public string MapVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the publish time.
        /// </summary>
        public DateTime PublishedAt { get; init; }

        /// <summary>
        /// Gets the operator that published the map.
        /// </summary>
        public string? OperatorId { get; init; }

        /// <summary>
        /// Gets the event occurrence time.
        /// </summary>
        public DateTime OccurredAt { get; init; } = DateTime.Now;
    }
}
