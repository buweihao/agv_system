using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using Prism.Events;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockTaskService : ITaskService
    {
        private readonly List<TaskOrder> _tasks = MockData.CreateTasks(DateTime.Now).ToList();
        private readonly IEventAggregator _eventAggregator;
        private readonly object _syncRoot = new();

        public MockTaskService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public IReadOnlyList<TaskOrder> GetTasks()
        {
            lock (_syncRoot)
            {
                return _tasks.ToArray();
            }
        }

        public TaskOrder? GetTask(string taskId)
        {
            lock (_syncRoot)
            {
                return FindTaskNoLock(taskId);
            }
        }

        public TaskOrder CreateTask(TaskCreateRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var task = new TaskOrder
            {
                TaskId = $"T-{DateTime.Now:HHmmssfff}",
                TaskNo = $"T-{DateTime.Now:yyyyMMddHHmmss}",
                TaskType = request.TaskType,
                TemplateId = request.TemplateId,
                Priority = request.Priority,
                State = TaskState.Pending,
                SourceNodeId = request.SourceNodeId,
                TargetNodeId = request.TargetNodeId,
                CargoCode = request.CargoCode,
                CargoName = request.CargoName,
                CargoWeight = request.CargoWeight,
                CreatedAt = DateTime.Now,
                PlannedStartAt = request.PlannedStartAt,
                DeadlineAt = request.DeadlineAt,
                CreatedBy = request.CreatedBy,
                Attributes = new Dictionary<string, string>(request.Attributes)
            };

            lock (_syncRoot)
            {
                _tasks.Insert(0, task);
            }

            PublishTaskUpdated(task);
            return task;
        }

        public void AssignVehicle(string taskId, string vehicleId)
        {
            TaskOrder? task;
            lock (_syncRoot)
            {
                task = FindTaskNoLock(taskId);
                if (task is null || string.IsNullOrWhiteSpace(vehicleId))
                {
                    return;
                }

                task.AssignedVehicleId = vehicleId.Trim();
            }

            PublishTaskUpdated(task);
        }

        public void UpdateTaskProgress(string taskId, int progressPercent, string? currentNodeId = null)
        {
            TaskOrder? task;
            lock (_syncRoot)
            {
                task = FindTaskNoLock(taskId);
                if (task is null)
                {
                    return;
                }

                task.ProgressPercent = Math.Clamp(progressPercent, 0, 100);
                if (!string.IsNullOrWhiteSpace(currentNodeId))
                {
                    task.CurrentNodeId = currentNodeId.Trim();
                }
            }

            PublishTaskUpdated(task);
        }

        public void UpdateTaskState(string taskId, TaskState state, string? reason = null)
        {
            TaskOrder? task;
            lock (_syncRoot)
            {
                task = FindTaskNoLock(taskId);
                if (task is null)
                {
                    return;
                }

                task.State = state;
                task.StartedAt = state == TaskState.Running && task.StartedAt is null
                    ? DateTime.Now
                    : task.StartedAt;
                task.FailureReason = state == TaskState.Failed ? reason : task.FailureReason;
                task.CancelReason = state == TaskState.Cancelled ? reason : task.CancelReason;
                task.FinishedAt = state is TaskState.Completed or TaskState.Failed or TaskState.Cancelled
                    ? DateTime.Now
                    : task.FinishedAt;
                task.ProgressPercent = state == TaskState.Completed ? 100 : task.ProgressPercent;
            }

            PublishTaskUpdated(task);
        }

        public void CancelTask(string taskId, string? reason = null)
        {
            UpdateTaskState(taskId, TaskState.Cancelled, reason);
        }

        private void PublishTaskUpdated(TaskOrder task)
        {
            _eventAggregator.GetEvent<TaskOrderUpdatedEvent>().Publish(task);
        }

        private TaskOrder? FindTaskNoLock(string taskId)
        {
            return _tasks.FirstOrDefault(task =>
                string.Equals(task.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
