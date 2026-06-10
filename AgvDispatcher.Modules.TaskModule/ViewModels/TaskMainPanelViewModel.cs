using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.TaskModule.Events;
using AgvDispatcher.Modules.TaskModule.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskModule.ViewModels
{
    public class TaskMainPanelViewModel : BindableBase
    {
        private readonly Dictionary<string, string> _pausedAgvReasons = new(StringComparer.OrdinalIgnoreCase);
        private readonly IEventAggregator _eventAggregator;
        private readonly ITaskService _taskService;
        private readonly IDispatchService _dispatchService;

        private ObservableCollection<TaskModel> _taskList = new();
        public ObservableCollection<TaskModel> TaskList
        {
            get => _taskList;
            set => SetProperty(ref _taskList, value);
        }

        private ObservableCollection<TaskModel> _pagedTaskList = new();
        public ObservableCollection<TaskModel> PagedTaskList
        {
            get => _pagedTaskList;
            set => SetProperty(ref _pagedTaskList, value);
        }

        private int _pageIndex = 1;
        public int PageIndex
        {
            get => _pageIndex;
            set
            {
                if (SetProperty(ref _pageIndex, value))
                {
                    RefreshPagedTasks();
                }
            }
        }

        private int _pageSize = 10;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    PageIndex = 1;
                    RefreshPagedTasks();
                }
            }
        }

        public IReadOnlyList<int> PageSizeOptions { get; } = new[] { 10, 20, 50 };

        public int TotalTaskCount => TaskList.Count;

        public int TotalPageCount => Math.Max(1, (int)Math.Ceiling((double)TotalTaskCount / PageSize));

        public string TotalRecordText => $"共 {TotalTaskCount} 条记录";

        public string PageSummaryText => $"第 {PageIndex} / {TotalPageCount} 页";

        public bool CanGoPreviousPage => PageIndex > 1;

        public bool CanGoNextPage => PageIndex < TotalPageCount;

        private string _dispatchMessage = "等待调度";
        public string DispatchMessage
        {
            get => _dispatchMessage;
            set => SetProperty(ref _dispatchMessage, value);
        }

        public DelegateCommand CreateDemoTaskCommand { get; }

        public DelegateCommand<TaskModel> AutoDispatchCommand { get; }

        public DelegateCommand PreviousPageCommand { get; }

        public DelegateCommand NextPageCommand { get; }

        public TaskMainPanelViewModel(
            IEventAggregator eventAggregator,
            ITaskService taskService,
            IDispatchService dispatchService)
        {
            _eventAggregator = eventAggregator;
            _taskService = taskService;
            _dispatchService = dispatchService;
            CreateDemoTaskCommand = new DelegateCommand(CreateDemoTask);
            AutoDispatchCommand = new DelegateCommand<TaskModel>(AutoDispatch, CanAutoDispatch);
            PreviousPageCommand = new DelegateCommand(
                () => PageIndex--,
                () => CanGoPreviousPage);
            NextPageCommand = new DelegateCommand(
                () => PageIndex++,
                () => CanGoNextPage);

            RefreshTasks();

            eventAggregator
                .GetEvent<RobotLowBatteryEvent>()
                .Subscribe(OnRobotLowBattery, ThreadOption.UIThread);
        }

        private void RefreshTasks()
        {
            TaskList = new ObservableCollection<TaskModel>(_taskService.GetTasks().Select(ToTaskModel));
            if (PageIndex > TotalPageCount)
            {
                _pageIndex = TotalPageCount;
                RaisePropertyChanged(nameof(PageIndex));
            }

            RefreshPagedTasks();
            _eventAggregator.GetEvent<TaskDataChangedEvent>().Publish();
        }

        private void RefreshPagedTasks()
        {
            int safePageSize = Math.Max(PageSize, 1);
            int safePageIndex = Math.Clamp(PageIndex, 1, TotalPageCount);
            if (safePageIndex != PageIndex)
            {
                _pageIndex = safePageIndex;
                RaisePropertyChanged(nameof(PageIndex));
            }

            PagedTaskList = new ObservableCollection<TaskModel>(
                TaskList
                    .Skip((safePageIndex - 1) * safePageSize)
                    .Take(safePageSize));

            RaisePagingPropertiesChanged();
        }

        private void RaisePagingPropertiesChanged()
        {
            RaisePropertyChanged(nameof(TotalTaskCount));
            RaisePropertyChanged(nameof(TotalPageCount));
            RaisePropertyChanged(nameof(TotalRecordText));
            RaisePropertyChanged(nameof(PageSummaryText));
            RaisePropertyChanged(nameof(CanGoPreviousPage));
            RaisePropertyChanged(nameof(CanGoNextPage));
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }

        private void CreateDemoTask()
        {
            var task = _taskService.CreateTask(new TaskCreateRequest
            {
                TaskType = "搬运",
                TemplateId = "MINIMAL-DISPATCH",
                SourceNodeId = "A1",
                TargetNodeId = "B2",
                Priority = TaskPriority.Normal,
                CargoCode = $"CARGO-{DateTime.Now:HHmmss}",
                CargoName = "测试物料",
                CreatedBy = "Operator"
            });

            DispatchMessage = $"已创建任务 {task.TaskId}";
            RefreshTasks();
        }

        private bool CanAutoDispatch(TaskModel? task)
        {
            return task is not null
                && task.State == TaskState.Pending
                && !task.IsDispatchPaused;
        }

        private void AutoDispatch(TaskModel? task)
        {
            if (task is null)
            {
                return;
            }

            var result = _dispatchService.AssignTask(task.Id);
            DispatchMessage = result.Succeeded
                ? $"派发成功：{result.TaskId} -> {result.VehicleId}"
                : $"派发失败：{result.Code}，{result.Message}";

            RefreshTasks();
        }

        private void OnRobotLowBattery(RobotBatteryAlert alert)
        {
            if (string.IsNullOrWhiteSpace(alert.VehicleId))
            {
                return;
            }

            string reason = $"低电量（{alert.BatteryLevel:0.#}%）";
            _pausedAgvReasons[alert.VehicleId] = reason;

            foreach (TaskModel task in TaskList)
            {
                ApplyDispatchPauseState(task);
            }

            foreach (TaskModel task in PagedTaskList)
            {
                ApplyDispatchPauseState(task);
            }

            AutoDispatchCommand.RaiseCanExecuteChanged();
        }

        private void ApplyDispatchPauseState(TaskModel task)
        {
            if (_pausedAgvReasons.TryGetValue(task.AgvId, out string? reason))
            {
                task.IsDispatchPaused = true;
                task.DispatchPauseReason = reason;
                return;
            }

            task.IsDispatchPaused = false;
            task.DispatchPauseReason = string.Empty;
        }

        private TaskModel ToTaskModel(TaskOrder task)
        {
            var model = new TaskModel
            {
                Id = task.TaskId,
                Type = task.TaskType,
                Priority = FormatPriority(task.Priority),
                State = task.State,
                AgvId = string.IsNullOrWhiteSpace(task.AssignedVehicleId) ? "-" : task.AssignedVehicleId,
                StartPoint = task.SourceNodeId,
                EndPoint = task.TargetNodeId,
                CreatedTime = task.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                EstimatedTime = task.FinishedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-"
            };

            ApplyDispatchPauseState(model);
            return model;
        }

        private static string FormatPriority(TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.Low => "低",
                TaskPriority.Normal => "中",
                TaskPriority.High => "高",
                TaskPriority.Urgent => "紧急",
                _ => priority.ToString()
            };
        }
    }
}
