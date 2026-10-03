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
[Trait("Context", "IAM")]
public sealed partial class IamTierDTests
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
        using var content = new StringContent($"{{\"email\":\"{big}@example.test\",\"password\":\"secret123\",\"role\":\"Builder\"}}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/users", content);
        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_INPUT_PARTITIONS_never_server_error()
    {
        // Deterministic fuzz partitions: email syntax and 320-char boundary,
        // empty/whitespace, unicode, control chars, malformed shapes, and password length.
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
            ("no-at-sign", "secret123", HttpStatusCode.BadRequest),
            ("a@@b@example.test", "secret123", HttpStatusCode.BadRequest),
            ("usuário@example.test", "secret123", HttpStatusCode.BadRequest),
            ("a\nb@example.test", "secret123", HttpStatusCode.BadRequest),
            (local319, "secret123", HttpStatusCode.Created),
            (local320, "secret123", HttpStatusCode.Created),
            (local321, "secret123", HttpStatusCode.BadRequest),
            ($"fuzz.{Guid.NewGuid():N}@example.test", "", HttpStatusCode.BadRequest),
            ($"fuzz.{Guid.NewGuid():N}@example.test", "1234567", HttpStatusCode.BadRequest),
            ($"fuzz.{Guid.NewGuid():N}@example.test", "12345678", HttpStatusCode.Created),
            ($"fuzz.{Guid.NewGuid():N}@example.test", new string('p', 5000), HttpStatusCode.Created), // characterized
        };

        foreach (var (email, password, expected) in partitions)
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { email, password, role = "Builder" }), Encoding.UTF8, "application/json");
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
            client.PostAsync("/api/v1/users", JsonContent(new { email = shared, password = "secret123", role = "Builder" }))));
        Assert.All(sameEmail, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"Burst same-email returned {r.StatusCode}"));

        var distinct = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
            client.PostAsync("/api/v1/users", JsonContent(new { email = $"burst.{Guid.NewGuid():N}.{i}@example.test", password = "secret123", role = "Builder" }))));
        Assert.All(distinct, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_BURST_50_distinct_registrations_all_succeed_without_server_error()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        var stamp = Guid.NewGuid().ToString("N");
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(i =>
            client.PostAsync("/api/v1/users", JsonContent(new { email = $"burst{stamp}{i}@example.test", password = "secret123", role = "Builder" }))));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        foreach (var r in results) r.Dispose();

        using var probe = await client.PostAsync("/api/v1/sessions",
            JsonContent(new { email = $"burst{stamp}7@example.test", password = "secret123" }));
        Assert.Equal(HttpStatusCode.Created, probe.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_MEGABYTE_PAYLOAD_fails_closed_without_server_error()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        var bigLocal = new string('z', 1_000_000);
        using var response = await client.PostAsync("/api/v1/users",
            JsonContent(new { email = bigLocal + "@example.test", password = "secret123", role = "Builder" }));
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge,
            $"Megabyte payload returned {response.StatusCode}");
    }

    private sealed class DeadDbApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IoBuildDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IoBuildDbContext>>();
            services.AddDbContext<IoBuildDbContext>(options => options.UseMySql(
                "Server=127.0.0.1;Port=1;Database=iobuild_dead;User=root;Password=iobuild;Connection Timeout=2",
                ServerVersion.Parse("8.0.46-mysql")));
            var readiness = new IoBuild.Api.Readiness.MigrationReadiness();
            readiness.RecordMigrationSuccess();
            services.AddSingleton(readiness);
            services.RemoveAll<IHostedService>();
        }).ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mqtt:Enabled"] = "false"
        }));
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
