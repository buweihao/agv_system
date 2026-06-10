using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class OperationLogRepository : IOperationLogRepository
    {
        private readonly AgvDispatcherDbContext _db;

        public OperationLogRepository(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(OperationLog log)
        {
            ArgumentNullException.ThrowIfNull(log);

            if (string.IsNullOrWhiteSpace(log.LogId))
            {
                log.LogId = $"LOG-{DateTime.Now:yyyyMMddHHmmssfff}";
            }

            if (log.OccurredAt == default)
            {
                log.OccurredAt = DateTime.Now;
            }

            _db.OperationLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<OperationLog>> QueryAsync(OperationLogQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var logs = _db.OperationLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                logs = logs.Where(log => log.Category == query.Category);
            }

            if (!string.IsNullOrWhiteSpace(query.Action))
            {
                logs = logs.Where(log => log.Action == query.Action);
            }

            if (!string.IsNullOrWhiteSpace(query.Operator))
            {
                logs = logs.Where(log => log.Operator == query.Operator);
            }

            if (!string.IsNullOrWhiteSpace(query.VehicleId))
            {
                logs = logs.Where(log => log.VehicleId == query.VehicleId);
            }

            if (!string.IsNullOrWhiteSpace(query.TaskId))
            {
                logs = logs.Where(log => log.TaskId == query.TaskId);
            }

            if (query.From is not null)
            {
                logs = logs.Where(log => log.OccurredAt >= query.From.Value);
            }

            if (query.To is not null)
            {
                logs = logs.Where(log => log.OccurredAt <= query.To.Value);
            }

            return await logs
                .OrderByDescending(log => log.OccurredAt)
                .Skip(Math.Max(query.Skip, 0))
                .Take(query.Take <= 0 ? 100 : query.Take)
                .ToArrayAsync();
        }
    }
}
