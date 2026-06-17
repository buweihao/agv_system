namespace AgvDispatcher.Core.Contracts.Planning.Enums
{
    /// <summary>
    /// 路径规划模式。
    /// </summary>
    public enum PathPlanMode
    {
        /// <summary>
        /// 最短距离优先。
        /// </summary>
        ShortestDistance = 1,

        /// <summary>
        /// 最短时间优先。
        /// </summary>
        ShortestTime = 2,

        /// <summary>
        /// 最少转弯优先。
        /// </summary>
        LeastTurn = 3,

        /// <summary>
        /// 避开拥堵优先。
        /// </summary>
        AvoidTraffic = 4,

        /// <summary>
        /// 综合平衡（默认推荐）。
        /// </summary>
        Balanced = 5
    }
}
