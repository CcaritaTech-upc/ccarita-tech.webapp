using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.IAM.Infrastructure.Tokens;
using IoBuild.Api.Persistence;
using IoBuild.Api.Workflows;

// Convergent Testing: IAM Tier D — exploratory paranoia as deterministic campaigns.
// Each test injects one unusual condition and asserts fail-closed behavior.
public sealed class IamTierDTests
{
    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_MALFORMED_JSON_is_rejected_without_server_error()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{not-json", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/users", content);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || (int)response.StatusCode == 422);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_OVERSIZED_PAYLOAD_fails_closed_without_user()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        var big = new string('x', 20000);
        using var content = new StringContent($"{{\"email\":\"{big}@example.test\",\"password\":\"secret123\",\"role\":\"Owner\"}}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/users", content);
        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.AUTHORIZED_ACCESS")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_AUTHORIZED_ACCESS_CORRUPT_BEARER_is_rejected()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "!!!not-a-jwt!!!");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "D")]
    public void IAM_LOGIN_HASH_MUTANT_wrong_password_never_verifies()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correct-secret");
        // Kills the mutant that returns true unconditionally.
        Assert.False(hasher.Verify("wrong-secret", hash));
        Assert.False(hasher.Verify("", hash));
        Assert.True(hasher.Verify("correct-secret", hash));
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_NORMALIZATION_IS_IDEMPOTENT_for_representative_inputs()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        foreach (var raw in new[] { "  Mixed@Example.Test ", "MIXED@example.test", "mixed@EXAMPLE.test" })
        {
            await service.RegisterAsync(new RegisterUser(raw, "secret123", "Owner"));
        }
        var users = await db.IamUsers.ToListAsync();
        Assert.Single(users);
        Assert.Equal("mixed@example.test", users[0].Email);
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

    private sealed class TierDApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IoBuildDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IoBuildDbContext>>();
            services.AddDbContext<IoBuildDbContext>(options => options.UseInMemoryDatabase(databaseName));
            var readiness = new IoBuild.Api.Readiness.MigrationReadiness();
            readiness.RecordMigrationSuccess();
            services.AddSingleton(readiness);
            services.RemoveAll<IHostedService>();
        }).ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mqtt:Enabled"] = "false"
        }));
    }
}
