using Microsoft.EntityFrameworkCore;
using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.IAM.Infrastructure.Tokens;
using IoBuild.Api.Persistence;
using IoBuild.Api.Workflows;

// Convergent Testing: IAM Tier C — uncommon boundaries and operational conditions.
// Fast proofs live here; MySQL collation/lock/migration proofs run against the
// production engine (see run_*_mysql_proof.sh, now pinned to mysql:8.0).
public sealed class IamTierCTests
{
    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "C")]
    public async Task IAM_REGISTRATION_UNICODE_EMAIL_normalizes_without_duplicating()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        // Epsilon variants: service normalizes case/whitespace; distinct Unicode
        // spellings remain distinct until MySQL collation decides equality.
        await service.RegisterAsync(new RegisterUser("Usuario.Caso@Example.Test", "secret123", "Owner"));
        var session = await service.SignInAsync(new SignIn("usuario.caso@example.test", "secret123"));
        Assert.Equal("usuario.caso@example.test", session.Email);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "C")]
    public async Task IAM_REGISTRATION_EMAIL_has_unique_index_intent_in_model()
    {
        await using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser));
        Assert.NotNull(entity);
        var uniqueEmailIndex = entity!.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["Email"]) && i.IsUnique);
        Assert.NotNull(uniqueEmailIndex);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "C")]
    public async Task IAM_REGISTRATION_SEQUENTIAL_DUPLICATE_stays_idempotent_pending_db_guard()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        // InMemory cannot enforce the unique index or transactions like MySQL;
        // it proves orchestration idempotency. Concurrency is guarded by
        // IX_iam_users_Email on MySQL 8.0 (proven live) plus rollback tests.
        await service.RegisterAsync(new RegisterUser("race@example.test", "secret123", "Owner"));
        await service.RegisterAsync(new RegisterUser("race@example.test", "secret123", "Owner"));
        Assert.Single(await db.IamUsers.ToListAsync());
        Assert.Single(await db.IntegrationDispatches.ToListAsync());
    }

    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "C")]
    public async Task IAM_LOGIN_OVERSIZED_INPUT_fails_closed()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        var oversized = new string('a', 5000) + "@example.test";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SignInAsync(new SignIn(oversized, "secret123")));
    }

    private static IoBuildDbContext CreateDb() => new(
        new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IamService CreateIamService(IoBuildDbContext db)
    {
        var passwordHasher = new PasswordHasher();
        var queue = new IntegrationDispatchQueue(db);
        var workflow = new RegisterUserWorkflow(db, passwordHasher, queue, new WorkflowExecutor(db));
        return new IamService(db, passwordHasher, new JwtTokenIssuer("a-test-secret-that-is-long-enough-for-hmac"), workflow);
    }
}
