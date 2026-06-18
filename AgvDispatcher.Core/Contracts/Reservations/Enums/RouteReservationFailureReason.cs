namespace AgvDispatcher.Core.Contracts.Reservations.Enums
{
    /// <summary>
    /// Defines failure reasons for route reservation operations.
    /// 定义路线预留操作的失败原因。
    /// </summary>
    public enum RouteReservationFailureReason
    {
        /// <summary>
        /// No failure.
        /// 无失败。
        /// </summary>
        None,

        /// <summary>
        /// Resource is already occupied.
        /// 资源已被占用。
        /// </summary>
        ResourceOccupied,

        /// <summary>
        /// Resource is already reserved.
        /// 资源已被预留。
        /// </summary>
        ResourceReserved,

        /// <summary>
        /// Resource is blocked.
        /// 资源已被阻塞。
        /// </summary>
        ResourceBlocked,

        /// <summary>
        /// Map version does not match.
        /// 地图版本不匹配。
        /// </summary>
        MapVersionMismatch,

        /// <summary>
        /// Route segment was not found.
        /// 未找到路线段。
        /// </summary>
        SegmentNotFound,

        /// <summary>
        /// Failed to acquire traffic resources.
        /// 获取交通资源失败。
        /// </summary>
        TrafficAcquireFailed,

        /// <summary>
        /// Operation timed out.
        /// 操作超时。
        /// </summary>
        Timeout,

        /// <summary>
        /// Operation was canceled.
        /// 操作已取消。
        /// </summary>
        Canceled,

        /// <summary>
        /// Unknown failure.
        /// 未知失败。
        /// </summary>
        Unknown
    }
}
