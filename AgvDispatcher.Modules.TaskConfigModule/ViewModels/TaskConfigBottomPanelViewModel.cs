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

        public TaskConfigBottomPanelViewModel(ITaskConfigService taskConfigService)
        {
            StepList = new ObservableCollection<TaskStepModel>(
                taskConfigService.GetTaskSteps().Select(ToTaskStepModel));
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
