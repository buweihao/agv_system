using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskConfigModule.Models
{
    public class TaskStepModel : BindableBase
    {
        public string Step { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string ActionConfig { get; set; } = string.Empty;
        public string ParamConfig { get; set; } = string.Empty;
        public string Timeout { get; set; } = string.Empty;
    }
}
