using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class SystemParameterRepository : ISystemParameterRepository
    {
        private readonly AgvDispatcherDbContext _db;

        public SystemParameterRepository(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ParameterConfig>> GetAllAsync()
        {
            return await _db.SystemParameters.AsNoTracking()
                .OrderBy(parameter => parameter.ParamKey)
                .ToArrayAsync();
        }

        public async Task SaveAsync(ParameterConfig parameter)
        {
            ArgumentNullException.ThrowIfNull(parameter);

            var existing = await _db.SystemParameters.FindAsync(parameter.ParamKey);
            if (existing is null)
            {
                _db.SystemParameters.Add(parameter);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(parameter);
            }

            await _db.SaveChangesAsync();
        }
    }
}
