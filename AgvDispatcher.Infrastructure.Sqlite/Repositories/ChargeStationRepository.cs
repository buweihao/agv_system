using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class ChargeStationRepository : IChargeStationRepository
    {
        private readonly AgvDispatcherDbContext _db;

        public ChargeStationRepository(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ChargeStation>> GetAllAsync()
        {
            return await _db.ChargeStations.AsNoTracking()
                .OrderBy(station => station.StationCode)
                .ToArrayAsync();
        }

        public async Task<ChargeStation?> GetByIdAsync(string stationId)
        {
            return await _db.ChargeStations.AsNoTracking()
                .FirstOrDefaultAsync(station => station.StationId == stationId);
        }

        public async Task SaveAsync(ChargeStation station)
        {
            ArgumentNullException.ThrowIfNull(station);

            var existing = await _db.ChargeStations.FindAsync(station.StationId);
            if (existing is null)
            {
                _db.ChargeStations.Add(station);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(station);
                _db.Entry(existing).Reference(item => item.Position).TargetEntry?.CurrentValues.SetValues(station.Position);
            }

            await _db.SaveChangesAsync();
        }
    }
}
