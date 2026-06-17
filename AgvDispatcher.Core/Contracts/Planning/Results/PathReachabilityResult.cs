namespace AgvDispatcher.Core.Contracts.Planning.Results
{
    /// <summary>
    /// 路径可达性检查结果。
    /// </summary>
    public sealed class PathReachabilityResult
    {
        /// <summary>
        /// 起点 NodeId。
        /// </summary>
        public string StartNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 终点 NodeId。
        /// </summary>
        public string TargetNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 是否可达。
        /// </summary>
        public bool IsReachable { get; init; }

        /// <summary>
        /// 估算的总距离。
        /// </summary>
        public double? EstimatedDistance { get; init; }

        /// <summary>
        /// 估算的总耗时（秒）。
        /// </summary>
        public double? EstimatedSeconds { get; init; }

        /// <summary>
        /// 不可达的原因说明（仅在不可达时存在）。
        /// </summary>
        public string? Reason { get; init; }
    }
}
