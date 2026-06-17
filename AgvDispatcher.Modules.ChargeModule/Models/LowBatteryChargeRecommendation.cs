using AgvDispatcher.Core.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.ChargeModule.Models
{
    /// <summary>
    /// 低电量充电推荐条目。
    /// <para>
    /// 将一条低电量告警 <see cref="RobotBatteryAlert"/> 与系统推荐的充电桩
    /// <see cref="RecommendedStation"/> 组合，供充电管理页"低电量待充"列表展示。
    /// 各只读属性是对告警字段的展示友好封装。
    /// </para>
    /// </summary>
    public class LowBatteryChargeRecommendation : BindableBase
    {
        /// <summary>触发本条推荐的低电量告警。</summary>
        public RobotBatteryAlert Alert { get; set; } = new();

        /// <summary>系统推荐的可用充电桩；无可用桩时为 null。</summary>
        public ChargeStationModel? RecommendedStation { get; set; }

        /// <summary>低电量车辆编号。</summary>
        public string VehicleId => Alert.VehicleId;

        /// <summary>告警时的电量百分比。</summary>
        public double BatteryLevel => Alert.BatteryLevel;

        /// <summary>车辆位置；为空时显示 "-"。</summary>
        public string Location => string.IsNullOrWhiteSpace(Alert.Location) ? "-" : Alert.Location;

        /// <summary>告警发生时间文本（HH:mm:ss）；缺省时取当前时间。</summary>
        public string OccurredAtText => Alert.OccurredAt == default
            ? DateTime.Now.ToString("HH:mm:ss")
            : Alert.OccurredAt.ToString("HH:mm:ss");

        /// <summary>推荐充电桩文本；无可用桩时显示提示语。</summary>
        public string RecommendedStationText => RecommendedStation is null
            ? "No available station"
            : RecommendedStation.Id;
    }
}
