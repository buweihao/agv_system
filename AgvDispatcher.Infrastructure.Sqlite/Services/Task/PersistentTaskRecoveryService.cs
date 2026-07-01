using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Events;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;
using Prism.Events;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentTaskRecoveryService : ITaskRecoveryService
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _dbOptions;
        private readonly IEventAggregator _eventAggregator;
        private readonly IOperationLogService _operationLogService;

        public PersistentTaskRecoveryService(
            DbContextOptions<AgvDispatcherDbContext> dbOptions,
            IEventAggregator eventAggregator,
            IOperationLogService operationLogService)
        {
            _dbOptions = dbOptions;
            _eventAggregator = eventAggregator;
            _operationLogService = operationLogService;
        }

        public void RecoverInterruptedTasks()
        {
            using var db = new AgvDispatcherDbContext(_dbOptions);
            
            var runningTasks = db.TaskOrders
                .Where(t => t.State == TaskState.Running)
                .ToList();

            if (runningTasks.Count == 0)
            {
                return;
            }

            foreach (var task in runningTasks)
            {
                task.State = TaskState.Interrupted;
                
                _operationLogService.WriteLog(new OperationLog
                {
                    Category = "Task",
                    Action = "Interrupted",
                    Message = "Task was interrupted due to system restart or failure.",
                    TaskId = task.TaskId,
                    VehicleId = task.AssignedVehicleId,
                    Operator = "System",
                    OccurredAt = DateTime.Now
                });
            }

            db.SaveChanges();

            foreach (var task in runningTasks)
            {
                _eventAggregator.GetEvent<TaskOrderUpdatedEvent>().Publish(task);
            }
        }
    }
}
