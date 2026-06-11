using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface ITaskService
    {
        IReadOnlyList<TaskOrder> GetTasks();

        TaskOrder? GetTask(string taskId);

        TaskOrder CreateTask(TaskCreateRequest request);

        void AssignVehicle(string taskId, string vehicleId);

        void UpdateTaskProgress(string taskId, int progressPercent, string? currentNodeId = null);

        void UpdateTaskState(string taskId, TaskState state, string? reason = null);

        void CancelTask(string taskId, string? reason = null);

        void RequeueInterruptedTask(string taskId, string? reason = null);

        void CompleteInterruptedTaskManually(string taskId, string? reason = null);

        void FailInterruptedTask(string taskId, string? reason = null);
    }
}
