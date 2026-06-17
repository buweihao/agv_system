namespace AgvDispatcher.Core.Contracts.Planning.Results
{
    /// <summary>
    /// 路径规划代价信息。
    /// 用于调试、监控、候选路径排序。
    /// </summary>
    public sealed class PathPlanCost
    {
        /// <summary>
        /// 距离代价。
        /// </summary>
        public double DistanceCost { get; init; }

        /// <summary>
        /// 时间代价。
        /// </summary>
        public double TimeCost { get; init; }

        /// <summary>
        /// 转弯代价。
        /// </summary>
        public double TurnCost { get; init; }

        /// <summary>
        /// 交通管制/拥堵代价。
        /// </summary>
        public double TrafficCost { get; init; }

        /// <summary>
        /// 资源预约代价。
        /// </summary>
        public double ReservationCost { get; init; }

        /// <summary>
        /// 总代价。
        /// </summary>
        public double TotalCost { get; init; }
    }
}
