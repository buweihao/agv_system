using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Represents a manual or operational block applied to traffic resources.
    /// 表示应用于交通资源的手动或操作性阻塞。
    /// </summary>
    public sealed class TrafficBlockDto
    {
        /// <summary>
        /// Gets the block identifier.
        /// 获取阻塞标识符。
        /// </summary>
        public string BlockId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the resources covered by this block.
        /// 获取此阻塞涵盖的资源。
        /// </summary>
        public IReadOnlyList<TrafficResourceKey> Resources { get; init; } = Array.Empty<TrafficResourceKey>();

        /// <summary>
        /// Gets the reason for the block.
        /// 获取阻塞的原因。
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the operator that created the block, if any.
        /// 获取创建阻塞的操作员（如果有）。
        /// </summary>
        public string? OperatorId { get; init; }

        /// <summary>
        /// Gets the time when the block was created.
        /// 获取阻塞创建的时间。
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        /// <summary>
        /// Gets the expiration time of the block, if any.
        /// 获取阻塞的过期时间（如果有）。
        /// </summary>
        public DateTimeOffset? ExpireAt { get; init; }
    }
}
