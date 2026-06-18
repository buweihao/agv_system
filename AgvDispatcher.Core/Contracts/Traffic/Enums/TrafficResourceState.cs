namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Describes the runtime state of a traffic-controlled resource.
    /// 描述受交通控制资源的运行时状态。
    /// </summary>
    public enum TrafficResourceState
    {
        /// <summary>
        /// The current state is unknown.
        /// 当前状态未知。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The resource is available for use.
        /// 资源可用。
        /// </summary>
        Free = 1,

        /// <summary>
        /// The resource is reserved by an AGV or task.
        /// 资源被 AGV 或任务预留。
        /// </summary>
        Reserved = 2,

        /// <summary>
        /// The resource is currently occupied by an AGV.
        /// 资源当前被 AGV 占用。
        /// </summary>
        Occupied = 3,

        /// <summary>
        /// The resource is locked for controlled access.
        /// 资源被锁定以进行受控访问。
        /// </summary>
        Locked = 4,

        /// <summary>
        /// The resource is manually or operationally blocked.
        /// 资源被手动或操作性阻塞。
        /// </summary>
        Blocked = 5,

        /// <summary>
        /// The resource is disabled and cannot be used.
        /// 资源被禁用且无法使用。
        /// </summary>
        Disabled = 6
    }
}
