using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class VehicleRepository : IVehicleRepository
    {
        private readonly AgvDispatcherDbContext _db;

        public VehicleRepository(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<Vehicle>> GetAllAsync()
        {
            return await _db.Vehicles.AsNoTracking()
                .OrderBy(vehicle => vehicle.VehicleCode)
                .ToArrayAsync();
        }

        public async Task<Vehicle?> GetByIdAsync(string vehicleId)
        {
            return await _db.Vehicles.AsNoTracking()
                .FirstOrDefaultAsync(vehicle => vehicle.VehicleId == vehicleId);
        }

        public async Task SaveAsync(Vehicle vehicle)
        {
            ArgumentNullException.ThrowIfNull(vehicle);

            var existing = await _db.Vehicles.FindAsync(vehicle.VehicleId);
            if (existing is null)
            {
                _db.Vehicles.Add(vehicle);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(vehicle);
            }

            await _db.SaveChangesAsync();
        }
    }
}
