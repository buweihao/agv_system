using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Modules.TaskModule.Models;
using Prism.Events;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskModule.ViewModels
{
    public class TaskMainPanelViewModel : BindableBase
    {
        private readonly Dictionary<string, string> _pausedAgvReasons = new(StringComparer.OrdinalIgnoreCase);

        private ObservableCollection<TaskModel> _taskList = new();
        public ObservableCollection<TaskModel> TaskList
        {
            get => _taskList;
            set => SetProperty(ref _taskList, value);
        }

        public TaskMainPanelViewModel(IEventAggregator eventAggregator, ITaskService taskService)
        {
            TaskList = new ObservableCollection<TaskModel>(taskService.GetTasks().Select(ToTaskModel));

            eventAggregator
                .GetEvent<RobotLowBatteryEvent>()
                .Subscribe(OnRobotLowBattery, ThreadOption.UIThread);
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
