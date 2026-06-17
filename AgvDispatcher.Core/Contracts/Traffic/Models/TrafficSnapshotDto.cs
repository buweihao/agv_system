using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Represents a consistent snapshot of runtime traffic resource states.
    /// </summary>
    public sealed class TrafficSnapshotDto
    {
        /// <summary>
        /// Gets the map version used by the traffic snapshot.
        /// </summary>
        public string MapVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the monotonically increasing traffic snapshot version.
        /// </summary>
        public long Version { get; init; }

        /// <summary>
        /// Gets the time when the snapshot was generated.
        /// </summary>
        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the resource statuses included in the snapshot.
        /// </summary>
        public IReadOnlyList<TrafficResourceStatusDto> Resources { get; init; } = Array.Empty<TrafficResourceStatusDto>();
    }
}
