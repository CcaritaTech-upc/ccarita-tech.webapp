using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.IAM.Infrastructure.Tokens;
using IoBuild.Api.Persistence;
using IoBuild.Api.Workflows;
using IoBuild.TestKit;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests;

// Convergent Testing G1: IAM persistence guarantees on the production engine.
// InMemory proves orchestration; only MySQL 8.0 proves unique indexes,
// rollback, and revocation durability. Opt-in: skips with success when
// IOBUILD_TEST_MYSQL_CONNECTION is not configured (same pattern as the
// existing MySQL lease test). Each test uses a unique email prefix and
// deletes only the rows it created.
public sealed class IamPersistenceMySqlTests
{
    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Duplicate_registration_on_mysql_keeps_a_single_user_and_dispatch()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var email = $"g1-dup-{Guid.NewGuid():N}@example.test";
        await using var db = MySqlFixture.CreateIsolatedContext(connectionString);
        var service = CreateIamService(db);
        try
        {
            await service.RegisterAsync(new RegisterUser(email, "secret123", "Owner"));
            await service.RegisterAsync(new RegisterUser(email, "secret123", "Owner"));

            Assert.Single(await db.IamUsers.Where(u => u.Email == email).ToListAsync());
            Assert.Single(await db.IntegrationDispatches
                .Where(d => d.IdempotencyKey == $"iam.user-registered:{email}")
                .ToListAsync());
        }
        finally
        {
            await CleanupAsync(db, email);
        }
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Failed_registration_on_mysql_leaves_no_durable_state()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = MySqlFixture.CreateIsolatedContext(connectionString);
        var service = CreateIamService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(new RegisterUser("   ", "secret123", "Owner")));

        Assert.Empty(await db.IamUsers.Where(u => u.Email == string.Empty).ToListAsync());
        Assert.Empty(await db.IntegrationDispatches
            .Where(d => d.IdempotencyKey == "iam.user-registered:")
            .ToListAsync());
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.LOGOUT")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Revocation_on_mysql_is_durable_across_contexts()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var email = $"g1-revoke-{Guid.NewGuid():N}@example.test";
        await using var db = MySqlFixture.CreateIsolatedContext(connectionString);
        var service = CreateIamService(db);
        string token;
        try
        {
            await service.RegisterAsync(new RegisterUser(email, "secret123", "Owner"));
            token = (await service.SignInAsync(new SignIn(email, "secret123"))).Token;
            await service.RevokeAsync(token);
        }
        catch
        {
            await CleanupAsync(db, email);
            throw;
        }

        // A brand-new context proves the revocation survived storage, not EF tracking.
        await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
        try
        {
            var hash = IamService.HashToken(token);
            Assert.True(await reader.RevokedTokens.AnyAsync(t => t.TokenHash == hash));
            Assert.True(await CreateIamService(reader).IsRevokedAsync(token));
        }
        finally
        {
            await CleanupAsync(db, email);
            var hash = IamService.HashToken(token);
            var rows = await reader.RevokedTokens.Where(t => t.TokenHash == hash).ToListAsync();
            if (rows.Count > 0)
            {
                reader.RevokedTokens.RemoveRange(rows);
                await reader.SaveChangesAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Concurrent_duplicate_registration_on_mysql_leaves_a_single_user()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var email = $"g1-race-{Guid.NewGuid():N}@example.test";
        await using var dbA = MySqlFixture.CreateIsolatedContext(connectionString);
        await using var dbB = MySqlFixture.CreateIsolatedContext(connectionString);

        await Task.WhenAll(
            Task.Run(() => CreateIamService(dbA).RegisterAsync(new RegisterUser(email, "secret123", "Owner"))),
            Task.Run(() => CreateIamService(dbB).RegisterAsync(new RegisterUser(email, "secret123", "Owner"))));

        await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
        try
        {
            Assert.Single(await reader.IamUsers.Where(u => u.Email == email).ToListAsync());
            Assert.Single(await reader.IntegrationDispatches
                .Where(d => d.IdempotencyKey == $"iam.user-registered:{email}")
                .ToListAsync());
        }
        finally
        {
            await CleanupAsync(reader, email);
        }
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Failed_registration_after_intermediate_write_rolls_back_on_mysql()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var email = $"g1-rb-{Guid.NewGuid():N}@example.test";
        await using var db = MySqlFixture.CreateIsolatedContext(connectionString);
        var passwordHasher = new PasswordHasher();
        var service = new IamService(
            db,
            passwordHasher,
            new JwtTokenIssuer("a-test-secret-that-is-long-enough-for-hmac"),
            new RegisterUserWorkflow(db, passwordHasher, new ExplodingQueue(), new WorkflowExecutor(db)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(new RegisterUser(email, "secret123", "Owner")));

        // A fresh context proves nothing durable survived — the user row written
        // before the failure was rolled back with the transaction.
        await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
        Assert.Empty(await reader.IamUsers.Where(u => u.Email == email).ToListAsync());
        Assert.Empty(await reader.IntegrationDispatches
            .Where(d => d.IdempotencyKey == $"iam.user-registered:{email}")
            .ToListAsync());
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "C")]
    [Trait("Dependency", "MySql")]
    public async Task Burst_duplicate_registration_on_mysql_stays_single_without_deadlock()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var email = $"g1-burst-{Guid.NewGuid():N}@example.test";
        var contexts = Enumerable.Range(0, 8)
            .Select(_ => MySqlFixture.CreateIsolatedContext(connectionString))
            .ToList();
        try
        {
            // Eight racers on eight contexts: every call must return (none may
            // throw or deadlock on the unique index) and exactly one account wins.
            await Task.WhenAll(contexts.Select(db =>
                Task.Run(() => CreateIamService(db).RegisterAsync(new RegisterUser(email, "secret123", "Owner")))));

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            Assert.Single(await reader.IamUsers.Where(u => u.Email == email).ToListAsync());
            Assert.Single(await reader.IntegrationDispatches
                .Where(d => d.IdempotencyKey == $"iam.user-registered:{email}")
                .ToListAsync());
        }
        finally
        {
            foreach (var db in contexts) await db.DisposeAsync();
            await using var cleaner = MySqlFixture.CreateIsolatedContext(connectionString);
            await CleanupAsync(cleaner, email);
        }
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "C")]
    [Trait("Dependency", "MySql")]
    public async Task Iam_data_survives_migration_to_latest_on_mysql()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var scratch = $"g1_mig_{Guid.NewGuid():N}";
        await using (var admin = MySqlFixture.CreateIsolatedContext(connectionString))
        {
            await admin.Database.ExecuteSqlRawAsync("CREATE DATABASE `" + scratch + "`");
        }
        try
        {
            var scratchConnection = new MySqlConnector.MySqlConnectionStringBuilder(connectionString) { Database = scratch }.ConnectionString;
            static IoBuildDbContext OpenScratch(string cs) => new(new DbContextOptionsBuilder<IoBuildDbContext>()
                .UseMySql(cs, ServerVersion.AutoDetect(cs)).Options);

            var email = $"mig-{Guid.NewGuid():N}@example.test";
            await using (var db = OpenScratch(scratchConnection))
            {
                await db.Database.MigrateAsync();
                db.IamUsers.Add(new IamUser { Email = email, PasswordHash = "hash", Role = "Owner" });
                await db.SaveChangesAsync();
            }
            await using (var db = OpenScratch(scratchConnection))
            {
                // Production startup path (Migrations__ApplyOnStartup): migrate
                // over existing data. Must be a no-op that preserves every row.
                await db.Database.MigrateAsync();
                Assert.Equal(
                    new[] { "202608280001_FoundationSchema", "202608290002_IamAndDispatch", "202608290003_CoreBusiness", "202608300004_DevicesTelemetry", "202608300005_AnalyticsProjections", "202609170006_SubscriptionActiveArbiter" },
                    db.Database.GetAppliedMigrations());
                var survivor = await db.IamUsers.SingleAsync(u => u.Email == email);
                Assert.Equal("Owner", survivor.Role);
            }
        }
        finally
        {
            await using var admin = MySqlFixture.CreateIsolatedContext(connectionString);
            await admin.Database.ExecuteSqlRawAsync("DROP DATABASE `" + scratch + "`");
        }
    }

    private sealed class ExplodingQueue : IIntegrationDispatchQueue
    {
        public Task<IntegrationDispatch> EnqueueAsync(DispatchRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated post-write failure.");
        public Task<IntegrationDispatch?> LeaseDueAsync(string worker, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task CompleteAsync(long id, string worker, DateTimeOffset now, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task FailAsync(long id, string worker, DateTimeOffset now, bool retryable, int maxAttempts, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task ReplayAsync(long id, DateTimeOffset now, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private static IamService CreateIamService(IoBuildDbContext db)
    {
        var passwordHasher = new PasswordHasher();
        var queue = new IntegrationDispatchQueue(db);
        var workflow = new RegisterUserWorkflow(
            db, passwordHasher, queue, new WorkflowExecutor(db));
        return new IamService(
            db, passwordHasher, new JwtTokenIssuer("a-test-secret-that-is-long-enough-for-hmac"), workflow);
    }

    private static async Task CleanupAsync(IoBuildDbContext db, string email)
    {
        var users = await db.IamUsers.Where(u => u.Email == email).ToListAsync();
        if (users.Count > 0) db.IamUsers.RemoveRange(users);
        var dispatches = await db.IntegrationDispatches
            .Where(d => d.IdempotencyKey == $"iam.user-registered:{email}")
            .ToListAsync();
        if (dispatches.Count > 0) db.IntegrationDispatches.RemoveRange(dispatches);
        await db.SaveChangesAsync();
    }
}
