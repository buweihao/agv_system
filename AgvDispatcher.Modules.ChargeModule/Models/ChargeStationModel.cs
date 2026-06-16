using AgvDispatcher.Core.Enums;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.ChargeModule.Models
{
    /// <summary>
    /// 充电管理页面用的充电桩行视图模型。
    /// <para>
    /// 由领域模型 <c>ChargeStation</c> 转换而来（见 <c>ChargeStationRecommendationService</c>），
    /// 承载充电桩列表展示所需的字段。继承 <see cref="BindableBase"/> 以支持属性变更通知。
    /// </para>
    /// </summary>
    public class ChargeStationModel : BindableBase
    {
        /// <summary>充电桩编号。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>充电桩状态（复用 <see cref="RobotState"/> 表达空闲/工作/故障/离线）。</summary>
        public RobotState State { get; set; }

        /// <summary>当前关联的 AGV 编号；无关联时为 "-"。</summary>
        public string LinkedAgv { get; set; } = string.Empty;

        /// <summary>关联车辆的电量百分比（用于展示充电进度）。</summary>
        public int BatteryLevel { get; set; }

        /// <summary>充电模式（快充 Fast / 慢充 Slow / 无 -）。</summary>
        public string ChargeMode { get; set; } = string.Empty;

        /// <summary>充电功率显示文本（如 <c>20kW</c>）。</summary>
        public string ChargePower { get; set; } = string.Empty;

        /// <summary>已充电时长文本。</summary>
        public string ChargedTime { get; set; } = string.Empty;

        /// <summary>预计完成时间文本。</summary>
        public string EstimatedCompletion { get; set; } = string.Empty;
    }
}
