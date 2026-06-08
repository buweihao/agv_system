using AgvDispatcher.Core.Enums;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskModule.Models
{
    public class TaskModel : BindableBase
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public TaskState State { get; set; }
        public string AgvId { get; set; } = string.Empty;
        public string StartPoint { get; set; } = string.Empty;
        public string EndPoint { get; set; } = string.Empty;
        public string CreatedTime { get; set; } = string.Empty;
        public string EstimatedTime { get; set; } = string.Empty;

        private bool _isDispatchPaused;
        public bool IsDispatchPaused
        {
            get => _isDispatchPaused;
            set => SetProperty(ref _isDispatchPaused, value);
        }

        private string _dispatchPauseReason = string.Empty;
        public string DispatchPauseReason
        {
            get => _dispatchPauseReason;
            set => SetProperty(ref _dispatchPauseReason, value);
        }
    }
}
