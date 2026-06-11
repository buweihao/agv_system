using System;
using AgvDispatcher.Infrastructure.Sqlite.Persistence;
using Microsoft.EntityFrameworkCore;
using System.IO;

class Program
{
    static void Main()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbPath = Path.Combine(appData, ""AgvDispatcher"", ""test.db"");
        if(File.Exists(dbPath)) File.Delete(dbPath);
        var options = new DbContextOptionsBuilder<AgvDispatcherDbContext>().UseSqlite($""Data Source={dbPath}"").Options;
        using var db = new AgvDispatcherDbContext(options);
        db.Database.EnsureCreated();
        Console.WriteLine(""Tables created in test.db:"");
        var connection = db.Database.GetDbConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = ""SELECT name FROM sqlite_master WHERE type='table'"";
        using var reader = cmd.ExecuteReader();
        while(reader.Read()) Console.WriteLine(reader.GetString(0));
    }
}
