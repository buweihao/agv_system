using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.TaskConfigModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskConfigModule.ViewModels
{
    public class TaskConfigBottomPanelViewModel : BindableBase
    {
        private ObservableCollection<TaskStepModel> _stepList = new();
        public ObservableCollection<TaskStepModel> StepList
        {
            get => _stepList;
            set => SetProperty(ref _stepList, value);
        }

        public ObservableCollection<string> AvailableNodeIds { get; } = new();

        public TaskConfigBottomPanelViewModel(ITaskConfigService taskConfigService, IMapRepository mapRepository)
        {
            StepList = new ObservableCollection<TaskStepModel>(
                taskConfigService.GetTaskSteps().Select(ToTaskStepModel));

            LoadNodesAsync(mapRepository);
        }

        private async void LoadNodesAsync(IMapRepository mapRepository)
        {
            var nodes = await mapRepository.GetNodesAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableNodeIds.Clear();
                foreach (var node in nodes)
                {
                    AvailableNodeIds.Add(node.NodeId);
                }
            });
        }

        private static TaskStepModel ToTaskStepModel(TaskStepConfig config)
        {
            return new TaskStepModel
            {
                Step = config.Step,
                NodeType = config.NodeType,
                NodeName = config.NodeName,
                ActionConfig = config.ActionConfig,
                ParamConfig = config.ParamConfig,
                Timeout = config.Timeout
            };
        }
    }
}
