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
// The database must already exist; the schema is created on first use through
// the production path (EfMigrationRunner). Each test uses a unique email prefix
// so rows never collide, and deletes only the rows it created.
public static class MySqlFixture
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("IOBUILD_TEST_MYSQL_CONNECTION") is { } value &&
        !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static readonly object SchemaLock = new();
    private static readonly HashSet<string> SchemaReady = new(StringComparer.Ordinal);

    // Creates the schema through the same path production uses
    // (EfMigrationRunner: EnsureCreated plus the column upgrades), once per
    // connection string and process. Test classes run in parallel, so the
    // lock keeps two of them from creating the schema at the same time. The
    // connection is marked ready only after it succeeds, so a failure is
    // reported by every test instead of being hidden.
    public static void EnsureSchema(string connectionString)
    {
        lock (SchemaLock)
        {
            if (SchemaReady.Contains(connectionString)) return;
            using var db = Create(connectionString);
            new EfMigrationRunner(db).ApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
            // Seed once, before any test host starts. DataSeeder checks "any
            // plans?" and then inserts, which is not atomic: several hosts
            // starting together on an empty database would all insert and hit
            // the unique index on plans.Name. Seeded first, every host sees
            // existing rows and skips the insert.
            DataSeeder.SeedAsync(db, CancellationToken.None).GetAwaiter().GetResult();
            ReserveProbeIds(db);
            SchemaReady.Add(connectionString);
        }
    }

    // Persistence tests insert probe rows with fixed ids (91xxx). Classes run in
    // parallel on one database, and each explicit insert moves AUTO_INCREMENT
    // forward, so a generated id could take a probe id first ("Duplicate entry
    // for key PRIMARY"). Generated ids start far above that range instead.
    private static void ReserveProbeIds(IoBuildDbContext db)
    {
        foreach (var table in new[] { "iam_users", "projects", "units", "devices", "clients", "subscriptions" })
        {
#pragma warning disable EF1002 // table names are the constants above
            db.Database.ExecuteSqlRaw($"ALTER TABLE {table} AUTO_INCREMENT = 1000000");
#pragma warning restore EF1002
        }
    }

    private static IoBuildDbContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<IoBuildDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)).Options);

    public static IoBuildDbContext CreateIsolatedContext(string connectionString)
    {
        EnsureSchema(connectionString);
        return Create(connectionString);
    }
}
