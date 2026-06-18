namespace AgvDispatcher.Core.Contracts.Reservations.Enums
{
    /// <summary>
    /// Describes the lifecycle state of a route reservation.
    /// 描述路线预留的生命周期状态。
    /// </summary>
    public enum RouteReservationState
    {
        /// <summary>
        /// Reservation is created.
        /// 已创建。
        /// </summary>
        Created,

        /// <summary>
        /// Route is partially locked.
        /// 部分锁定。
        /// </summary>
        PartiallyLocked,

        /// <summary>
        /// Route is fully locked.
        /// 完全锁定。
        /// </summary>
        FullyLocked,

        /// <summary>
        /// Waiting for resources.
        /// 等待中。
        /// </summary>
        Waiting,

        /// <summary>
        /// Reservation is completed.
        /// 已完成。
        /// </summary>
        Completed,

        /// <summary>
        /// Reservation was canceled.
        /// 已取消。
        /// </summary>
        Canceled,

        /// <summary>
        /// Reservation failed.
        /// 失败。
        /// </summary>
        Failed,

        /// <summary>
        /// Resources have been released.
        /// 已释放。
        /// </summary>
        Released
    }
}
