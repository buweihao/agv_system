using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class TaskTemplateRepository : ITaskTemplateRepository
    {
        private readonly DbContextOptions<AgvDispatcherDbContext> _options;

        public TaskTemplateRepository(DbContextOptions<AgvDispatcherDbContext> options)
        {
            _options = options;
        }

        private AgvDispatcherDbContext CreateContext() => new AgvDispatcherDbContext(_options);

        public async Task<IReadOnlyList<TaskTemplateConfig>> GetAllAsync()
        {
            using var db = CreateContext();
            return await db.TaskTemplates.AsNoTracking()
                .OrderBy(template => template.TemplateName)
                .ToArrayAsync();
        }

        public async Task SaveAsync(TaskTemplateConfig template)
        {
            ArgumentNullException.ThrowIfNull(template);

            using var db = CreateContext();
            var existing = await db.TaskTemplates.FindAsync(template.TemplateName);
            if (existing is null)
            {
                db.TaskTemplates.Add(template);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(template);
            }

            await db.SaveChangesAsync();
        }
    }
}
