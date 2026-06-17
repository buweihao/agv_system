using System.Collections.ObjectModel;
using AgvDispatcher.Modules.ChargeModule.Models;
using AgvDispatcher.Modules.ChargeModule.Services;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.ChargeModule.ViewModels
{
    /// <summary>
    /// 充电桩列表视图的 ViewModel。
    /// <para>
    /// 直接复用单例 <see cref="ChargeStationRecommendationService"/> 中的充电桩集合作为列表数据源，
    /// 实现各充电分视图间的数据共享。
    /// </para>
    /// </summary>
    public class ChargeListViewModel : BindableBase
    {
        private ObservableCollection<ChargeStationModel> _stationList = new();

        /// <summary>充电桩列表数据源。</summary>
        public ObservableCollection<ChargeStationModel> StationList
        {
            get => _stationList;
            set => SetProperty(ref _stationList, value);
        }

        /// <summary>构造函数，从推荐服务取充电桩集合作为列表数据源。</summary>
        /// <param name="stationService">充电桩推荐服务（单例）。</param>
        public ChargeListViewModel(ChargeStationRecommendationService stationService)
        {
            StationList = stationService.Stations;
        }
    }
}
