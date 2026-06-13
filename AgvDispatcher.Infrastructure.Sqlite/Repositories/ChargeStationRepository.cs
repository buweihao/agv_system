using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class ChargeStationRepository : IChargeStationRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public ChargeStationRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task<IReadOnlyList<ChargeStation>> GetAllAsync()
        {
            using var db = CreateContext();
            return await db.ChargeStations.AsNoTracking()
                .OrderBy(station => station.StationCode)
                .ToArrayAsync();
        }

        public async Task<ChargeStation?> GetByIdAsync(string stationId)
        {
            using var db = CreateContext();
            return await db.ChargeStations.AsNoTracking()
                .FirstOrDefaultAsync(station => station.StationId == stationId);
        }

        public async Task SaveAsync(ChargeStation station)
        {
            ArgumentNullException.ThrowIfNull(station);

            using var db = CreateContext();
            var existing = await db.ChargeStations.FindAsync(station.StationId);
            if (existing is null)
            {
                db.ChargeStations.Add(station);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(station);
                db.Entry(existing).Reference(item => item.Position).TargetEntry?.CurrentValues.SetValues(station.Position);
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(string stationId)
        {
            using var db = CreateContext();
            var existing = await db.ChargeStations.FindAsync(stationId);
            if (existing is not null)
            {
                db.ChargeStations.Remove(existing);
                await db.SaveChangesAsync();
            }
        }

        public async Task<IReadOnlyList<ChargeSessionRecord>> GetSessionsAsync(string? vehicleId = null, string? stationId = null)
        {
            using var db = CreateContext();
            var query = db.ChargeSessionRecords.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(vehicleId))
            {
                query = query.Where(s => s.VehicleId == vehicleId);
            }
            if (!string.IsNullOrWhiteSpace(stationId))
            {
                query = query.Where(s => s.StationId == stationId);
            }
            return await query.OrderByDescending(s => s.StartTime).ToArrayAsync();
        }

        public async Task AddSessionAsync(ChargeSessionRecord session)
        {
            ArgumentNullException.ThrowIfNull(session);
            using var db = CreateContext();
            db.ChargeSessionRecords.Add(session);
            await db.SaveChangesAsync();
        }

        public async Task UpdateSessionAsync(ChargeSessionRecord session)
        {
            ArgumentNullException.ThrowIfNull(session);
            using var db = CreateContext();
            var existing = await db.ChargeSessionRecords.FindAsync(session.SessionId);
            if (existing is not null)
            {
                db.Entry(existing).CurrentValues.SetValues(session);
                await db.SaveChangesAsync();
            }
        }
    }
}
