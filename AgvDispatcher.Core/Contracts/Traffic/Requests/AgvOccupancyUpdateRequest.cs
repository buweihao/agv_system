using System;
using System.Collections.Generic;
using AgvDispatcher.Core.Contracts.Common;

namespace AgvDispatcher.Core.Contracts.Traffic.Requests
{
    /// <summary>
    /// Requests an update of traffic occupancy based on an AGV status report.
    /// 基于 AGV 状态报告请求更新交通占用情况。
    /// </summary>
    public sealed class AgvOccupancyUpdateRequest : IAgvRequest
    {
        /// <summary>
        /// Gets the request context.
        /// 获取请求上下文。
        /// </summary>
        public RequestContext Context { get; init; } = new RequestContext();

        /// <summary>
        /// Gets the AGV identifier that reported occupancy.
        /// 获取报告占用情况的 AGV 标识符。
        /// </summary>
        public string AgvId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the task associated with the occupancy report, if any.
        /// 获取与占用报告相关的任务（如果有）。
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
        /// Gets the node identifiers currently occupied by the AGV.
        /// 获取 AGV 当前占用的节点标识符。
        /// </summary>
        public IReadOnlyList<string> OccupiedNodeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the edge identifiers currently occupied by the AGV.
        /// 获取 AGV 当前占用的边标识符。
        /// </summary>
        public IReadOnlyList<string> OccupiedEdgeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the AGV report time.
        /// 获取 AGV 报告时间。
        /// </summary>
        public DateTimeOffset ReportTime { get; init; } = DateTimeOffset.Now;
    }
}
