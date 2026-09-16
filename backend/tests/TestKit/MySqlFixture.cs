using Microsoft.EntityFrameworkCore;
using IoBuild.Api.Persistence;

namespace IoBuild.TestKit;

// Convergent Testing: production-engine fixture for persistence guarantees.
// InMemory proves orchestration; only MySQL 8.0 proves transactions,
// unique indexes, collation, migrations and durability.
//
// Usage (opt-in, never in the fast gate):
//   var connection = MySqlFixture.ConnectionString; // null when not configured
//   if (connection is null) return; // skip with success, like the existing lease test
//   await using var db = MySqlFixture.CreateIsolatedContext(connection);
//   ... assert, then MySqlFixture.Cleanup(db) ...
//
// Configure with IOBUILD_TEST_MYSQL_CONNECTION, e.g.
//   Server=127.0.0.1;Port=3306;Database=iobuild_test;User=root;Password=iobuild
// The database must already exist; each test uses a unique email prefix so
// rows never collide, and deletes only the rows it created.
public static class MySqlFixture
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("IOBUILD_TEST_MYSQL_CONNECTION") is { } value &&
        !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    public static IoBuildDbContext CreateIsolatedContext(string connectionString) =>
        new(new DbContextOptionsBuilder<IoBuildDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)).Options);
}
