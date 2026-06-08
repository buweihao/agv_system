using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskConfigModule.Models
{
    public class TaskTemplateModel : BindableBase
    {
        public string TemplateName { get; set; } = string.Empty;
        public string TemplateType { get; set; } = string.Empty;
        public string Scene { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public string UpdateTime { get; set; } = string.Empty;
    }
}
