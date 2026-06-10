using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class MapRepository : IMapRepository
    {
        private readonly AgvDispatcherDbContext _db;

        public MapRepository(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<MapNode>> GetNodesAsync()
        {
            return await _db.MapNodes.AsNoTracking()
                .OrderBy(node => node.NodeCode)
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<MapEdge>> GetEdgesAsync()
        {
            return await _db.MapEdges.AsNoTracking()
                .OrderBy(edge => edge.EdgeId)
                .ToArrayAsync();
        }

        public async Task SaveNodeAsync(MapNode node)
        {
            ArgumentNullException.ThrowIfNull(node);

            var existing = await _db.MapNodes.FindAsync(node.NodeId);
            if (existing is null)
            {
                _db.MapNodes.Add(node);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(node);
                _db.Entry(existing).Reference(item => item.Position).TargetEntry?.CurrentValues.SetValues(node.Position);
            }

            await _db.SaveChangesAsync();
        }

        public async Task SaveEdgeAsync(MapEdge edge)
        {
            ArgumentNullException.ThrowIfNull(edge);

            var existing = await _db.MapEdges.FindAsync(edge.EdgeId);
            if (existing is null)
            {
                _db.MapEdges.Add(edge);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(edge);
            }

            await _db.SaveChangesAsync();
        }
    }
}
