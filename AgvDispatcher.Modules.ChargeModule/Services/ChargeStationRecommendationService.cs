using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.ChargeModule.Models;

namespace AgvDispatcher.Modules.ChargeModule.Services
{
    /// <summary>
    /// 充电桩推荐服务（模块内单例）。
    /// <para>
    /// 在构造时从充电服务 <see cref="IChargeService"/> 拉取充电桩并转换为展示模型
    /// <see cref="ChargeStationModel"/> 缓存于 <see cref="Stations"/>，供充电列表/推荐等分视图共享；
    /// 并提供 <see cref="RecommendAvailableStation"/> 为低电量车辆挑选一个空闲可用的充电桩。
    /// </para>
    /// </summary>
    public class ChargeStationRecommendationService
    {
        private readonly IChargeService _chargeService;

        /// <summary>充电桩展示模型集合（构造时一次性加载）。</summary>
        public ObservableCollection<ChargeStationModel> Stations { get; }

        /// <summary>构造函数，从充电服务加载并转换全部充电桩。</summary>
        /// <param name="chargeService">充电服务，提供充电桩数据源。</param>
        public ChargeStationRecommendationService(IChargeService chargeService)
        {
            _chargeService = chargeService;
            Stations = new ObservableCollection<ChargeStationModel>(
                _chargeService.GetStations().Select(ToChargeStationModel));
        }

        /// <summary>
        /// 推荐一个可用充电桩：选取状态为空闲且未绑定车辆（LinkedAgv 为 "-"）的第一个充电桩；
        /// 无可用桩时返回 null。
        /// </summary>
        public ChargeStationModel? RecommendAvailableStation()
        {
            return Stations.FirstOrDefault(station => station.State == RobotState.Idle && station.LinkedAgv == "-");
        }

        /// <summary>
        /// 将领域充电桩 <see cref="ChargeStation"/> 转换为展示模型；功率/充电模式/进度等部分字段为演示推导值。
        /// </summary>
        private static ChargeStationModel ToChargeStationModel(ChargeStation station)
        {
            return new ChargeStationModel
            {
                Id = station.StationId,
                State = ToRobotState(station.State),
                LinkedAgv = string.IsNullOrWhiteSpace(station.BoundVehicleId) ? "-" : station.BoundVehicleId,
                BatteryLevel = station.BoundVehicleId is null ? 0 : station.State == ChargeStationState.Charging ? 45 : 12,
                ChargeMode = station.RatedPowerKw >= 20 ? "Fast" : station.RatedPowerKw > 0 ? "Slow" : "-",
                ChargePower = station.RatedPowerKw > 0 ? $"{station.RatedPowerKw:0}kW" : "-",
                ChargedTime = station.State == ChargeStationState.Charging ? "00:15:30" : "00:00:00",
                EstimatedCompletion = station.State == ChargeStationState.Charging ? "00:20:00" : station.State == ChargeStationState.Occupied ? "01:30:00" : "-"
            };
        }

        /// <summary>
        /// 将充电桩状态 <see cref="ChargeStationState"/> 映射为展示用的 <see cref="RobotState"/>：
        /// 充电中→运行，故障→故障，离线/禁用→离线，其余（可用/占用/预约）→空闲。
        /// </summary>
        private static RobotState ToRobotState(ChargeStationState state)
        {
            return state switch
            {
                ChargeStationState.Available => RobotState.Idle,
                ChargeStationState.Occupied => RobotState.Idle,
                ChargeStationState.Charging => RobotState.Running,
                ChargeStationState.Fault => RobotState.Fault,
                ChargeStationState.Offline => RobotState.Offline,
                ChargeStationState.Disabled => RobotState.Offline,
                ChargeStationState.Reserved => RobotState.Idle,
                _ => RobotState.Offline
            };
        }
    }
}
