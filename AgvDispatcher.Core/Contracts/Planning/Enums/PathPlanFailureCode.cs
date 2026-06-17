namespace AgvDispatcher.Core.Contracts.Planning.Enums
{
    /// <summary>
    /// 路径规划失败原因码。
    /// </summary>
    public enum PathPlanFailureCode
    {
        /// <summary>
        /// 无错误。
        /// </summary>
        None = 0,

        /// <summary>
        /// 找不到地图快照。
        /// </summary>
        MapNotFound = 1001,

        /// <summary>
        /// 地图版本不匹配。
        /// </summary>
        MapVersionMismatch = 1002,

        /// <summary>
        /// 找不到起点。
        /// </summary>
        StartNodeNotFound = 1003,

        /// <summary>
        /// 找不到终点。
        /// </summary>
        TargetNodeNotFound = 1004,

        /// <summary>
        /// 找不到途经点。
        /// </summary>
        WaypointNotFound = 1005,

        /// <summary>
        /// 起点已被禁用。
        /// </summary>
        StartNodeDisabled = 1006,

        /// <summary>
        /// 终点已被禁用。
        /// </summary>
        TargetNodeDisabled = 1007,

        /// <summary>
        /// 未找到可行路径。
        /// </summary>
        NoPathFound = 1008,

        /// <summary>
        /// 路径被交通管制阻塞。
        /// </summary>
        PathBlockedByTrafficControl = 1009,

        /// <summary>
        /// 路径被已预约资源阻塞。
        /// </summary>
        PathBlockedByReservation = 1010,

        /// <summary>
        /// 车辆能力不支持通过该路径。
        /// </summary>
        VehicleCapabilityNotSupported = 1011,

        /// <summary>
        /// 约束条件无效或冲突。
        /// </summary>
        InvalidConstraint = 1012,

        /// <summary>
        /// 规划算法超时。
        /// </summary>
        PlannerTimeout = 1013
    }
}
