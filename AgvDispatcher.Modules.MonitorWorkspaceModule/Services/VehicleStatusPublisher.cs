using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Core.Rules;
using AgvDispatcher.Core.Enums;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.Services
{
    /// <summary>
    /// 车辆状态发布服务（模块内实现）。
    /// <para>
    /// 实现 <see cref="IVehicleStatusPublisher"/>，负责把外部上报的车辆状态快照
    /// <see cref="VehicleStatusSnapshot"/> 进行清洗/归一化后写入状态存储 <see cref="IVehicleStateStore"/>，
    /// 并据此判定是否触发低电量告警。是车辆状态进入系统的统一入口。
    /// </para>
    /// </summary>
    public class VehicleStatusPublisher : IVehicleStatusPublisher
    {
        private readonly IVehicleStateStore _vehicleStateStore;

        /// <summary>
        /// 构造函数，注入车辆状态存储。
        /// </summary>
        /// <param name="vehicleStateStore">车辆状态存储，用于落地归一化后的快照。</param>
        public VehicleStatusPublisher(IVehicleStateStore vehicleStateStore)
        {
            _vehicleStateStore = vehicleStateStore;
        }

        /// <summary>
        /// 发布并处理一条车辆状态快照。
        /// </summary>
        /// <param name="snapshot">原始上报的车辆状态快照。</param>
        /// <returns>
        /// 处理结果 <see cref="VehicleStatusIngestionResult"/>：包含归一化后的快照、是否已更新状态，
        /// 以及是否检测到低电量（若检测到则附带 <see cref="RobotBatteryAlert"/> 告警信息）。
        /// </returns>
        public VehicleStatusIngestionResult PublishStatus(VehicleStatusSnapshot snapshot)
        {
            // 上报时间缺省时以当前时间兜底
            var reportedAt = snapshot.ReportedAt == default ? DateTime.Now : snapshot.ReportedAt;
            var existingSnapshot = _vehicleStateStore.GetVehicle(snapshot.VehicleId.Trim());

            // 归一化：去除字段空白、电量裁剪到 0~100、推导在线/告警等派生状态，避免脏数据进入存储
            var normalizedSnapshot = new VehicleStatusSnapshot
            {
                VehicleId = snapshot.VehicleId.Trim(),
                Brand = existingSnapshot?.Brand ?? string.Empty,
                BatteryLevel = Math.Clamp(snapshot.BatteryLevel, 0, 100),
                Location = snapshot.Location.Trim(),
                State = snapshot.State,
                CurrentTaskId = string.IsNullOrWhiteSpace(snapshot.CurrentTaskId) ? null : snapshot.CurrentTaskId.Trim(),
                ReportedAt = reportedAt,
                LoadState = snapshot.LoadState,
                Position = snapshot.Position ?? new MapPosition(),
                // 非离线状态视为在线
                IsOnline = snapshot.IsOnline || snapshot.State != RobotState.Offline,
                IsCharging = snapshot.IsCharging,
                // 故障状态或存在告警码时统一标记为有告警
                HasAlarm = snapshot.HasAlarm || snapshot.State == RobotState.Fault || !string.IsNullOrWhiteSpace(snapshot.ActiveAlarmCode),
                ActiveAlarmCode = snapshot.ActiveAlarmCode,
                ActiveAlarmMessage = snapshot.ActiveAlarmMessage,
                Telemetry = snapshot.Telemetry == null ? new Dictionary<string, string>() : new Dictionary<string, string>(snapshot.Telemetry)
            };

            // 以车辆ID为键 Upsert 到状态存储
            _vehicleStateStore.UpsertStatus(normalizedSnapshot);

            var result = new VehicleStatusIngestionResult
            {
                Snapshot = normalizedSnapshot,
                VehicleStatusUpdated = true,
                LowBatteryDetected = VehicleStatusRules.IsLowBattery(normalizedSnapshot.BatteryLevel)
            };

            // 低电量时构造告警载荷，供上层（如充电模块）订阅处理
            if (result.LowBatteryDetected)
            {
                result.LowBatteryAlert = new RobotBatteryAlert
                {
                    VehicleId = normalizedSnapshot.VehicleId,
                    Brand = normalizedSnapshot.Brand,
                    BatteryLevel = normalizedSnapshot.BatteryLevel,
                    Location = normalizedSnapshot.Location,
                    Threshold = VehicleStatusRules.LowBatteryThreshold,
                    OccurredAt = normalizedSnapshot.ReportedAt
                };

            }

            return result;
        }
    }
}
