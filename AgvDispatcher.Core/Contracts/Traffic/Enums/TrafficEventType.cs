namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Defines event types emitted for traffic resource state changes.
    /// 定义因交通资源状态改变而发出的事件类型。
    /// </summary>
    public enum TrafficEventType
    {
        /// <summary>
        /// A resource was reserved.
        /// 资源被预留。
        /// </summary>
        ResourceReserved = 1,

        /// <summary>
        /// A resource was occupied.
        /// 资源被占用。
        /// </summary>
        ResourceOccupied = 2,

        /// <summary>
        /// A resource was released.
        /// 资源被释放。
        /// </summary>
        ResourceReleased = 3,

        /// <summary>
        /// A resource was blocked.
        /// 资源被阻塞。
        /// </summary>
        ResourceBlocked = 4,

        /// <summary>
        /// A resource was unblocked.
        /// 资源被解除阻塞。
        /// </summary>
        ResourceUnblocked = 5,

        /// <summary>
        /// A reservation expired.
        /// 预留过期。
        /// </summary>
        ReservationExpired = 6,

        /// <summary>
        /// A traffic conflict was detected.
        /// 检测到交通冲突。
        /// </summary>
        ConflictDetected = 7,

        /// <summary>
        /// An AGV occupancy report changed resource state.
        /// AGV 占用报告改变了资源状态。
        /// </summary>
        AgvOccupancyChanged = 8
    }
}
