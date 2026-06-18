namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Describes the lifecycle state of a traffic reservation.
    /// 描述交通预留的生命周期状态。
    /// </summary>
    public enum TrafficReservationStatus
    {
        /// <summary>
        /// The reservation is active.
        /// 预留处于活动状态。
        /// </summary>
        Active = 1,

        /// <summary>
        /// The reservation has been released.
        /// 预留已被释放。
        /// </summary>
        Released = 2,

        /// <summary>
        /// The reservation has expired.
        /// 预留已过期。
        /// </summary>
        Expired = 3,

        /// <summary>
        /// The reservation request failed.
        /// 预留请求失败。
        /// </summary>
        Failed = 4
    }
}
