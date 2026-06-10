using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class AlarmRepository : IAlarmRepository
    {
        private readonly AgvDispatcherDbContext _db;

        public AlarmRepository(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<AlarmEvent>> GetAllAsync()
        {
            return await _db.Alarms.AsNoTracking()
                .OrderByDescending(alarm => alarm.OccurredAt)
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<AlarmEvent>> GetActiveAsync()
        {
            return await _db.Alarms.AsNoTracking()
                .Where(alarm => alarm.State == AlarmState.Active)
                .OrderByDescending(alarm => alarm.OccurredAt)
                .ToArrayAsync();
        }

        public async Task<AlarmEvent?> GetByIdAsync(string alarmId)
        {
            return await _db.Alarms.AsNoTracking()
                .FirstOrDefaultAsync(alarm => alarm.AlarmId == alarmId);
        }

        public async Task SaveAsync(AlarmEvent alarm)
        {
            ArgumentNullException.ThrowIfNull(alarm);

            var existing = await _db.Alarms.FindAsync(alarm.AlarmId);
            if (existing is null)
            {
                _db.Alarms.Add(alarm);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(alarm);
            }

            await _db.SaveChangesAsync();
        }
    }
}
