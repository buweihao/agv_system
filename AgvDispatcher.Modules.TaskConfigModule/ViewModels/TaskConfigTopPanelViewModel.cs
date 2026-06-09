using System.Collections.ObjectModel;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.TaskConfigModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskConfigModule.ViewModels
{
    public class TaskConfigTopPanelViewModel : BindableBase
    {
        private ObservableCollection<TaskTemplateModel> _templateList = new();
        public ObservableCollection<TaskTemplateModel> TemplateList
        {
            get => _templateList;
            set => SetProperty(ref _templateList, value);
        }

        public TaskConfigTopPanelViewModel(ITaskConfigService taskConfigService)
        {
            TemplateList = new ObservableCollection<TaskTemplateModel>(
                taskConfigService.GetTaskTemplates().Select(ToTaskTemplateModel));
        }

        private static TaskTemplateModel ToTaskTemplateModel(TaskTemplateConfig config)
        {
            return new TaskTemplateModel
            {
                TemplateName = config.TemplateName,
                TemplateType = config.TemplateType,
                Scene = config.Scene,
                Priority = config.Priority,
                Description = config.Description,
                IsEnabled = config.IsEnabled,
                UpdateTime = config.UpdateTime
            };
        }
    }
}
