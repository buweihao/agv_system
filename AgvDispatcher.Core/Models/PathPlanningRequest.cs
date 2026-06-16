using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Core.Models
{
    /// <summary>
    /// 路径规划请求：在起止点之外，携带车辆品牌、能力以及对禁用/锁定/封闭通路的处理策略，
    /// 使路径规划能够剔除当前车辆无法通行（品牌不允许、能力不足）或被运维临时阻断的节点与边。
    /// </summary>
    public class PathPlanningRequest
    {
        /// <summary>起始节点编号。</summary>
        public string StartNodeId { get; set; } = string.Empty;

        /// <summary>目标节点编号。</summary>
        public string EndNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 执行规划的车辆品牌；<c>null</c> 或空字符串表示不按品牌过滤。
        /// 节点/边的 <c>AllowedBrands</c> 非空且不含此品牌时，对应节点/边视为不可通行。
        /// </summary>
        public string? Brand { get; set; }

        /// <summary>
        /// 车辆能力位（<see cref="VehicleCapability"/> 标志组合）；默认 <see cref="VehicleCapability.None"/>。
        /// 当节点要求的能力（<c>RequiredCapabilities</c>）未被完全包含时，该节点视为不可通行。
        /// </summary>
        public VehicleCapability Capabilities { get; set; } = VehicleCapability.None;

        /// <summary>是否跳过被禁用（<c>IsEnabled == false</c>）的节点与边。默认 <c>true</c>。</summary>
        public bool RespectDisabled { get; set; } = true;

        /// <summary>是否跳过被锁定（<c>IsLocked == true</c>）的边。默认 <c>true</c>。</summary>
        public bool RespectLocked { get; set; } = true;

        /// <summary>是否跳过方向为 <see cref="EdgeDirection.Closed"/> 的封闭边。默认 <c>true</c>。</summary>
        public bool RespectClosed { get; set; } = true;
    }
}
