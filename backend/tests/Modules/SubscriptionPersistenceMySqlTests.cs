using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.TestKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IoBuild.Modules.Tests;

// Convergent Testing G1: SUBSCRIPTIONS.PURCHASE persistence guarantees on the
// production engine. The full flow runs over HTTP against MySQL with simulated
// Stripe (no network). Opt-in: skips with success without
// IOBUILD_TEST_MYSQL_CONNECTION. Uses a high, dedicated builder id and deletes
// only the subscriptions it created.
public sealed class SubscriptionPersistenceMySqlTests
{
    private const int ProbeBuilderId = 91827;

    [Fact]
    [Trait("Category", "Subscriptions")]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Activation_and_supersede_on_mysql()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new MySqlPurchaseApiFactory(connectionString);
        using var client = factory.CreateClient();
        try
        {
            await using (var admin = MySqlFixture.CreateIsolatedContext(connectionString))
            {
                Assert.True(await admin.Plans.AnyAsync(p => p.Id == 1), "Seed plan 1 missing in test database.");
                Assert.True(await admin.Plans.AnyAsync(p => p.Id == 2), "Seed plan 2 missing in test database.");
            }

            await ConfirmPlanAsync(client, ProbeBuilderId, planId: 1);
            await ConfirmPlanAsync(client, ProbeBuilderId, planId: 2);

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            var mine = await reader.Subscriptions.Where(s => s.BuilderId == ProbeBuilderId).ToListAsync();
            Assert.Equal(2, mine.Count);
            Assert.Single(mine.Where(s => s.Status == "active" && s.PlanId == 2));
            var expired = Assert.Single(mine.Where(s => s.Status == "expired" && s.PlanId == 1));
            Assert.NotNull(expired.EndDate);
        }
        finally
        {
            await using var cleaner = MySqlFixture.CreateIsolatedContext(connectionString);
            var rows = await cleaner.Subscriptions.Where(s => s.BuilderId == ProbeBuilderId).ToListAsync();
            if (rows.Count > 0)
            {
                cleaner.Subscriptions.RemoveRange(rows);
                await cleaner.SaveChangesAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", "Subscriptions")]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Concurrent_confirms_of_one_session_leave_a_single_active()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        const int builderId = 91828;
        await using var factory = new MySqlPurchaseApiFactory(connectionString);
        using var client = factory.CreateClient();
        await using (var admin = MySqlFixture.CreateIsolatedContext(connectionString))
        {
            Assert.True(await admin.Plans.AnyAsync(p => p.Id == 1), "Seed plan 1 missing in test database.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/payments/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(builderId));
        request.Content = new StringContent(
            $"{{\"builderId\":{builderId},\"planId\":1,\"successUrl\":\"https://success.example\",\"cancelUrl\":\"https://cancel.example\"}}",
            Encoding.UTF8, "application/json");
        using var checkout = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var sessionId = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;

        try
        {
            // Six racers on one paid session: winners confirm (200), losers hit
            // the single-active arbiter (409). Never a 500, never two actives.
            var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
                client.PatchAsync($"/api/v1/subscriptions/payments/sessions/{sessionId}", null)));
            Assert.All(results, r => Assert.True(
                r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,
                $"Concurrent confirm returned {r.StatusCode}"));
            foreach (var r in results) r.Dispose();

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            var mine = await reader.Subscriptions.Where(s => s.BuilderId == builderId).ToListAsync();
            Assert.Single(mine.Where(s => s.Status == "active"));
        }
        finally
        {
            await using var cleaner = MySqlFixture.CreateIsolatedContext(connectionString);
            var rows = await cleaner.Subscriptions.Where(s => s.BuilderId == builderId).ToListAsync();
            if (rows.Count > 0)
            {
                cleaner.Subscriptions.RemoveRange(rows);
                await cleaner.SaveChangesAsync();
            }
        }
    }

    private static string Token(int id) => new IoBuild.Api.IAM.Infrastructure.Tokens.JwtTokenIssuer("iobuild-development-secret-must-be-replaced-before-production")
        .Issue(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = id, Email = $"probe{id}@example.test", Role = "Builder" });

    private static async Task ConfirmPlanAsync(HttpClient client, int builderId, int planId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/payments/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(builderId));
        request.Content = new StringContent(
            $"{{\"builderId\":{builderId},\"planId\":{planId},\"successUrl\":\"https://success.example\",\"cancelUrl\":\"https://cancel.example\"}}",
            Encoding.UTF8, "application/json");
        using var checkout = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var sessionId = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
        using var confirm = await client.PatchAsync($"/api/v1/subscriptions/payments/sessions/{sessionId}", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    private sealed class MySqlPurchaseApiFactory(string connectionString) : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IoBuildDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IoBuildDbContext>>();
            services.AddDbContext<IoBuildDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
            var readiness = new IoBuild.Api.Readiness.MigrationReadiness();
            readiness.RecordMigrationSuccess();
            services.AddSingleton(readiness);
            services.RemoveAll<IHostedService>();
        }).ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mqtt:Enabled"] = "false",
            ["Stripe:UseSimulatedPayments"] = "true",
            ["Stripe:WebhookSecret"] = "test-secret"
        }));
    }
}
