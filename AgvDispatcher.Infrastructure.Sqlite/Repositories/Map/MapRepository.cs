using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class MapRepository : IMapRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public MapRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task<IReadOnlyList<MapNode>> GetNodesAsync()
        {
            using var db = CreateContext();
            return await db.MapNodes.AsNoTracking()
                .OrderBy(node => node.NodeCode)
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<MapNode>> GetNodesAsync(string mapId, string mapVersion)
        {
            using var db = CreateContext();
            return await db.MapNodes.AsNoTracking()
                .Where(node => node.MapId == mapId && node.MapVersion == mapVersion)
                .OrderBy(node => node.NodeCode)
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<MapEdge>> GetEdgesAsync()
        {
            using var db = CreateContext();
            return await db.MapEdges.AsNoTracking()
                .OrderBy(edge => edge.EdgeId)
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<MapEdge>> GetEdgesAsync(string mapId, string mapVersion)
        {
            using var db = CreateContext();
            return await db.MapEdges.AsNoTracking()
                .Where(edge => edge.MapId == mapId && edge.MapVersion == mapVersion)
                .OrderBy(edge => edge.EdgeId)
                .ToArrayAsync();
        }

        public async Task SaveNodeAsync(MapNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            using var db = CreateContext();
            var existing = await db.MapNodes.FindAsync(node.MapId, node.MapVersion, node.NodeId);
            if (existing is null)
            {
                db.MapNodes.Add(node);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(node);
                db.Entry(existing).Reference(item => item.Position).TargetEntry?.CurrentValues.SetValues(node.Position);
            }

            await db.SaveChangesAsync();
        }

        public async Task SaveEdgeAsync(MapEdge edge)
        {
            ArgumentNullException.ThrowIfNull(edge);

            using var db = CreateContext();
            var existing = await db.MapEdges.FindAsync(edge.MapId, edge.MapVersion, edge.EdgeId);
            if (existing is null)
            {
                db.MapEdges.Add(edge);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(edge);
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteNodeAsync(string nodeId)
        {
            using var db = CreateContext();
            var node = await db.MapNodes.FirstOrDefaultAsync(item => item.NodeId == nodeId);
            if (node is not null)
            {
                db.MapNodes.Remove(node);
                await db.SaveChangesAsync();
            }
        }

        public async Task DeleteEdgeAsync(string edgeId)
        {
            using var db = CreateContext();
            var edge = await db.MapEdges.FirstOrDefaultAsync(item => item.EdgeId == edgeId);
            if (edge is not null)
            {
                db.MapEdges.Remove(edge);
                await db.SaveChangesAsync();
            }
        }
    }
}
