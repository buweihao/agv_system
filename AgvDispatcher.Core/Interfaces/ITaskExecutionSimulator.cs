using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface ITaskExecutionSimulator
    {
        void Start(TaskOrder task, string vehicleId);

        void Cancel(string taskId);
    }
}
