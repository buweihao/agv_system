using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    /// <summary>
    /// 路径规划请求参数。
    /// 
    /// 用于增强版路径规划。
    /// 相比只传起点和终点，该对象可以携带车辆品牌、车辆能力、是否避让锁定边等调度约束。
    /// </summary>
    public class PathPlanningRequest
    {
        /// <summary>
        /// 起点节点编号。
        /// 例如车辆当前位置节点、任务起点节点。
        /// </summary>
        public string StartNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 终点节点编号。
        /// 例如任务取货点、放货点、充电点、等待点。
        /// </summary>
        public string EndNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 车辆编号。
        /// 可选字段，用于日志记录、路径锁定、调度追踪。
        /// </summary>
        public string? VehicleId { get; set; }

        /// <summary>
        /// 车辆品牌或适配器品牌。
        /// 用于判断节点或路径边是否允许该品牌车辆通过。
        /// 例如 Okapi、Hikrobot、MockBrandA。
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// 车辆能力集合。
        /// 用于判断是否满足节点 RequiredCapabilities。
        /// 例如 Transfer、Lift、Fork、Tow 等。
        /// </summary>
        public VehicleCapability Capabilities { get; set; } = VehicleCapability.None;

        /// <summary>
        /// 是否避开已经锁定的路径边。
        /// 
        /// true：路径规划时不走 IsLocked = true 的边。
        /// false：允许规划到锁定边，通常只用于调试或强制规划。
        /// </summary>
        public bool AvoidLockedEdges { get; set; } = true;

        /// <summary>
        /// 是否避开禁用的节点和路径边。
        /// 
        /// true：不经过 IsEnabled = false 的节点或边。
        /// false：允许经过禁用对象，通常只用于地图调试，不建议实际调度使用。
        /// </summary>
        public bool AvoidDisabledObjects { get; set; } = true;

        /// <summary>
        /// 是否避开关闭方向的路径边。
        /// 
        /// true：EdgeDirection.Closed 的边不可通行。
        /// false：允许经过关闭边，通常只用于调试。
        /// </summary>
        public bool AvoidClosedEdges { get; set; } = true;

        /// <summary>
        /// 是否检查品牌约束。
        /// 
        /// true：如果节点或边设置了 AllowedBrands，则只有匹配 Brand 的车辆可以通过。
        /// false：忽略品牌约束。
        /// </summary>
        public bool CheckBrandConstraint { get; set; } = true;

        /// <summary>
        /// 是否检查车辆能力约束。
        /// 
        /// true：如果节点要求 RequiredCapabilities，则车辆必须具备对应能力。
        /// false：忽略能力约束。
        /// </summary>
        public bool CheckCapabilityConstraint { get; set; } = true;
    }
}
