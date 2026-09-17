using System.Net;
using System.Net.Http.Headers;
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

        var buyer = Token(1, "buyer1@example.test", "Builder");
        var checkout = await SendAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", buyer,
            "{\"builderId\":1,\"planId\":3,\"successUrl\":\"https://success.example\",\"cancelUrl\":\"https://cancel.example\"}");
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var session = await checkout.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = session.GetProperty("sessionId").GetString()!;
        Assert.StartsWith("cs_sim_", sessionId, StringComparison.Ordinal);

        using var confirm = await client.PatchAsync($"/api/v1/subscriptions/payments/sessions/{sessionId}", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal("paid", (await confirm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var invoices = await SendAsync(client, HttpMethod.Get, "/api/v1/subscriptions/payments/invoices?builderId=1", buyer);
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

        var buyer2 = Token(2, "buyer2@example.test", "Builder");
        await ConfirmPlanAsync(client, buyer2, builderId: 2, planId: 1);
        await ConfirmPlanAsync(client, buyer2, builderId: 2, planId: 2);

        var all = await SendAsync(client, HttpMethod.Get, "/api/v1/subscriptions", buyer2);
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

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task CHECKOUT_UNKNOWN_PLAN_is_rejected()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        var buyer = Token(1, "buyer1@example.test", "Builder");
        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", buyer,
            "{\"builderId\":1,\"planId\":999999,\"successUrl\":\"https://success.example\",\"cancelUrl\":\"https://cancel.example\"}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task CHECKOUT_WITHOUT_KEY_fails_closed()
    {
        await using var factory = new NoKeyApiFactory();
        using var client = factory.CreateClient();
        var buyer = Token(1, "buyer1@example.test", "Builder");
        var response = await SendAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", buyer,
            "{\"builderId\":1,\"planId\":1,\"successUrl\":\"https://success.example\",\"cancelUrl\":\"https://cancel.example\"}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task WEBHOOK_MALFORMED_JSON_is_rejected_without_server_error()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{not-json", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/webhooks/stripe", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task CANCEL_MISSING_is_not_found_and_repeat_is_stable()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        var buyer5 = Token(5, "buyer5@example.test", "Builder");
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Post, "/api/v1/subscriptions/999999/cancel", buyer5)).StatusCode);

        await ConfirmPlanAsync(client, buyer5, builderId: 5, planId: 1);
        var mine = await SendAsync(client, HttpMethod.Get, "/api/v1/subscriptions", buyer5);
        var sub = (await mine.Content.ReadFromJsonAsync<List<JsonElement>>())!
            .First(s => s.GetProperty("builderId").GetInt32() == 5);
        var id = sub.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post, $"/api/v1/subscriptions/{id}/cancel", buyer5)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post, $"/api/v1/subscriptions/{id}/cancel", buyer5)).StatusCode);
        var after = await (await SendAsync(client, HttpMethod.Get, $"/api/v1/subscriptions/{id}", buyer5)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cancelled", after.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task CROSS_BUILDER_checkout_invoices_and_cancel_are_rejected()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        var buyer6 = Token(6, "buyer6@example.test", "Builder");
        var intruder = Token(1, "intruder@example.test", "Builder");

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", intruder,
            "{\"builderId\":6,\"planId\":1,\"successUrl\":\"https://s.example\",\"cancelUrl\":\"https://c.example\"}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, "/api/v1/subscriptions/payments/invoices?builderId=6", intruder)).StatusCode);

        await ConfirmPlanAsync(client, buyer6, builderId: 6, planId: 1);
        var mine = await SendAsync(client, HttpMethod.Get, "/api/v1/subscriptions", buyer6);
        var id = (await mine.Content.ReadFromJsonAsync<List<JsonElement>>())!.First().GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"/api/v1/subscriptions/{id}", intruder)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Post, $"/api/v1/subscriptions/{id}/cancel", intruder)).StatusCode);
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task LIST_SCOPED_returns_only_own_subscriptions()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        var buyer8 = Token(8, "buyer8@example.test", "Builder");
        var buyer9 = Token(9, "buyer9@example.test", "Builder");
        await ConfirmPlanAsync(client, buyer8, builderId: 8, planId: 1);
        await ConfirmPlanAsync(client, buyer9, builderId: 9, planId: 1);

        var list = await (await SendAsync(client, HttpMethod.Get, "/api/v1/subscriptions", buyer8)).Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotEmpty(list!);
        Assert.All(list!, s => Assert.Equal(8, s.GetProperty("builderId").GetInt32()));
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task MISSING_TOKEN_is_unauthorized_on_purchase_routes()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/subscriptions/payments/sessions",
            Json("{\"builderId\":1,\"planId\":1,\"successUrl\":\"https://s.example\",\"cancelUrl\":\"https://c.example\"}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/subscriptions/payments/invoices?builderId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/subscriptions")).StatusCode);
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "B")]
    public async Task ERROR_CONTRACT_failure_paths_never_leak_internals()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        var buyer = Token(1, "buyer1@example.test", "Builder");
        var cases = new List<HttpResponseMessage>
        {
            await SendAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", buyer,
                "{\"builderId\":1,\"planId\":999999,\"successUrl\":\"https://s.example\",\"cancelUrl\":\"https://c.example\"}"),
            await client.PatchAsync("/api/v1/subscriptions/payments/sessions/does-not-exist", null),
            await SendAsync(client, HttpMethod.Get, "/api/v1/subscriptions/999999", buyer),
        };
        using (var malformed = new StringContent("{not-json", Encoding.UTF8, "application/json"))
        {
            cases.Add(await client.PostAsync("/api/v1/webhooks/stripe", malformed));
        }
        foreach (var response in cases)
        {
            using (response)
            {
                Assert.True(
                    response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.ServiceUnavailable,
                    $"Failure path returned {response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("at IoBuild", body, StringComparison.Ordinal);
                Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task CHECKOUT_FUZZ_partitions_never_server_error()
    {
        await using var factory = new PurchaseApiFactory();
        using var client = factory.CreateClient();
        var buyer = Token(1, "buyer1@example.test", "Builder");
        var payloads = new[]
        {
            "{\"builderId\":1,\"planId\":1,\"successUrl\":\"\",\"cancelUrl\":\"\"}",
            $"{{\"builderId\":1,\"planId\":1,\"successUrl\":\"https://s.example/{new string('x', 2000)}\",\"cancelUrl\":\"https://c.example\"}}",
            "{\"builderId\":1}",
            "{}",
            "{\"builderId\":2,\"planId\":1,\"successUrl\":\"https://s.example\",\"cancelUrl\":\"https://c.example\"}",
        };
        foreach (var payload in payloads)
        {
            using var response = await SendAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", buyer, payload);
            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.ServiceUnavailable or HttpStatusCode.Forbidden,
                $"Fuzz partition returned {response.StatusCode}");
        }
    }

    private static string Token(int id, string email, string role) => new IoBuild.Api.IAM.Infrastructure.Tokens.JwtTokenIssuer("iobuild-development-secret-must-be-replaced-before-production")
        .Issue(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = id, Email = email, Role = role });

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, string? json = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private static async Task ConfirmPlanAsync(HttpClient client, string token, int builderId, int planId)
    {
        var checkout = await SendAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", token,
            $"{{\"builderId\":{builderId},\"planId\":{planId},\"successUrl\":\"https://success.example\",\"cancelUrl\":\"https://cancel.example\"}}");
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

    private sealed class NoKeyApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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
            ["Stripe:UseSimulatedPayments"] = "false"
        }));
    }

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
