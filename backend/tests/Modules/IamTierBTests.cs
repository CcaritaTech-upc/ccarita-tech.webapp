using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Domain.Model.Entities;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.IAM.Infrastructure.Tokens;
using IoBuild.Api.Persistence;
using IoBuild.Api.Workflows;
using IoBuild.TestKit;

// Convergent Testing: IAM Tier B — plausible degradation with meaningful impact.
public sealed class IamTierBTests
{
    [Fact]
    [Trait("Flow", "IAM.LOGOUT")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "B")]
    public async Task IAM_LOGOUT_REPEATED_second_logout_is_rejected_after_revocation()
    {
        await using var factory = new TierBApiFactory();
        using var client = factory.CreateClient();
        var email = $"logout-{Guid.NewGuid():N}@example.test";
        await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Owner\"}}"));
        var session = await client.PostAsync("/api/v1/sessions", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\"}}"));
        var token = (await session.Content.ReadFromJsonAsync<AuthenticatedUser>())!.Token;

        using var first = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/sessions/current");
        first.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(first)).StatusCode);

        using var second = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/sessions/current");
        second.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        // Auth middleware rejects the revoked token before reaching the handler.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(second)).StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.LOGOUT")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "B")]
    public async Task IAM_LOGOUT_MISSING_TOKEN_is_rejected_without_handler_execution()
    {
        await using var factory = new TierBApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/v1/sessions/current")).StatusCode);
    }

    [Fact]
    [Trait(Traits.Flow, "IAM.LOGIN")]
    [Trait(Traits.Layer, Layers.Application)]
    [Trait(Traits.Risk, Risks.B)]
    public async Task IAM_LOGIN_BLANK_EMAIL_is_rejected_without_user_lookup()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SignInAsync(new SignIn("   ", "secret123")));
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "B")]
    public async Task IAM_REGISTRATION_NORMALIZATION_links_case_and_whitespace_variants()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await service.RegisterAsync(new RegisterUser("  ADA-NORM@Example.Test ", "secret123", "Owner"));
        var session = await service.SignInAsync(new SignIn("ada-norm@example.test", "secret123"));
        Assert.Equal("ada-norm@example.test", session.Email);
        Assert.Single(await db.IamUsers.ToListAsync());
    }

    [Fact]
    [Trait("Flow", "IAM.LOGOUT")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "B")]
    public async Task IAM_LOGOUT_EXPIRED_REVOCATION_is_not_considered_revoked()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await service.RegisterAsync(new RegisterUser("expiry@example.test", "secret123", "Owner"));
        var session = await service.SignInAsync(new SignIn("expiry@example.test", "secret123"));

        // Simulate an expired revocation row (cleanup boundary).
        var hash = IamService.HashToken(session.Token);
        db.RevokedTokens.Add(new RevokedToken { TokenHash = hash, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1), RevokedAt = DateTimeOffset.UtcNow.AddDays(-8) });
        await db.SaveChangesAsync();

        Assert.False(await service.IsRevokedAsync(session.Token));
    }

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");

    private static IoBuildDbContext CreateDb() => new(
        new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IamService CreateIamService(IoBuildDbContext db)
    {
        var passwordHasher = new PasswordHasher();
        var queue = new IntegrationDispatchQueue(db);
        var workflow = new RegisterUserWorkflow(db, passwordHasher, queue, new WorkflowExecutor(db));
        return new IamService(db, passwordHasher, new JwtTokenIssuer("a-test-secret-that-is-long-enough-for-hmac"), workflow);
    }

    private sealed class TierBApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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
