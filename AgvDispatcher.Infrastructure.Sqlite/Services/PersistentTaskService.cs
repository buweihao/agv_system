using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;
using Prism.Events;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentTaskService : ITaskService
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _dbOptions;
        private readonly IEventAggregator _eventAggregator;
        private readonly IOperationLogService _operationLogService;
        private readonly object _syncRoot = new();

        public PersistentTaskService(
            DbContextOptions<AgvDispatcherDbContext> dbOptions,
            IEventAggregator eventAggregator,
            IOperationLogService operationLogService)
        {
            _dbOptions = dbOptions;
            _eventAggregator = eventAggregator;
            _operationLogService = operationLogService;
        }

        public IReadOnlyList<TaskOrder> GetTasks()
        {
            using var db = CreateContext();
            return db.TaskOrders
                .OrderByDescending(t => t.CreatedAt)
                .ToArray();
        }

        public TaskOrder? GetTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return null;
            }

            using var db = CreateContext();
            return db.TaskOrders.Find(taskId);
        }

        public TaskOrder CreateTask(TaskCreateRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var now = DateTime.Now;
            var task = new TaskOrder
            {
                TaskId = $"T-{now:HHmmssfff}",
                TaskNo = $"T-{now:yyyyMMddHHmmss}",
                TaskType = request.TaskType,
                TemplateId = request.TemplateId,
                Priority = request.Priority,
                State = TaskState.Pending,
                SourceNodeId = request.SourceNodeId,
                TargetNodeId = request.TargetNodeId,
                CargoCode = request.CargoCode,
                CargoName = request.CargoName,
                CargoWeight = request.CargoWeight,
                CreatedAt = now,
                PlannedStartAt = request.PlannedStartAt,
                DeadlineAt = request.DeadlineAt,
                CreatedBy = request.CreatedBy,
                Attributes = new Dictionary<string, string>(request.Attributes)
            };

            lock (_syncRoot)
            {
                using var db = CreateContext();
                db.TaskOrders.Add(task);
                db.SaveChanges();
            }

            WriteTaskLog("Created", task, "Task created successfully.");
            PublishTaskUpdated(task);
            return task;
        }

        public void AssignVehicle(string taskId, string vehicleId)
        {
            if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(vehicleId))
            {
                return;
            }

            TaskOrder? task;
            lock (_syncRoot)
            {
                using var db = CreateContext();
                task = db.TaskOrders.Find(taskId);
                if (task is null)
                {
                    return;
                }

                task.AssignedVehicleId = vehicleId.Trim();
                db.SaveChanges();
            }

            WriteTaskLog("Dispatched", task, $"Task dispatched to vehicle {vehicleId}.");
            PublishTaskUpdated(task);
        }

        public void UpdateTaskProgress(string taskId, int progressPercent, string? currentNodeId = null)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            TaskOrder? task;
            lock (_syncRoot)
            {
                using var db = CreateContext();
                task = db.TaskOrders.Find(taskId);
                if (task is null)
                {
                    return;
                }

                task.ProgressPercent = Math.Clamp(progressPercent, 0, 100);
                if (!string.IsNullOrWhiteSpace(currentNodeId))
                {
                    task.CurrentNodeId = currentNodeId.Trim();
                }

                db.SaveChanges();
            }

            PublishTaskUpdated(task);
        }

        public void UpdateTaskState(string taskId, TaskState state, string? reason = null)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return;
            }

            TaskOrder? task;
            lock (_syncRoot)
            {
                using var db = CreateContext();
                task = db.TaskOrders.Find(taskId);
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

                db.SaveChanges();
            }

            WriteTaskLog(state.ToString(), task, $"Task state changed to {state}. Reason: {reason ?? "N/A"}");
            PublishTaskUpdated(task);
        }

        public void CancelTask(string taskId, string? reason = null)
        {
            UpdateTaskState(taskId, TaskState.Cancelled, reason);
        }

        private AgvDispatcherDbContext CreateContext()
        {
            return new AgvDispatcherDbContext(_dbOptions);
        }

        private void PublishTaskUpdated(TaskOrder task)
        {
            _eventAggregator.GetEvent<TaskOrderUpdatedEvent>().Publish(task);
        }

        private void WriteTaskLog(string action, TaskOrder task, string message)
        {
            _operationLogService.WriteLog(new OperationLog
            {
                Category = "Task",
                Action = action,
                Message = message,
                TaskId = task.TaskId,
                VehicleId = task.AssignedVehicleId,
                Operator = "System", // Or from task context if available
                OccurredAt = DateTime.Now
            });
        }
    }
}
