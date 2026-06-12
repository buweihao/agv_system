using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    internal class MapLocationAliasRepository : IMapLocationAliasRepository
    {
        private readonly AgvDispatcherDbContext _db;

        public MapLocationAliasRepository(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<MapLocationAlias>> GetAllAsync()
        {
            return await _db.MapLocationAliases.AsNoTracking().ToListAsync();
        }

        public async Task SaveAsync(MapLocationAlias alias)
        {
            var existing = await _db.MapLocationAliases.FindAsync(alias.AliasId);
            if (existing == null)
            {
                _db.MapLocationAliases.Add(alias);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(alias);
            }
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(string aliasId)
        {
            var alias = await _db.MapLocationAliases.FindAsync(aliasId);
            if (alias != null)
            {
                _db.MapLocationAliases.Remove(alias);
                await _db.SaveChangesAsync();
            }
        }
    }
}
