using System;
using System.Collections.Generic;

namespace AgvDispatcher.Core.Contracts.Traffic.Models
{
    /// <summary>
    /// Describes the runtime resources currently occupied by one AGV.
    /// 描述一个 AGV 当前占用的运行时资源。
    /// </summary>
    public sealed class AgvOccupancyDto
    {
        /// <summary>
        /// Gets the AGV identifier.
        /// 获取 AGV 标识符。
        /// </summary>
        public string AgvId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task associated with the occupancy, if any.
        /// 获取与占用相关的任务（如果有）。
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// Gets the current node identifier reported by the AGV, if any.
        /// 获取由 AGV 报告的当前节点标识符（如果有）。
        /// </summary>
        public string? CurrentNodeId { get; init; }

        /// <summary>
        /// Gets the current edge identifier reported by the AGV, if any.
        /// 获取由 AGV 报告的当前边标识符（如果有）。
        /// </summary>
        public string? CurrentEdgeId { get; init; }

        /// <summary>
        /// Gets the occupied node identifiers.
        /// 获取被占用的节点标识符。
        /// </summary>
        public IReadOnlyList<string> OccupiedNodeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the occupied edge identifiers.
        /// 获取被占用的边标识符。
        /// </summary>
        public IReadOnlyList<string> OccupiedEdgeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the AGV report time used to derive this occupancy.
        /// 获取用于派生此占用情况的 AGV 报告时间。
        /// </summary>
        public DateTimeOffset ReportTime { get; init; } = DateTimeOffset.Now;
    }
}
