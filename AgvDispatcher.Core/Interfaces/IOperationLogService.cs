using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Core.Interfaces
{
    public interface IOperationLogService
    {
        void WriteLog(OperationLog log);

        IReadOnlyList<OperationLog> QueryLogs(OperationLogQuery query);
    }
}
