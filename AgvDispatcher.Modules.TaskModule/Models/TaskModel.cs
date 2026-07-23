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
                    RaiseDispatchStateChanged();
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
            set
            {
                if (SetProperty(ref _isDispatchPaused, value))
                {
                    RaiseDispatchStateChanged();
                }
            }
        }

        private string _dispatchPauseReason = string.Empty;
        public string DispatchPauseReason
        {
            get => _dispatchPauseReason;
            set
            {
                if (SetProperty(ref _dispatchPauseReason, value))
                {
                    RaiseDispatchStateChanged();
                }
            }
        }

        private bool _isDispatching;
        public bool IsDispatching
        {
            get => _isDispatching;
            set
            {
                if (SetProperty(ref _isDispatching, value))
                {
                    RaiseDispatchStateChanged();
                }
            }
        }

        public string DispatchStateDisplay
        {
            get
            {
                if (IsDispatching)
                {
                    return "派发中";
                }

                if (State == TaskState.Pending && IsDispatchPaused)
                {
                    return string.IsNullOrWhiteSpace(DispatchPauseReason)
                        ? "暂停派发"
                        : $"暂停派发：{DispatchPauseReason}";
                }

                return State switch
                {
                    TaskState.Pending => "可派发",
                    TaskState.Running => "已派发 / 执行中",
                    TaskState.Completed => "已完成",
                    TaskState.Failed => "任务失败",
                    TaskState.Cancelled => "已取消",
                    TaskState.Interrupted => "已中断 / 待处理",
                    _ => State.ToString()
                };
            }
        }

        public string DispatchStateColor => IsDispatching
            ? "#00BFFF"
            : State switch
            {
                TaskState.Pending when IsDispatchPaused => "#FFD700",
                TaskState.Pending => "#00FF7F",
                TaskState.Running => "#00BFFF",
                TaskState.Completed => "#2E8B57",
                TaskState.Failed => "#FF4500",
                TaskState.Cancelled => "#808080",
                TaskState.Interrupted => "#FF8C00",
                _ => "#FFFFFF"
            };

        private void RaiseDispatchStateChanged()
        {
            RaisePropertyChanged(nameof(DispatchStateDisplay));
            RaisePropertyChanged(nameof(DispatchStateColor));
        }
    }
}
