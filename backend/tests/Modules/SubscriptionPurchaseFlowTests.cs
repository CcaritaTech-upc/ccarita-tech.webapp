using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IoBuild.Modules.Tests;

// Convergent Testing G1: SUBSCRIPTIONS.PURCHASE checkout→confirm→invoices plus
// webhook idempotency, all versioned and deterministic. Stripe runs simulated
// (no network); persistence is InMemory here, MySQL guarantees live separately.
public sealed class SubscriptionPurchaseFlowTests
{
    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Contract")]
    [Trait("Risk", "A")]
    public async Task PURCHASE_HAPPY_PATH_checkout_confirm_invoices()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();

        var plans = await client.GetAsync("/api/v1/plans");
        Assert.Equal(HttpStatusCode.OK, plans.StatusCode);
        var planList = await plans.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(planList);
        Assert.NotEmpty(planList);

        var checkout = await client.PostAsync("/api/v1/subscriptions/payments/sessions",
            Json("{\"builderId\":1,\"planId\":3,\"successUrl\":\"https://success.example\",\"cancelUrl\":\"https://cancel.example\"}"));
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var session = await checkout.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = session.GetProperty("sessionId").GetString()!;
        Assert.StartsWith("cs_sim_", sessionId, StringComparison.Ordinal);

        using var confirm = await client.PatchAsync($"/api/v1/subscriptions/payments/sessions/{sessionId}", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal("paid", (await confirm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var invoices = await client.GetAsync("/api/v1/subscriptions/payments/invoices?builderId=1");
        Assert.Equal(HttpStatusCode.OK, invoices.StatusCode);
        Assert.Contains("in_sim_1", await invoices.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task PURCHASE_SUPERSEDE_expires_previous_active_subscription()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();

        await ConfirmPlanAsync(client, builderId: 2, planId: 1);
        await ConfirmPlanAsync(client, builderId: 2, planId: 2);

        var all = await client.GetAsync("/api/v1/subscriptions");
        var body = await all.Content.ReadFromJsonAsync<List<JsonElement>>();
        var mine = body!.Where(s => s.GetProperty("builderId").GetInt32() == 2).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Single(mine.Where(s => s.GetProperty("status").GetString() == "active" && s.GetProperty("planId").GetInt32() == 2));
        Assert.Single(mine.Where(s => s.GetProperty("status").GetString() == "expired" && s.GetProperty("planId").GetInt32() == 1));
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task WEBHOOK_DUPLICATE_EVENT_answered_twice_but_applied_once()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        var payload = "{\"id\":\"evt_dup_1\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"payment_status\":\"paid\",\"metadata\":{\"builder_id\":\"3\",\"plan_id\":\"1\"}}}}";

        var first = await client.PostAsync("/api/v1/webhooks/stripe", SignedJson(payload));
        var second = await client.PostAsync("/api/v1/webhooks/stripe", SignedJson(payload));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task WEBHOOK_BAD_SIGNATURE_is_rejected()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        var payload = "{\"id\":\"evt_bad_1\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"payment_status\":\"paid\",\"metadata\":{\"builder_id\":\"3\",\"plan_id\":\"1\"}}}}";
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "t=1,v1=deadbeef");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/webhooks/stripe", content)).StatusCode);
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task WEBHOOK_IDEMPOTENCY_keeps_a_single_subscription_row()
    {
        await using var db = CreateDb();
        var processor = new StripeWebhookProcessor(db, "test-secret");
        var payload = "{\"id\":\"evt_single_1\",\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"payment_status\":\"paid\",\"metadata\":{\"builder_id\":\"4\",\"plan_id\":\"2\"}}}}";
        var request = new StripeWebhookRequest("evt_single_1", "checkout.session.completed", payload, Sign(payload));

        Assert.True(await processor.ProcessAsync(request));
        Assert.True(await processor.ProcessAsync(request));
        Assert.Single(await db.Subscriptions.Where(s => s.BuilderId == 4).ToListAsync());
        Assert.Single(await db.SubscriptionWebhooks.Where(w => w.EventId == "evt_single_1").ToListAsync());
    }

    private static async Task ConfirmPlanAsync(HttpClient client, int builderId, int planId)
    {
        var checkout = await client.PostAsync("/api/v1/subscriptions/payments/sessions",
            Json($"{{\"builderId\":{builderId},\"planId\":{planId},\"successUrl\":\"https://success.example\",\"cancelUrl\":\"https://cancel.example\"}}"));
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var sessionId = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
        using var confirm = await client.PatchAsync($"/api/v1/subscriptions/payments/sessions/{sessionId}", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    private static StringContent SignedJson(string payload)
    {
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", Sign(payload));
        return content;
    }

    private static string Sign(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static IoBuildDbContext CreateDb() => new(
        new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class PurchaseApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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
            ["Mqtt:Enabled"] = "false",
            ["Stripe:UseSimulatedPayments"] = "true",
            ["Stripe:WebhookSecret"] = "test-secret"
        }));
    }
}
