using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using IoBuild.Api.Persistence;

namespace IoBuild.Modules.Tests;

// Convergent Testing: IAM Tier A — critical what-if scenarios at API/application boundary.
public sealed class IamTierATests
{
    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_LOGIN_INVALID_CREDENTIALS_rejects_wrong_password_without_token()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var email = $"tier-a-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Owner\"}}"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsync("/api/v1/sessions", Json($"{{\"email\":\"{email}\",\"password\":\"wrong-password\"}}"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_LOGIN_UNKNOWN_USER_is_rejected()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var login = await client.PostAsync("/api/v1/sessions", Json("{\"email\":\"unknown-tier-a@example.test\",\"password\":\"secret123\"}"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_REGISTRATION_DUPLICATE_HTTP_preserves_characterized_contract()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.test";
        var first = await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Owner\"}}"));
        var second = await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Owner\"}}"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        // Current contract always returns 201; service layer keeps a single user + dispatch row.
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.AUTHORIZED_ACCESS")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_AUTHORIZED_ACCESS_TAMPERED_TOKEN_is_rejected()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var email = $"tamper-{Guid.NewGuid():N}@example.test";
        await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Owner\"}}"));
        var session = await client.PostAsync("/api/v1/sessions", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\"}}"));
        var token = (await session.Content.ReadFromJsonAsync<IoBuild.Api.IAM.Domain.Model.Commands.AuthenticatedUser>())!.Token;

        using var tampered = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        tampered.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token + "tampered");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(tampered)).StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.AUTHORIZED_ACCESS")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_AUTHORIZED_ACCESS_MISSING_TOKEN_is_rejected()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users")).StatusCode);
    }

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");

    private sealed class TierAApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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
