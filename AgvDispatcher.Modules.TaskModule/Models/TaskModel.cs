using AgvDispatcher.Core.Enums;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskModule.Models
{
    public class TaskModel : BindableBase
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        private TaskState _state;
        public TaskState State
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    RaisePropertyChanged(nameof(StateDisplay));
                }
            }
        }
        
        public string StateDisplay => State switch
        {
            TaskState.Pending => "待处理",
            TaskState.Running => "运行中",
            TaskState.Completed => "已完成",
            TaskState.Failed => "失败",
            TaskState.Cancelled => "取消",
            TaskState.Interrupted => "中断待处理",
            _ => State.ToString()
        };
        public string AgvId { get; set; } = string.Empty;
        public string StartPoint { get; set; } = string.Empty;
        public string EndPoint { get; set; } = string.Empty;
        public string CreatedTime { get; set; } = string.Empty;
        public string EstimatedTime { get; set; } = string.Empty;
        public int ProgressPercent { get; set; }
        public string ProgressText => $"{ProgressPercent}%";

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
