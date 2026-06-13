using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class OperationLogRepository : IOperationLogRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public OperationLogRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task AddAsync(OperationLog log)
        {
            ArgumentNullException.ThrowIfNull(log);

            if (string.IsNullOrWhiteSpace(log.LogId))
            {
                log.LogId = $"LOG-{Guid.NewGuid():N}";
            }

            if (log.OccurredAt == default)
            {
                log.OccurredAt = DateTime.Now;
            }

            using var db = CreateContext();
            db.OperationLogs.Add(log);
            await db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<OperationLog>> QueryAsync(OperationLogQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            using var db = CreateContext();
            var logs = db.OperationLogs.AsNoTracking().AsQueryable();

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

            if (!string.IsNullOrWhiteSpace(query.SourceId))
            {
                logs = logs.Where(log => log.SourceId == query.SourceId);
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
