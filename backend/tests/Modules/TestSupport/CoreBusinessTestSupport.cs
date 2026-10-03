using System.Net;
using System.Security.Cryptography;
using System.Text;
using IoBuild.Api.CoreBusiness;
using IoBuild.Api.Persistence;
using IoBuild.Api.Profiles.Infrastructure.Cloudinary;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using IoBuild.Api.Subscriptions.Infrastructure.Stripe;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IoBuild.Modules.Tests.TestSupport;

// Collaborators retained from the legacy mixed suite; no tests live here.
internal static class CoreBusinessTestSupport
{
    internal static string Token(int id, string email, string role) => new IoBuild.Api.IAM.Infrastructure.Tokens.JwtTokenIssuer("iobuild-development-secret-must-be-replaced-before-production")
        .Issue(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = id, Email = email, Role = role });

    internal static Task<HttpResponseMessage> SendAuthorizedAsync(HttpClient client, HttpMethod method, string path, string token, string? json = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    internal static IConfiguration Configuration(params string[] values) => new ConfigurationBuilder()
        .AddInMemoryCollection(values.Chunk(2).ToDictionary(pair => pair[0], pair => (string?)pair[1]))
        .Build();

    internal sealed class FixedTimeProvider(long unixTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(unixTime);
    }

    internal sealed class SequenceHandler(params string[] bodies) : HttpMessageHandler
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

    internal sealed class RecordingHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
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

    internal static string Sign(string secret, string payload, long timestamp)
    {
        var bytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    internal static IoBuildDbContext CreateDb() => new(new DbContextOptionsBuilder<IoBuildDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    internal sealed class FailingCloudinaryUploader : ICloudinaryUploader
    {
        public Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    internal sealed class SuccessfulCloudinaryUploader : ICloudinaryUploader
    {
        public Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult<string?>("cloudinary://asset");
    }

    internal sealed class CoreBusinessApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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
