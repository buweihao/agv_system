using System;
using System.Collections.ObjectModel;
using AgvDispatcher.Core.Enums;
using AgvDispatcher.Modules.TaskModule.Models;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.TaskModule.ViewModels
{
    public class TaskMainPanelViewModel : BindableBase
    {
        private ObservableCollection<TaskModel> _taskList = new();
        public ObservableCollection<TaskModel> TaskList
        {
            get => _taskList;
            set => SetProperty(ref _taskList, value);
        }

        public TaskMainPanelViewModel()
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string later = DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm:ss");

            TaskList.Add(new TaskModel { Id = "T-240101", Type = "搬运", Priority = "高", State = TaskState.Running, AgvId = "AGV-001", StartPoint = "A1", EndPoint = "B2", CreatedTime = now, EstimatedTime = later });
            TaskList.Add(new TaskModel { Id = "T-240102", Type = "搬运", Priority = "中", State = TaskState.Running, AgvId = "AGV-002", StartPoint = "A2", EndPoint = "B3", CreatedTime = now, EstimatedTime = later });
            TaskList.Add(new TaskModel { Id = "T-240103", Type = "充电", Priority = "高", State = TaskState.Pending, AgvId = "AGV-005", StartPoint = "C1", EndPoint = "Charge-1", CreatedTime = now, EstimatedTime = "-" });
            TaskList.Add(new TaskModel { Id = "T-240104", Type = "巡检", Priority = "低", State = TaskState.Completed, AgvId = "AGV-008", StartPoint = "D1", EndPoint = "D2", CreatedTime = now, EstimatedTime = now });
            TaskList.Add(new TaskModel { Id = "T-240105", Type = "补货", Priority = "中", State = TaskState.Failed, AgvId = "AGV-010", StartPoint = "W1", EndPoint = "W2", CreatedTime = now, EstimatedTime = "-" });
            TaskList.Add(new TaskModel { Id = "T-240106", Type = "搬运", Priority = "高", State = TaskState.Running, AgvId = "AGV-011", StartPoint = "A3", EndPoint = "B1", CreatedTime = now, EstimatedTime = later });
            TaskList.Add(new TaskModel { Id = "T-240107", Type = "补货", Priority = "中", State = TaskState.Cancelled, AgvId = "-", StartPoint = "E1", EndPoint = "E2", CreatedTime = now, EstimatedTime = "-" });
            TaskList.Add(new TaskModel { Id = "T-240108", Type = "巡检", Priority = "低", State = TaskState.Completed, AgvId = "AGV-015", StartPoint = "F1", EndPoint = "F3", CreatedTime = now, EstimatedTime = now });
            TaskList.Add(new TaskModel { Id = "T-240109", Type = "搬运", Priority = "中", State = TaskState.Pending, AgvId = "-", StartPoint = "A1", EndPoint = "B4", CreatedTime = now, EstimatedTime = "-" });
            TaskList.Add(new TaskModel { Id = "T-240110", Type = "充电", Priority = "高", State = TaskState.Running, AgvId = "AGV-020", StartPoint = "G1", EndPoint = "Charge-2", CreatedTime = now, EstimatedTime = later });
        }
    }
}
