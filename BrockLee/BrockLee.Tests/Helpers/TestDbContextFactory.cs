// Tests use EF Core InMemory provider — not SQLite, not Azure SQL.
// This keeps unit/integration tests fast and self-contained with
// no external dependencies.
// Azure SQL is only used by the running application itself.

using BrockLee.Data;
using Microsoft.EntityFrameworkCore;

namespace BrockLee.Tests.Helpers;

public static class TestDbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}