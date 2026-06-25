using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;

namespace AgvDispatcher.Infrastructure.Sqlite.Services
{
    public class PersistentOperationLogService : IOperationLogService
    {
        private readonly IOperationLogRepository _logs;

        public PersistentOperationLogService(IOperationLogRepository logs)
        {
            _logs = logs;
        }

        public void WriteLog(OperationLog log)
        {
            _logs.AddAsync(log).GetAwaiter().GetResult();
        }

        public IReadOnlyList<OperationLog> QueryLogs(OperationLogQuery query)
        {
            return _logs.QueryAsync(query).GetAwaiter().GetResult();
        }
    }
}
