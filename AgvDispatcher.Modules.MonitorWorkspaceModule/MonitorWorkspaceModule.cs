using Prism.Ioc;
using Prism.Modularity;
using AgvDispatcher.Modules.MonitorWorkspaceModule.Views;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule
{
    /// <summary>
    /// 运行监控模块。
    /// <para>
    /// Prism 模块入口，负责将"运行监控"主页面 <see cref="MonitorLayoutView"/> 注册为可导航视图。
    /// 该主页面内部再通过 Prism 子区域（左侧统计、地图、AGV 列表、右侧统计）组合各分视图。
    /// 模块构建后由 PostBuild 拷贝到 Shell 的 <c>Modules</c> 目录，被 <c>DirectoryModuleCatalog</c> 动态加载。
    /// </para>
    /// </summary>
    public class MonitorWorkspaceModule : IModule
    {
        /// <summary>
        /// 模块初始化完成后的回调。本模块无需在此做额外工作。
        /// </summary>
        /// <param name="containerProvider">容器提供器，用于解析已注册的依赖。</param>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary>
        /// 向 Prism 容器注册本模块的类型。
        /// 此处将 <see cref="MonitorLayoutView"/> 注册为可导航视图，使其能通过区域导航名加载到主工作区。
        /// </summary>
        /// <param name="containerRegistry">容器注册器。</param>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MonitorLayoutView>();
        }
    }
}
