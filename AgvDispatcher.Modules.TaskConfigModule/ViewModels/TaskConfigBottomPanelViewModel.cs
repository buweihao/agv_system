using System.Collections.ObjectModel;
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

        public TaskConfigBottomPanelViewModel()
        {
            StepList.Add(new TaskStepModel { Step = "1", NodeType = "起点", NodeName = "开始", ActionConfig = "-", ParamConfig = "-", Timeout = "-" });
            StepList.Add(new TaskStepModel { Step = "2", NodeType = "任务节点", NodeName = "接收任务", ActionConfig = "分配AGV", ParamConfig = "空闲优先", Timeout = "60" });
            StepList.Add(new TaskStepModel { Step = "3", NodeType = "任务节点", NodeName = "前往取货点", ActionConfig = "移动", ParamConfig = "Target=A01", Timeout = "120" });
            StepList.Add(new TaskStepModel { Step = "4", NodeType = "任务节点", NodeName = "到达取货点", ActionConfig = "等待", ParamConfig = "-", Timeout = "10" });
            StepList.Add(new TaskStepModel { Step = "5", NodeType = "结束", NodeName = "任务完成", ActionConfig = "更新状态", ParamConfig = "Completed", Timeout = "-" });
        }
    }
}
