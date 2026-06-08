using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using AgvDispatcher.Core.Constants;
using Prism.Events;
using AgvDispatcher.Core.Events;

namespace AgvDispatcher.Shell.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;

        private bool _isSidebarExpanded = true;
        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            set 
            {
                if (SetProperty(ref _isSidebarExpanded, value))
                {
                    SidebarWidth = value ? 200 : 60;
                }
            }
        }

        private int _sidebarWidth = 200;
        public int SidebarWidth
        {
            get => _sidebarWidth;
            set => SetProperty(ref _sidebarWidth, value);
        }

        public DelegateCommand ToggleSidebarCommand { get; }
        public DelegateCommand<string> NavigateCommand { get; }

        public MainWindowViewModel(IRegionManager regionManager, IEventAggregator eventAggregator)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;
            
            ToggleSidebarCommand = new DelegateCommand(() =>
            {
                IsSidebarExpanded = !IsSidebarExpanded;
            });

            NavigateCommand = new DelegateCommand<string>(path =>
            {
                if (path != null)
                {
                    _regionManager.RequestNavigate(RegionNames.MainWorkspaceRegion, path);
                    
                    string title = path switch
                    {
                        "MonitorLayoutView" => "运行监控",
                        "TaskWorkspaceView" => "任务管理",
                        "ChargeWorkspaceView" => "充电管理",
                        "SignalWorkspaceView" => "信号交互",
                        "DataQueryWorkspaceView" => "数据查询",
                        "TaskConfigWorkspaceView" => "任务配置",
                        "SystemSettingView" => "系统设置",
                        _ => ""
                    };
                    _eventAggregator.GetEvent<NavigationTitleEvent>().Publish(title);
                }
            });

            // Initial navigation
            _regionManager.RegisterViewWithRegion(RegionNames.HeaderRegion, typeof(Views.HeaderView));
        }
    }
}
