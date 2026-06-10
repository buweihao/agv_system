namespace AgvDispatcher.Infrastructure.Sqlite.Persistence
{
    public class LocalPersistenceInitializer
    {
        private readonly AgvDispatcherDbContext _db;

        public LocalPersistenceInitializer(AgvDispatcherDbContext db)
        {
            _db = db;
        }

        public void Initialize()
        {
            LocalPersistenceSeeder.EnsureSeedData(_db);
        }
    }
}
