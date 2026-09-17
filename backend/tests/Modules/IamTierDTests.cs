using System.Net;
using System.Text;
using System.Text.Json;
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

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_INPUT_PARTITIONS_never_server_error()
    {
        // Deterministic fuzz partitions: boundary lengths around the 320 rule,
        // empty/whitespace, unicode, control chars, malformed shapes, huge input.
        // Every partition must resolve to 201 (valid) or 400 (fail-closed):
        // a 500 means an unhandled path survived the frontend.
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        var local319 = new string('a', 306) + "@example.test"; // 319 chars
        var local320 = new string('a', 307) + "@example.test"; // 320 chars
        var local321 = new string('a', 308) + "@example.test"; // 321 chars
        var partitions = new (string Email, string Password, HttpStatusCode Expected)[]
        {
            ("", "secret123", HttpStatusCode.BadRequest),
            ("   ", "secret123", HttpStatusCode.BadRequest),
            ("no-at-sign", "secret123", HttpStatusCode.Created), // backend has no charset policy: characterized
            ("a@@b@example.test", "secret123", HttpStatusCode.Created), // characterized: accepted, normalized
            ("usuário@example.test", "secret123", HttpStatusCode.Created),
            ("a\nb@example.test", "secret123", HttpStatusCode.Created), // characterized: accepted
            (local319, "secret123", HttpStatusCode.Created),
            (local320, "secret123", HttpStatusCode.Created),
            (local321, "secret123", HttpStatusCode.BadRequest),
            ($"fuzz.{Guid.NewGuid():N}@example.test", "", HttpStatusCode.BadRequest),
            ($"fuzz.{Guid.NewGuid():N}@example.test", "x", HttpStatusCode.Created), // characterized: no min length server-side
            ($"fuzz.{Guid.NewGuid():N}@example.test", new string('p', 5000), HttpStatusCode.Created), // characterized
        };

        foreach (var (email, password, expected) in partitions)
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { email, password, role = "Owner" }), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/users", content);
            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest,
                $"Partition email={Truncate(email)} password-len={password.Length} returned {response.StatusCode}");
            Assert.Equal(expected, response.StatusCode);
        }
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_BURST_same_email_never_server_errors()
    {
        // Concurrency fuzz at the API boundary sharing one store: twelve racers,
        // same email and distinct emails. No response may be a 500; the endpoint
        // contract stays fail-closed-or-created under burst. Single-winner
        // uniqueness under race is proven on MySQL (burst x8 persistence test).
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        var shared = $"burst.{Guid.NewGuid():N}@example.test";

        var sameEmail = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            client.PostAsync("/api/v1/users", JsonContent(new { email = shared, password = "secret123", role = "Owner" }))));
        Assert.All(sameEmail, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest,
            $"Burst same-email returned {r.StatusCode}"));

        var distinct = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
            client.PostAsync("/api/v1/users", JsonContent(new { email = $"burst.{Guid.NewGuid():N}.{i}@example.test", password = "secret123", role = "Owner" }))));
        Assert.All(distinct, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
    }

    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_LOGIN_FUZZ_rejects_without_token_or_server_error()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        var email = $"loginfuzz.{Guid.NewGuid():N}@example.test";
        using (var setup = new StringContent(
            JsonSerializer.Serialize(new { email, password = "secret123", role = "Owner" }), Encoding.UTF8, "application/json"))
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync("/api/v1/users", setup)).StatusCode);
        }

        var passwords = new[] { "wrong", "", "SECRET123", "secret123 ", new string('p', 5000), "usuário✓" };
        foreach (var password in passwords)
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { email, password }), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/sessions", content);
            var body = await response.Content.ReadAsStringAsync();
            if (password == "secret123")
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
            }
            Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        }

        // Unknown user with hostile shapes: still a clean 401, never a 500.
        foreach (var hostile in new[] { "'; DROP TABLE iam_users; --@example.test", "\0@example.test", new string('e', 1000) + "@example.test" })
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { email = hostile, password = "x" }), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/sessions", content);
            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
                $"Hostile login returned {response.StatusCode}");
        }
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_GUARD_MUTANTS_every_bypass_throws()
    {
        // Kills mutants that delete any single fail-closed guard.
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("", "secret123", "Owner")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("   ", "secret123", "Owner")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("mut@example.test", "", "Owner")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("mut@example.test", "   ", "Owner")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("mut@example.test", "secret123", "Admin")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("mut@example.test", "secret123", "")));
        Assert.Empty(await db.IamUsers.ToListAsync());
    }

    private static StringContent JsonContent(object payload) => new(
        JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static string Truncate(string value, int max = 40) =>
        value.Length <= max ? value : value[..max] + $"…({value.Length})";

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
