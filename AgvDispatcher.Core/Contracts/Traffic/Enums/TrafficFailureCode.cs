namespace AgvDispatcher.Core.Contracts.Traffic.Enums
{
    /// <summary>
    /// Provides traffic-control-specific failure reasons without changing common failure codes.
    /// 提供特定的交通控制失败原因，而不改变通用失败代码。
    /// </summary>
    public enum TrafficFailureCode
    {
        /// <summary>
        /// No traffic failure occurred.
        /// 未发生交通失败。
        /// </summary>
        None = 0,

        /// <summary>
        /// The requested resource was not found.
        /// 未找到请求的资源。
        /// </summary>
        ResourceNotFound = 1,

        /// <summary>
        /// The requested resource is already occupied.
        /// 请求的资源已被占用。
        /// </summary>
        ResourceAlreadyOccupied = 2,

        /// <summary>
        /// The requested resource is already reserved.
        /// 请求的资源已被预留。
        /// </summary>
        ResourceAlreadyReserved = 3,

        /// <summary>
        /// The requested resource is blocked.
        /// 请求的资源被阻塞。
        /// </summary>
        ResourceBlocked = 4,

        /// <summary>
        /// The requested resource is disabled.
        /// 请求的资源被禁用。
        /// </summary>
        ResourceDisabled = 5,

        /// <summary>
        /// The request conflicts with another AGV.
        /// 请求与另一个 AGV 冲突。
        /// </summary>
        ConflictWithOtherAgv = 6,

        /// <summary>
        /// The request conflicts with another task.
        /// 请求与另一个任务冲突。
        /// </summary>
        ConflictWithOtherTask = 7,

        /// <summary>
        /// The related reservation has expired.
        /// 相关的预留已过期。
        /// </summary>
        ReservationExpired = 8,

        /// <summary>
        /// The reservation could not be found.
        /// 未找到预留。
        /// </summary>
        ReservationNotFound = 9,

        /// <summary>
        /// The resource type is invalid for the requested operation.
        /// 对于请求的操作，资源类型无效。
        /// </summary>
        InvalidResourceType = 10,

        /// <summary>
        /// The request is invalid.
        /// 请求无效。
        /// </summary>
        InvalidRequest = 11,

        /// <summary>
        /// The expected map version does not match the active map version.
        /// 预期的地图版本与活动的地图版本不匹配。
        /// </summary>
        MapVersionMismatch = 12,

        /// <summary>
        /// An internal traffic-control error occurred.
        /// 发生内部交通控制错误。
        /// </summary>
        InternalError = 13
    }
}
