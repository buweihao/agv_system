namespace AgvDispatcher.Core.Contracts.Planning.Results
{
    /// <summary>
    /// 最近可达点查询结果。
    /// </summary>
    public sealed class NearestNodeResult
    {
        /// <summary>
        /// 起点 NodeId。
        /// </summary>
        public string FromNodeId { get; init; } = string.Empty;

        /// <summary>
        /// 找到的最优/最近 NodeId。
        /// </summary>
        public string? NearestNodeId { get; init; }

        /// <summary>
        /// 是否成功找到可达点。
        /// </summary>
        public bool Found { get; init; }

        /// <summary>
        /// 到达该点的总距离。
        /// </summary>
        public double? Distance { get; init; }

        /// <summary>
        /// 到达该点的估算耗时（秒）。
        /// </summary>
        public double? EstimatedSeconds { get; init; }

        /// <summary>
        /// 到达该点的实际规划路径（可选）。
        /// </summary>
        public PathPlanResult? Path { get; init; }
    }
}
