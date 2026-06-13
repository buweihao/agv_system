using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class SystemParameterRepository : ISystemParameterRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public SystemParameterRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task<IReadOnlyList<ParameterConfig>> GetAllAsync()
        {
            using var db = CreateContext();
            return await db.SystemParameters.AsNoTracking()
                .OrderBy(parameter => parameter.ParamKey)
                .ToArrayAsync();
        }

        public async Task SaveAsync(ParameterConfig parameter)
        {
            ArgumentNullException.ThrowIfNull(parameter);

            using var db = CreateContext();
            var existing = await db.SystemParameters.FindAsync(parameter.ParamKey);
            if (existing is null)
            {
                db.SystemParameters.Add(parameter);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(parameter);
            }

            await db.SaveChangesAsync();
        }
    }
}
