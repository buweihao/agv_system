using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
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

        public TaskMainPanelViewModel(IEventAggregator eventAggregator)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string later = DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm:ss");

            AddTask(new TaskModel { Id = "T-240101", Type = "搬运", Priority = "高", State = TaskState.Running, AgvId = "AGV-001", StartPoint = "A1", EndPoint = "B2", CreatedTime = now, EstimatedTime = later });
            AddTask(new TaskModel { Id = "T-240102", Type = "搬运", Priority = "中", State = TaskState.Running, AgvId = "AGV-002", StartPoint = "A2", EndPoint = "B3", CreatedTime = now, EstimatedTime = later });
            AddTask(new TaskModel { Id = "T-240103", Type = "充电", Priority = "高", State = TaskState.Pending, AgvId = "AGV-005", StartPoint = "C1", EndPoint = "Charge-1", CreatedTime = now, EstimatedTime = "-" });
            AddTask(new TaskModel { Id = "T-240104", Type = "巡检", Priority = "低", State = TaskState.Completed, AgvId = "AGV-008", StartPoint = "D1", EndPoint = "D2", CreatedTime = now, EstimatedTime = now });
            AddTask(new TaskModel { Id = "T-240105", Type = "补货", Priority = "中", State = TaskState.Failed, AgvId = "AGV-010", StartPoint = "W1", EndPoint = "W2", CreatedTime = now, EstimatedTime = "-" });
            AddTask(new TaskModel { Id = "T-240106", Type = "搬运", Priority = "高", State = TaskState.Running, AgvId = "AGV-011", StartPoint = "A3", EndPoint = "B1", CreatedTime = now, EstimatedTime = later });
            AddTask(new TaskModel { Id = "T-240107", Type = "补货", Priority = "中", State = TaskState.Cancelled, AgvId = "-", StartPoint = "E1", EndPoint = "E2", CreatedTime = now, EstimatedTime = "-" });
            AddTask(new TaskModel { Id = "T-240108", Type = "巡检", Priority = "低", State = TaskState.Completed, AgvId = "AGV-015", StartPoint = "F1", EndPoint = "F3", CreatedTime = now, EstimatedTime = now });
            AddTask(new TaskModel { Id = "T-240109", Type = "搬运", Priority = "中", State = TaskState.Pending, AgvId = "-", StartPoint = "A1", EndPoint = "B4", CreatedTime = now, EstimatedTime = "-" });
            AddTask(new TaskModel { Id = "T-240110", Type = "充电", Priority = "高", State = TaskState.Running, AgvId = "AGV-020", StartPoint = "G1", EndPoint = "Charge-2", CreatedTime = now, EstimatedTime = later });

            eventAggregator
                .GetEvent<RobotLowBatteryEvent>()
                .Subscribe(OnRobotLowBattery, ThreadOption.UIThread);
        }

        private void AddTask(TaskModel task)
        {
            ApplyDispatchPauseState(task);
            TaskList.Add(task);
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
    }
}
