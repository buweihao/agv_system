using System.Collections.ObjectModel;
using AgvDispatcher.Core.Events;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.ViewModels
{
    public class MapViewModel : BindableBase
    {
        private string _pathSummary = "请选择AGV查看规划路径";
        public string PathSummary
        {
            get => _pathSummary;
            set => SetProperty(ref _pathSummary, value);
        }

        private string _detailTitle = "详情";
        public string DetailTitle
        {
            get => _detailTitle;
            set => SetProperty(ref _detailTitle, value);
        }

        private string _detailContent = "点击地图元素查看详情";
        public string DetailContent
        {
            get => _detailContent;
            set => SetProperty(ref _detailContent, value);
        }

        public MapViewModel(IEventAggregator eventAggregator)
        {
            // Empty view model for now to fix compile errors
        }
    }
}
