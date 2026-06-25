using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class VehicleRepository : IVehicleRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public VehicleRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task<IReadOnlyList<Vehicle>> GetAllAsync()
        {
            using var db = CreateContext();
            return await db.Vehicles.AsNoTracking()
                .OrderBy(vehicle => vehicle.VehicleCode)
                .ToArrayAsync();
        }

        public async Task<Vehicle?> GetByIdAsync(string vehicleId)
        {
            using var db = CreateContext();
            return await db.Vehicles.AsNoTracking()
                .FirstOrDefaultAsync(vehicle => vehicle.VehicleId == vehicleId);
        }

        public async Task SaveAsync(Vehicle vehicle)
        {
            ArgumentNullException.ThrowIfNull(vehicle);

            using var db = CreateContext();
            var existing = await db.Vehicles.FindAsync(vehicle.VehicleId);
            if (existing is null)
            {
                db.Vehicles.Add(vehicle);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(vehicle);
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(string vehicleId)
        {
            using var db = CreateContext();
            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
            if (vehicle != null)
            {
                db.Vehicles.Remove(vehicle);
                await db.SaveChangesAsync();
            }
        }
    }
}
