using System.Net;
using System.Text;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using IoBuild.Api.Subscriptions.Infrastructure.Stripe;
using Microsoft.Extensions.Configuration;

namespace IoBuild.Modules.Tests;

// Convergent Testing G0: Stripe least-privilege discipline at the cheapest layer.
// No HTTP calls leave the process and no database is touched: pure resolver
// decisions plus the outgoing Authorization header through a stub handler.
public sealed class StripeKeyDisciplineTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public void Restricted_key_in_either_slot_resolves()
    {
        Assert.Equal("rk_test_abc", StripeRestrictedKeyResolver.Resolve(Config(("Stripe:RestrictedApiKey", "rk_test_abc"))));
        Assert.Equal("rk_test_abc", StripeRestrictedKeyResolver.Resolve(Config(("Stripe:SecretKey", "rk_test_abc"))));
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public void Secret_key_fails_closed_without_simulation()
    {
        var configuration = Config(
            ("Stripe:RestrictedApiKey", ""),
            ("Stripe:SecretKey", "sk_live_trap"),
            ("Stripe:UseSimulatedPayments", "false"));
        Assert.Null(StripeRestrictedKeyResolver.Resolve(configuration));
        Assert.Throws<InvalidOperationException>(() => StripeIntegrationOptions.Create("sk_live_trap"));
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public void Empty_config_falls_back_to_local_simulated_key()
    {
        var configuration = Config(("Stripe:UseSimulatedPayments", "true"));
        Assert.Equal("rk_test_local", StripeRestrictedKeyResolver.Resolve(configuration));
        Assert.NotNull(StripeIntegrationOptions.Create("rk_test_minimum"));
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task Outgoing_calls_carry_the_restricted_key_even_with_secret_configured()
    {
        var capture = new CaptureHandler("{\"id\":\"cs_test\",\"url\":\"https://checkout.test/cs_test\",\"amount_total\":1200}");
        var configuration = Config(
            ("Stripe:RestrictedApiKey", "rk_test_minimum"),
            ("Stripe:SecretKey", "sk_test_trap"),
            ("Stripe:UseSimulatedPayments", "false"),
            ("Stripe:ProviderBaseUrl", "http://127.0.0.1:9/"),
            ("Stripe:PlanPrices:3", "price_runtime"));
        var provider = new StripeHttpPaymentProvider(new HttpClient(capture), configuration);

        var session = await provider.CreateCheckoutSessionAsync(
            new PaymentCheckoutRequest(1, 3, "https://success.example", "https://cancel.example"),
            StripeIntegrationOptions.Create("rk_test_minimum"));

        Assert.NotNull(session);
        Assert.Equal("rk_test_minimum", capture.Authorization);
        Assert.DoesNotContain("sk_test_trap", capture.Authorization ?? string.Empty);
        Assert.Equal("2026-05-27.dahlia", capture.StripeVersion);
    }

    private sealed class CaptureHandler(string json) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? StripeVersion { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.Parameter;
            StripeVersion = request.Headers.TryGetValues("Stripe-Version", out var values) ? string.Join(",", values) : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
