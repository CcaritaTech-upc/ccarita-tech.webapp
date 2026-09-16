using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using IoBuild.Api.CoreBusiness;
using IoBuild.Api.Persistence;
using IoBuild.Api.Profiles.Application.Internal.CommandServices;
using IoBuild.Api.Profiles.Domain.Model.Aggregates;
using IoBuild.Api.Profiles.Infrastructure.Cloudinary;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;
using IoBuild.Api.Subscriptions.Domain.Model.Entities;
using IoBuild.Api.Subscriptions.Infrastructure.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
namespace IoBuild.Modules.Tests;
public sealed class CoreBusinessWorkflowTests
{
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Invalid_stripe_signature_creates_no_subscription_effects()
    {
        await using var db = CreateDb();
        var processor = new StripeWebhookProcessor(db, "whsec_test");
        var accepted = await processor.ProcessAsync(new StripeWebhookRequest(
            "evt_invalid", "checkout.session.completed", "{\"builderId\":7,\"planId\":3}", "t=1,v1=not-a-signature"));
        Assert.False(accepted);
        Assert.Empty(await db.SubscriptionWebhooks.ToListAsync());
        Assert.Empty(await db.Subscriptions.ToListAsync());
    }
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Duplicate_signed_stripe_event_is_a_no_op()
    {
        await using var db = CreateDb();
        var payload = "{\"builderId\":7,\"planId\":3}";
        var processor = new StripeWebhookProcessor(db, "whsec_test");
        var request = new StripeWebhookRequest("evt_duplicate", "checkout.session.completed", payload, Sign("whsec_test", payload, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        Assert.True(await processor.ProcessAsync(request));
        Assert.True(await processor.ProcessAsync(request));
        Assert.Single(await db.SubscriptionWebhooks.ToListAsync());
        Assert.Single(await db.Subscriptions.ToListAsync());
    }
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Signed_stripe_checkout_payload_reads_legacy_session_metadata()
    {
        await using var db = CreateDb();
        var payload = "{\"data\":{\"object\":{\"metadata\":{\"builder_id\":\"9\",\"plan_id\":\"4\"}}}}";
        var processor = new StripeWebhookProcessor(db, "whsec_test");
        Assert.True(await processor.ProcessAsync(new StripeWebhookRequest("evt_checkout", "checkout.session.completed", payload, Sign("whsec_test", payload, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))));
        var subscription = await db.Subscriptions.SingleAsync();
        Assert.Equal(9, subscription.BuilderId);
        Assert.Equal(4, subscription.PlanId);
    }
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Failed_cloudinary_upload_keeps_the_existing_content_addressed_reference()
    {
        await using var db = CreateDb();
        db.Profiles.Add(new Profile { UserId = 7, Name = "Ada", Username = "ada", PhotoReference = "sha256:existing" });
        await db.SaveChangesAsync();
        var workflow = new ProfilePhotoWorkflow(db, new FailingCloudinaryUploader());
        var updated = await workflow.ReplaceAsync(7, "sha256:existing", "image-bytes");
        Assert.False(updated);
        Assert.Equal("sha256:existing", (await db.Profiles.SingleAsync()).PhotoReference);
    }
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Successful_cloudinary_upload_persists_a_content_addressed_reference()
    {
        await using var db = CreateDb();
        db.Profiles.Add(new Profile { UserId = 8, Name = "Grace", Username = "grace", PhotoReference = "sha256:existing" });
        await db.SaveChangesAsync();
        var workflow = new ProfilePhotoWorkflow(db, new SuccessfulCloudinaryUploader());
        Assert.True(await workflow.ReplaceAsync(8, "sha256:existing", "image-bytes"));

        var profile = await db.Profiles.SingleAsync();
        Assert.Equal("sha256:2c8648d103e3dd7ad87660da0f126a1443b6d21ac1bd3ec000c5e24e2373a90c", profile.PhotoReference);
        Assert.Equal("cloudinary://asset", profile.CloudinaryReference);
        Assert.Equal("cloudinary://asset", profile.PhotoUrl);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task CreateProfile_persists_phone_address_second_email_age_and_photo()
    {
        await using var db = CreateDb();
        var service = new ProfileCommandService(db);
        var created = await service.CreateProfileAsync(
            userId: 99,
            name: "John Builder",
            username: "jbuilder",
            phoneNumber: "+51987654321",
            address: "Av. Primavera 123",
            secondEmail: "alt@iobuild.test",
            age: 32,
            photoUrl: "https://res.cloudinary.com/demo/image/upload/sample.jpg");

        Assert.Equal(99, created.UserId);
        Assert.Equal("John Builder", created.Name);
        Assert.Equal("jbuilder", created.Username);
        Assert.Equal("+51987654321", created.PhoneNumber);
        Assert.Equal("Av. Primavera 123", created.Address);
        Assert.Equal("alt@iobuild.test", created.SecondEmail);
        Assert.Equal(32, created.Age);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/sample.jpg", created.PhotoUrl);

        var fromDb = await db.Profiles.SingleAsync(p => p.UserId == 99);
        Assert.Equal("+51987654321", fromDb.PhoneNumber);
        Assert.Equal("Av. Primavera 123", fromDb.Address);
        Assert.Equal("alt@iobuild.test", fromDb.SecondEmail);
        Assert.Equal(32, fromDb.Age);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public void Stripe_configuration_requires_a_restricted_key_and_dynamic_payment_methods()
    {
        Assert.Throws<InvalidOperationException>(() => StripeIntegrationOptions.Create("sk_not_allowed"));
        var options = StripeIntegrationOptions.Create("rk_test_minimum");
        Assert.True(options.UsesDynamicPaymentMethods);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Stripe_webhook_route_rejects_an_unsigned_callback()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"id\":\"evt_invalid\",\"type\":\"checkout.session.completed\",\"builderId\":7,\"planId\":3}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/webhooks/stripe", content);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Configured_payment_provider_uses_dynamic_methods_without_exposing_the_restricted_key()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"id\":\"cs_123\",\"url\":\"https://checkout.example/cs_123\",\"amount_total\":1200,\"payment_status\":\"unpaid\",\"status\":\"open\",\"metadata\":{\"builder_id\":\"7\",\"plan_id\":\"3\"}}");
        var provider = new StripeHttpPaymentProvider(new HttpClient(handler), Configuration("Stripe:ProviderBaseUrl", "https://payments.example", "Stripe:PlanPrices:3", "price_plan_3", "Stripe:BuilderCustomers:7", "cus_builder_7"));
        var options = StripeIntegrationOptions.Create("rk_test_minimum");

        var session = await provider.CreateCheckoutSessionAsync(new PaymentCheckoutRequest(7, 3, "https://success", "https://cancel"), options);

        Assert.NotNull(session);
        Assert.Equal("cs_123", session!.Id);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/v1/checkout/sessions", handler.PathAndQuery);
        Assert.Equal("application/x-www-form-urlencoded", handler.ContentType);
        Assert.Contains("mode=subscription", handler.Body, StringComparison.Ordinal);
        Assert.Contains("line_items%5B0%5D%5Bprice%5D=price_plan_3", handler.Body, StringComparison.Ordinal);
        Assert.Contains("metadata%5Bbuilder_id%5D=7", handler.Body, StringComparison.Ordinal);
        Assert.Contains("metadata%5Bplan_id%5D=3", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("payment_method_types", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("rk_test_minimum", handler.Body, StringComparison.Ordinal);
        Assert.Equal("Bearer rk_test_minimum", handler.Authorization);
        Assert.Equal("2026-05-27.dahlia", handler.StripeVersion);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Stripe_provider_uses_real_session_and_invoice_retrieval_contracts()
    {
        var handler = new SequenceHandler(
            "{\"id\":\"cs_123\",\"payment_status\":\"paid\",\"status\":\"complete\",\"metadata\":{\"builder_id\":\"7\",\"plan_id\":\"3\"}}",
            "{\"object\":\"list\",\"data\":[{\"id\":\"in_123\",\"status\":\"paid\",\"amount_paid\":1200}]}");
        var provider = new StripeHttpPaymentProvider(new HttpClient(handler), Configuration("Stripe:ProviderBaseUrl", "https://payments.example", "Stripe:RestrictedApiKey", "rk_test_minimum", "Stripe:BuilderCustomers:7", "cus_builder_7"));

        var confirmation = await provider.ConfirmSessionAsync("cs_123");
        var invoices = await provider.GetInvoicesAsync(7);

        Assert.Equal(new PaymentSessionConfirmation("cs_123", "paid", 7, 3), confirmation);
        Assert.Single(invoices!);
        Assert.Equal("in_123", invoices![0].Id);
        Assert.Equal((HttpMethod.Get, "/v1/checkout/sessions/cs_123"), handler.Calls[0]);
        Assert.Equal((HttpMethod.Get, "/v1/invoices?customer=cus_builder_7&limit=100"), handler.Calls[1]);
        Assert.All(handler.StripeVersions, version => Assert.Equal("2026-05-27.dahlia", version));
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Secret_keys_fail_closed_without_confirmation_or_invoice_transport()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var provider = new StripeHttpPaymentProvider(new HttpClient(handler), Configuration("Stripe:ProviderBaseUrl", "https://payments.example", "Stripe:RestrictedApiKey", "sk_not_allowed", "Stripe:BuilderCustomers:7", "cus_builder_7"));

        Assert.Null(await provider.ConfirmSessionAsync("cs_secret"));
        Assert.Null(await provider.GetInvoicesAsync(7));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Cloudinary_adapter_posts_a_signed_multipart_upload_contract()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"secure_url\":\"https://res.cloudinary.com/demo/image/upload/a.png\"}");
        var uploader = new CloudinaryHttpUploader(new HttpClient(handler), Configuration("Cloudinary:UploadBaseUrl", "https://cloud.example", "Cloudinary:CloudName", "demo", "Cloudinary:ApiKey", "key_123", "Cloudinary:ApiSecret", "secret_456"), new FixedTimeProvider(1_700_000_000));

        var reference = await uploader.UploadAsync("image-bytes");

        Assert.Equal("https://res.cloudinary.com/demo/image/upload/a.png", reference);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/v1_1/demo/auto/upload", handler.PathAndQuery);
        Assert.StartsWith("multipart/form-data", handler.ContentType, StringComparison.Ordinal);
        Assert.Contains("name=api_key", handler.Body, StringComparison.Ordinal);
        Assert.Contains("key_123", handler.Body, StringComparison.Ordinal);
        Assert.Contains("name=timestamp", handler.Body, StringComparison.Ordinal);
        Assert.Contains("1700000000", handler.Body, StringComparison.Ordinal);
        Assert.Contains("name=signature", handler.Body, StringComparison.Ordinal);
        Assert.Contains(Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes("timestamp=1700000000secret_456"))).ToLowerInvariant(), handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Payment_provider_fails_closed_when_no_provider_url_is_configured()
    {
        var provider = new StripeHttpPaymentProvider(new HttpClient(new RecordingHandler(HttpStatusCode.OK, "{}")), Configuration());

        Assert.Null(await provider.CreateCheckoutSessionAsync(new PaymentCheckoutRequest(7, 3, "https://success", "https://cancel"), StripeIntegrationOptions.Create("rk_test_minimum")));
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Configured_cloudinary_adapter_returns_no_reference_for_provider_failure()
    {
        var handler = new RecordingHandler(HttpStatusCode.BadGateway, "{}");
        var uploader = new CloudinaryHttpUploader(new HttpClient(handler), Configuration("Cloudinary:UploadBaseUrl", "https://cloud.example", "Cloudinary:CloudName", "demo", "Cloudinary:ApiKey", "key_123", "Cloudinary:ApiSecret", "secret_456"), new FixedTimeProvider(1_700_000_000));

        Assert.Null(await uploader.UploadAsync("image-bytes"));
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("/v1_1/demo/auto/upload", handler.PathAndQuery);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Cloudinary_cas_mismatch_does_not_overwrite_the_profile()
    {
        await using var db = CreateDb();
        db.Profiles.Add(new Profile { UserId = 22, Name = "Ada", Username = "ada", PhotoReference = "sha256:expected" });
        await db.SaveChangesAsync();

        var result = await new ProfilePhotoWorkflow(db, new SuccessfulCloudinaryUploader()).ReplaceAsync(22, "sha256:stale", "image-bytes");

        Assert.False(result);
        Assert.Equal("sha256:expected", (await db.Profiles.SingleAsync()).PhotoReference);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Builder_project_list_and_structure_route_preserve_legacy_statuses()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        var builderToken = Token(1, "builder@example.test", "Builder");
        var otherToken = Token(2, "other@example.test", "Developer");

        var project = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/projects", builderToken, "{\"name\":\"P\",\"description\":\"D\",\"location\":\"L\",\"totalUnits\":1,\"builderId\":1,\"imageUrl\":null}");
        var projectId = JsonDocument.Parse(await project.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
        var builderList = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/projects", builderToken);
        var hiddenList = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/projects", otherToken);
        var invalid = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", builderToken, "{\"floors\":0,\"unitsPerFloor\":1}");
        var forbidden = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", otherToken, "{\"floors\":1,\"unitsPerFloor\":1}");
        var outOfRange = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", builderToken, "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":[2]}");
        var created = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", builderToken, "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":[1]}");
        var duplicate = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", builderToken, "{\"floors\":1,\"unitsPerFloor\":1}");

        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
        Assert.Contains("\"builderId\":1", await builderList.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("[]", await hiddenList.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Payment_routes_use_the_injected_provider_and_publish_dynamic_method_policy()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        using var request = new StringContent("{\"builderId\":7,\"planId\":3,\"successUrl\":\"https://success\",\"cancelUrl\":\"https://cancel\"}", Encoding.UTF8, "application/json");

        var checkout = await client.PostAsync("/api/v1/subscriptions/payments/sessions", request);
        var confirmation = await client.PatchAsync("/api/v1/subscriptions/payments/sessions/cs_fake", null);
        var invoices = await client.GetAsync("/api/v1/subscriptions/payments/invoices?builderId=7");

        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        Assert.Contains("\"usesDynamicPaymentMethods\":true", await checkout.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, invoices.StatusCode);
    }

    private static string Token(int id, string email, string role) => new IoBuild.Api.IAM.Infrastructure.Tokens.JwtTokenIssuer("iobuild-development-secret-must-be-replaced-before-production")
        .Issue(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = id, Email = email, Role = role });

    private static Task<HttpResponseMessage> SendAuthorizedAsync(HttpClient client, HttpMethod method, string path, string token, string? json = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private static IConfiguration Configuration(params string[] values) => new ConfigurationBuilder()
        .AddInMemoryCollection(values.Chunk(2).ToDictionary(pair => pair[0], pair => (string?)pair[1]))
        .Build();

    private sealed class FixedTimeProvider(long unixTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(unixTime);
    }

    private sealed class SequenceHandler(params string[] bodies) : HttpMessageHandler
    {
        private int index;
        public List<(HttpMethod Method, string PathAndQuery)> Calls { get; } = [];
        public List<string?> StripeVersions { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method, request.RequestUri!.PathAndQuery));
            StripeVersions.Add(request.Headers.TryGetValues("Stripe-Version", out var versions) ? versions.Single() : null);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(bodies[index++], Encoding.UTF8, "application/json") });
        }
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? PathAndQuery { get; private set; }
        public string? ContentType { get; private set; }
        public string? Authorization { get; private set; }
        public string? StripeVersion { get; private set; }
        public int CallCount { get; private set; }
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Request = request;
            Method = request.Method;
            PathAndQuery = request.RequestUri?.PathAndQuery;
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Authorization = request.Headers.Authorization?.ToString();
            StripeVersion = request.Headers.TryGetValues("Stripe-Version", out var versions) ? versions.Single() : null;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static string Sign(string secret, string payload, long timestamp)
    {
        var bytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static IoBuildDbContext CreateDb() => new(new DbContextOptionsBuilder<IoBuildDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FailingCloudinaryUploader : ICloudinaryUploader
    {
        public Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class SuccessfulCloudinaryUploader : ICloudinaryUploader
    {
        public Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult<string?>("cloudinary://asset");
    }

    private sealed class CoreBusinessApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            var databaseName = Guid.NewGuid().ToString();
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<IoBuildDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<IoBuildDbContext>>();
                services.AddDbContext<IoBuildDbContext>(options => options.UseInMemoryDatabase(databaseName));
                var readiness = new IoBuild.Api.Readiness.MigrationReadiness();
                readiness.RecordMigrationSuccess();
                services.AddSingleton(readiness);
                services.RemoveAll<IPaymentProvider>();
                services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
                services.RemoveAll<IHostedService>();
            }).ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:RestrictedApiKey"] = "rk_test_minimum",
                ["Mqtt:Enabled"] = "false"
            }));
        }
    }

    private sealed class FakePaymentProvider : IPaymentProvider
    {
        public Task<PaymentCheckoutSession?> CreateCheckoutSessionAsync(PaymentCheckoutRequest request, StripeIntegrationOptions options, CancellationToken cancellationToken = default) => Task.FromResult<PaymentCheckoutSession?>(new("cs_fake", "https://checkout.example/cs_fake", 1200));
        public Task<PaymentSessionConfirmation?> ConfirmSessionAsync(string sessionId, CancellationToken cancellationToken = default) => Task.FromResult<PaymentSessionConfirmation?>(new(sessionId, "confirmed", 7, 3));
        public Task<IReadOnlyList<PaymentInvoice>?> GetInvoicesAsync(int builderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PaymentInvoice>?>([new PaymentInvoice("in_fake", "paid", 1200)]);
    }
}
