using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public sealed class MapAreaRepository : IMapAreaRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public MapAreaRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new(_options);

        public async Task<IReadOnlyList<MapArea>> GetAllAsync(string mapId, string mapVersion)
        {
            using var db = CreateContext();
            return await db.MapAreas.AsNoTracking()
                .Where(area => area.MapId == mapId && area.MapVersion == mapVersion)
                .OrderBy(area => area.AreaId)
                .ToArrayAsync();
        }

        public async Task SaveAsync(MapArea area)
        {
            ArgumentNullException.ThrowIfNull(area);

            using var db = CreateContext();
            var existing = await db.MapAreas.FindAsync(area.MapId, area.MapVersion, area.AreaId);
            if (existing is null)
            {
                db.MapAreas.Add(area);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(area);
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(string mapId, string mapVersion, string areaId)
        {
            using var db = CreateContext();
            var area = await db.MapAreas.FindAsync(mapId, mapVersion, areaId);
            if (area is not null)
            {
                db.MapAreas.Remove(area);
                await db.SaveChangesAsync();
            }
        }
    }
}
