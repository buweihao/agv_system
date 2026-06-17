using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;
using AgvDispatcher.Core.Constants;
using AgvDispatcher.Modules.ChargeModule.Services;
using AgvDispatcher.Modules.ChargeModule.Views;

namespace AgvDispatcher.Modules.ChargeModule
{
    /// <summary>
    /// 充电管理模块。
    /// <para>
    /// Prism 模块入口，注册充电桩推荐服务 <see cref="ChargeStationRecommendationService"/>（单例），
    /// 并将"充电管理"主页面 <see cref="ChargeWorkspaceView"/> 注册为可导航视图。
    /// 主页面内部组合充电概览、充电地图、充电桩列表、右侧策略/低电量告警等分视图。
    /// </para>
    /// </summary>
    public class ChargeModule : IModule
    {
        private readonly IRegionManager _regionManager;

        /// <summary>构造函数，注入区域管理器（保留以备区域注册需要）。</summary>
        /// <param name="regionManager">Prism 区域管理器。</param>
        public ChargeModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        /// <summary>模块初始化完成后的回调。本模块无需在此做额外工作。</summary>
        /// <param name="containerProvider">容器提供器。</param>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary>
        /// 注册类型：充电桩推荐服务（单例，供各充电分视图共享数据）与充电主页面（可导航）。
        /// </summary>
        /// <param name="containerRegistry">容器注册器。</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ChargeStationRecommendationService>();
            containerRegistry.RegisterForNavigation<ChargeWorkspaceView>();
        }
    }
}
