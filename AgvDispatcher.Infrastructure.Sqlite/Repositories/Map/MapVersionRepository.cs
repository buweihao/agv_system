using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public sealed class MapVersionRepository : IMapVersionRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public MapVersionRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task<MapVersionEntity?> GetActiveAsync()
        {
            using var db = CreateContext();
            return await db.MapVersions.AsNoTracking()
                .Where(version => version.IsActive || version.State == MapState.Active)
                .OrderByDescending(version => version.ActivatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<MapVersionEntity?> GetAsync(string mapId, string mapVersion)
        {
            using var db = CreateContext();
            return await db.MapVersions.AsNoTracking()
                .FirstOrDefaultAsync(version => version.MapId == mapId && version.MapVersion == mapVersion);
        }

        public async Task<IReadOnlyList<MapVersionEntity>> GetAllAsync(string? mapId = null)
        {
            using var db = CreateContext();
            var query = db.MapVersions.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(mapId))
            {
                query = query.Where(version => version.MapId == mapId);
            }

            return await query
                .OrderByDescending(version => version.IsActive)
                .ThenByDescending(version => version.UpdatedAt)
                .ToArrayAsync();
        }

        public async Task SaveAsync(MapVersionEntity version)
        {
            using var db = CreateContext();
            var existing = await db.MapVersions
                .FirstOrDefaultAsync(item => item.MapId == version.MapId && item.MapVersion == version.MapVersion);
            if (existing is null)
            {
                db.MapVersions.Add(version);
            }
            else
            {
                version.Id = existing.Id;
                db.Entry(existing).CurrentValues.SetValues(version);
            }

            await db.SaveChangesAsync();
        }

        public async Task SetActiveAsync(string mapId, string mapVersion)
        {
            using var db = CreateContext();
            var versions = await db.MapVersions.ToListAsync();
            foreach (var version in versions)
            {
                var isTarget = version.MapId == mapId && version.MapVersion == mapVersion;
                if (isTarget)
                {
                    version.State = MapState.Active;
                    version.IsActive = true;
                    version.ActivatedAt = DateTimeOffset.Now;
                    version.UpdatedAt = version.ActivatedAt.Value;
                }
                else if (version.IsActive || version.State == MapState.Active)
                {
                    version.State = MapState.Archived;
                    version.IsActive = false;
                    version.UpdatedAt = DateTimeOffset.Now;
                }
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(string mapId, string mapVersion)
        {
            using var db = CreateContext();
            var version = await db.MapVersions
                .FirstOrDefaultAsync(item => item.MapId == mapId && item.MapVersion == mapVersion);
            if (version is not null)
            {
                db.MapVersions.Remove(version);
                await db.SaveChangesAsync();
            }
        }
    }
}
