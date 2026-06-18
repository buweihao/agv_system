namespace AgvDispatcher.Core.Contracts.Reservations.Enums
{
    /// <summary>
    /// Defines policies for route reservation and locking.
    /// 定义路线预留和锁定的策略。
    /// </summary>
    public enum RouteReservationPolicy
    {
        /// <summary>
        /// Only reserve the route without locking.
        /// 仅预留路线而不锁定。
        /// </summary>
        ReserveOnly,

        /// <summary>
        /// Lock the resource just before moving into it.
        /// 在移入资源前才锁定。
        /// </summary>
        LockBeforeMove,

        /// <summary>
        /// Lock the full path immediately.
        /// 立即锁定全路径。
        /// </summary>
        LockFullPath,

        /// <summary>
        /// Use a rolling window to dynamically lock resources ahead.
        /// 使用滚动窗口动态锁定前方的资源。
        /// </summary>
        RollingWindow
    }
}
