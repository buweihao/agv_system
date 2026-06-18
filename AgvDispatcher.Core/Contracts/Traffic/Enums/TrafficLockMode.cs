namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Specifies how a caller wants to acquire traffic resources.
    /// 指定调用者希望如何获取交通资源。
    /// </summary>
    public enum TrafficLockMode
    {
        /// <summary>
        /// Reserve the resource for future movement.
        /// 为未来的移动预留资源。
        /// </summary>
        Reserve = 1,

        /// <summary>
        /// Mark the resource as occupied by the AGV.
        /// 标记资源被 AGV 占用。
        /// </summary>
        Occupy = 2,

        /// <summary>
        /// Lock the resource for controlled use.
        /// 锁定资源以进行受控使用。
        /// </summary>
        Lock = 3,

        /// <summary>
        /// Block the resource from regular traffic.
        /// 阻塞资源，使其脱离常规交通。
        /// </summary>
        Block = 4
    }
}
