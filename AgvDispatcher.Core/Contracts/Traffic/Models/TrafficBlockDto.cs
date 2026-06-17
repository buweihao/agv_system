using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Represents a manual or operational block applied to traffic resources.
    /// </summary>
    public sealed class TrafficBlockDto
    {
        /// <summary>
        /// Gets the block identifier.
        /// </summary>
        public string BlockId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the resources covered by this block.
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the reason for the block.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the operator that created the block, if any.
        /// </summary>
        public string? OperatorId { get; init; }

        /// <summary>
        /// Gets the time when the block was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the expiration time of the block, if any.
        /// </summary>
        public DateTimeOffset? ExpireAt { get; init; }
    }
}
