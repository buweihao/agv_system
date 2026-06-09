using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Mock
{
    public class MockTaskService : ITaskService
    {
        private readonly List<TaskOrder> _tasks = MockData.CreateTasks(DateTime.Now).ToList();

        public IReadOnlyList<TaskOrder> GetTasks()
        {
            return _tasks.ToArray();
        }

        public TaskOrder? GetTask(string taskId)
        {
            return _tasks.FirstOrDefault(task =>
                string.Equals(task.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
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

            _tasks.Insert(0, task);
            return task;
        }

        public void UpdateTaskState(string taskId, TaskState state, string? reason = null)
        {
            var task = GetTask(taskId);
            if (task is null)
            {
                return;
            }

            task.State = state;
            task.FailureReason = state == TaskState.Failed ? reason : task.FailureReason;
            task.CancelReason = state == TaskState.Cancelled ? reason : task.CancelReason;
            task.FinishedAt = state is TaskState.Completed or TaskState.Failed or TaskState.Cancelled
                ? DateTime.Now
                : task.FinishedAt;
        }

        public void CancelTask(string taskId, string? reason = null)
        {
            UpdateTaskState(taskId, TaskState.Cancelled, reason);
        }
    }
}
