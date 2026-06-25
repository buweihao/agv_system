using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    internal class MapLocationAliasRepository : IMapLocationAliasRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public MapLocationAliasRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task<IReadOnlyList<MapLocationAlias>> GetAllAsync()
        {
            using var db = CreateContext();
            return await db.MapLocationAliases.AsNoTracking().ToListAsync();
        }

        public async Task<IReadOnlyList<MapLocationAlias>> GetAllAsync(string mapId, string mapVersion)
        {
            using var db = CreateContext();
            return await db.MapLocationAliases.AsNoTracking()
                .Where(alias => alias.MapId == mapId && alias.MapVersion == mapVersion)
                .ToListAsync();
        }

        public async Task SaveAsync(MapLocationAlias alias)
        {
            using var db = CreateContext();
            var existing = await db.MapLocationAliases.FindAsync(alias.AliasId);
            if (existing == null)
            {
                db.MapLocationAliases.Add(alias);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(alias);
            }
            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(string aliasId)
        {
            using var db = CreateContext();
            var alias = await db.MapLocationAliases.FindAsync(aliasId);
            if (alias != null)
            {
                db.MapLocationAliases.Remove(alias);
                await db.SaveChangesAsync();
            }
        }
    }
}
