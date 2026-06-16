using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Rules;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.Models
{
    /// <summary>
    /// 运行监控页面用的 AGV 行视图模型。
    /// <para>
    /// 由车辆状态快照 <c>VehicleStatusSnapshot</c> 转换而来，承载 AGV 列表/详情展示所需的字段，
    /// 并提供低电量判定等派生属性，专供监控界面绑定使用（区别于 Core 中的领域模型 <c>Vehicle</c>）。
    /// </para>
    /// </summary>
    public class RobotModel
    {
        /// <summary>AGV 唯一编号（如 <c>AGV-001</c>）。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>车辆品牌/型号标识（如 <c>RGV-A</c>）。</summary>
        public string Brand { get; set; } = string.Empty;

        /// <summary>当前运行状态（空闲/运行/故障/离线，见 <see cref="RobotState"/>）。</summary>
        public RobotState State { get; set; }

        /// <summary>当前执行的任务编号；无任务时通常显示为 "-"。</summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>当前所在位置（节点编号或位置别名）。</summary>
        public string CurrentPosition { get; set; } = string.Empty;

        /// <summary>目标位置（任务终点节点编号）。</summary>
        public string TargetPosition { get; set; } = string.Empty;

        /// <summary>电量百分比（0~100）。</summary>
        public int BatteryLevel { get; set; }

        /// <summary>当前速度（m/s）。</summary>
        public double Speed { get; set; }

        /// <summary>已运行时长的显示文本（如 <c>02:35:23</c>）。</summary>
        public string RunningTime { get; set; } = string.Empty;

        /// <summary>是否低电量，依据 <see cref="VehicleStatusRules.IsLowBattery"/> 的统一阈值判定。</summary>
        public bool IsLowBattery => VehicleStatusRules.IsLowBattery(BatteryLevel);

        /// <summary>电量状态文本，低电量显示 <c>LOW</c>，否则显示 <c>OK</c>。</summary>
        public string BatteryStatusText => IsLowBattery ? "LOW" : "OK";
    }
}
