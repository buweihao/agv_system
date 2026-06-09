using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface ITaskService
    {
        IReadOnlyList<TaskOrder> GetTasks();

        TaskOrder? GetTask(string taskId);

        TaskOrder CreateTask(TaskCreateRequest request);

        void UpdateTaskState(string taskId, TaskState state, string? reason = null);

        void CancelTask(string taskId, string? reason = null);
    }
}
