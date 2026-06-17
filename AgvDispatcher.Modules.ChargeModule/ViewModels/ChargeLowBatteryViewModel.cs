using System.Collections.ObjectModel;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.ChargeModule.Models;
using AgvDispatcher.Modules.ChargeModule.Services;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.ChargeModule.ViewModels
{
    /// <summary>
    /// 低电量待充列表视图的 ViewModel。
    /// <para>
    /// 订阅 <see cref="RobotLowBatteryEvent"/>，当有车辆触发低电量告警时，结合
    /// <see cref="ChargeStationRecommendationService"/> 推荐一个可用充电桩，生成/更新一条
    /// <see cref="LowBatteryChargeRecommendation"/> 并置于列表顶部（同一车辆仅保留最新一条）。
    /// </para>
    /// </summary>
    public class ChargeLowBatteryViewModel : BindableBase
    {
        private readonly ChargeStationRecommendationService _stationService;

        /// <summary>低电量待充推荐列表（最新的排在最前）。</summary>
        public ObservableCollection<LowBatteryChargeRecommendation> LowBatteryAgvs { get; } = new();

        /// <summary>构造函数，订阅低电量告警事件。</summary>
        /// <param name="eventAggregator">事件聚合器。</param>
        /// <param name="stationService">充电桩推荐服务，用于挑选可用充电桩。</param>
        public ChargeLowBatteryViewModel(
            IEventAggregator eventAggregator,
            ChargeStationRecommendationService stationService)
        {
            _stationService = stationService;

            eventAggregator.GetEvent<RobotLowBatteryEvent>()
                .Subscribe(OnRobotLowBattery, ThreadOption.UIThread);
        }

        /// <summary>
        /// 处理低电量告警：生成推荐条目。若该车辆已有条目则原位更新，否则插入到列表顶部。
        /// </summary>
        private void OnRobotLowBattery(RobotBatteryAlert alert)
        {
            var recommendation = new LowBatteryChargeRecommendation
            {
                Alert = alert,
                RecommendedStation = _stationService.RecommendAvailableStation(),
            };

            var existing = LowBatteryAgvs.FirstOrDefault(item => item.VehicleId == alert.VehicleId);
            if (existing is null)
            {
                LowBatteryAgvs.Insert(0, recommendation);
                return;
            }

            var index = LowBatteryAgvs.IndexOf(existing);
            LowBatteryAgvs[index] = recommendation;
        }
    }
}
