using AgvDispatcher.Core.Interfaces;
using AgvDispatcher.Core.Models;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgvDispatcher.Infrastructure.Sqlite.Repositories
{
    public class TaskTemplateRepository : ITaskTemplateRepository
    {
        private readonly AgvDispatcherDbContext _db;

        public TaskTemplateRepository(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<TaskTemplateConfig>> GetAllAsync()
        {
            return await _db.TaskTemplates.AsNoTracking()
                .OrderBy(template => template.TemplateName)
                .ToArrayAsync();
        }

        public async Task SaveAsync(TaskTemplateConfig template)
        {
            ArgumentNullException.ThrowIfNull(template);

            var existing = await _db.TaskTemplates.FindAsync(template.TemplateName);
            if (existing is null)
            {
                _db.TaskTemplates.Add(template);
            }
            else
            {
                _db.Entry(existing).CurrentValues.SetValues(template);
            }

            await _db.SaveChangesAsync();
        }
    }
}
