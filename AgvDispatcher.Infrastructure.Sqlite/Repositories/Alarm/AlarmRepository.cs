using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class AlarmRepository : IAlarmRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public AlarmRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task<IReadOnlyList<AlarmEvent>> GetAllAsync()
        {
            using var db = CreateContext();
            return await db.Alarms.AsNoTracking()
                .OrderByDescending(alarm => alarm.OccurredAt)
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<AlarmEvent>> GetActiveAsync()
        {
            using var db = CreateContext();
            return await db.Alarms.AsNoTracking()
                .Where(alarm => alarm.State == AlarmState.Active)
                .OrderByDescending(alarm => alarm.OccurredAt)
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<AlarmEvent>> QueryAsync(AlarmQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);

            using var db = CreateContext();
            var alarms = db.Alarms.AsNoTracking().AsQueryable();

            if (query.Severity is not null)
            {
                alarms = alarms.Where(alarm => alarm.Severity == query.Severity.Value);
            }

            if (query.State is not null)
            {
                alarms = alarms.Where(alarm => alarm.State == query.State.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.AlarmCode))
            {
                alarms = alarms.Where(alarm => alarm.AlarmCode == query.AlarmCode);
            }

            if (!string.IsNullOrWhiteSpace(query.SourceType))
            {
                alarms = alarms.Where(alarm => alarm.SourceType == query.SourceType);
            }

            if (!string.IsNullOrWhiteSpace(query.SourceId))
            {
                alarms = alarms.Where(alarm => alarm.SourceId == query.SourceId);
            }

            if (!string.IsNullOrWhiteSpace(query.VehicleId))
            {
                alarms = alarms.Where(alarm => alarm.VehicleId == query.VehicleId);
            }

            if (!string.IsNullOrWhiteSpace(query.TaskId))
            {
                alarms = alarms.Where(alarm => alarm.TaskId == query.TaskId);
            }

            if (query.From is not null)
            {
                alarms = alarms.Where(alarm => alarm.OccurredAt >= query.From.Value);
            }

            if (query.To is not null)
            {
                alarms = alarms.Where(alarm => alarm.OccurredAt <= query.To.Value);
            }

            return await alarms
                .OrderByDescending(alarm => alarm.OccurredAt)
                .Skip(Math.Max(query.Skip, 0))
                .Take(query.Take <= 0 ? 100 : query.Take)
                .ToArrayAsync();
        }

        public async Task<AlarmEvent?> GetByIdAsync(string alarmId)
        {
            using var db = CreateContext();
            return await db.Alarms.AsNoTracking()
                .FirstOrDefaultAsync(alarm => alarm.AlarmId == alarmId);
        }

        public async Task SaveAsync(AlarmEvent alarm)
        {
            ArgumentNullException.ThrowIfNull(alarm);

            using var db = CreateContext();
            var existing = await db.Alarms.FindAsync(alarm.AlarmId);
            if (existing is null)
            {
                db.Alarms.Add(alarm);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(alarm);
            }

            await db.SaveChangesAsync();
        }
    }
}
