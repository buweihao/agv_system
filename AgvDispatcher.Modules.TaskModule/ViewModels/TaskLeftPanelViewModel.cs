using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.TaskModule.Events;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskModule.ViewModels
{
    public class TaskLeftPanelViewModel : BindableBase
    {
        private readonly ITaskService _taskService;

        private int _totalTasks;
        public int TotalTasks
        {
            get => _totalTasks;
            set => SetProperty(ref _totalTasks, value);
        }

        private int _runningTasks;
        public int RunningTasks
        {
            get => _runningTasks;
            set => SetProperty(ref _runningTasks, value);
        }

        private int _pendingTasks;
        public int PendingTasks
        {
            get => _pendingTasks;
            set => SetProperty(ref _pendingTasks, value);
        }

        private int _completedTasks;
        public int CompletedTasks
        {
            get => _completedTasks;
            set => SetProperty(ref _completedTasks, value);
        }

        private int _failedTasks;
        public int FailedTasks
        {
            get => _failedTasks;
            set => SetProperty(ref _failedTasks, value);
        }

        private int _cancelledTasks;
        public int CancelledTasks
        {
            get => _cancelledTasks;
            set => SetProperty(ref _cancelledTasks, value);
        }

        public string SuccessRateText => $"{CalculatePercent(CompletedTasks, CompletedTasks + FailedTasks + CancelledTasks):0}%";

        public string RunningPercentText => $"{CalculatePercent(RunningTasks, TotalTasks):0}%";

        public string PendingPercentText => $"{CalculatePercent(PendingTasks, TotalTasks):0}%";

        public string CompletedPercentText => $"{CalculatePercent(CompletedTasks, TotalTasks):0}%";

        public string FailedPercentText => $"{CalculatePercent(FailedTasks, TotalTasks):0}%";

        public string CancelledPercentText => $"{CalculatePercent(CancelledTasks, TotalTasks):0}%";

        public TaskLeftPanelViewModel(IEventAggregator eventAggregator, ITaskService taskService)
        {
            _taskService = taskService;

            RefreshStats();

            eventAggregator
                .GetEvent<TaskDataChangedEvent>()
                .Subscribe(RefreshStats, ThreadOption.UIThread);

            eventAggregator
                .GetEvent<TaskOrderUpdatedEvent>()
                .Subscribe(_ => RefreshStats(), ThreadOption.UIThread);

            eventAggregator
                .GetEvent<VehicleStateChangedEvent>()
                .Subscribe(_ => RefreshStats(), ThreadOption.UIThread);
        }

        private void RefreshStats()
        {
            IReadOnlyList<TaskOrder> tasks = _taskService.GetTasks();

            TotalTasks = tasks.Count;
            RunningTasks = CountByState(tasks, TaskState.Running);
            PendingTasks = CountByState(tasks, TaskState.Pending);
            CompletedTasks = CountByState(tasks, TaskState.Completed);
            FailedTasks = CountByState(tasks, TaskState.Failed);
            CancelledTasks = CountByState(tasks, TaskState.Cancelled);

            RaisePropertyChanged(nameof(SuccessRateText));
            RaisePropertyChanged(nameof(RunningPercentText));
            RaisePropertyChanged(nameof(PendingPercentText));
            RaisePropertyChanged(nameof(CompletedPercentText));
            RaisePropertyChanged(nameof(FailedPercentText));
            RaisePropertyChanged(nameof(CancelledPercentText));
        }

        private static int CountByState(IEnumerable<TaskOrder> tasks, TaskState state)
        {
            return tasks.Count(task => task.State == state);
        }

        private static double CalculatePercent(int count, int total)
        {
            return total <= 0 ? 0 : (double)count / total * 100;
        }
    }
}
