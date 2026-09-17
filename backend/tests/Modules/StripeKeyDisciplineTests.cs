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

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task Invoices_map_hosted_receipt_url_for_the_frontend_button()
    {
        var json = "{\"data\":["
            + "{\"id\":\"in_with_receipt\",\"status\":\"paid\",\"amount_paid\":79900,\"hosted_invoice_url\":\"https://invoice.stripe.com/i/test1\"},"
            + "{\"id\":\"in_fallback_receipt\",\"status\":\"paid\",\"amount_paid\":29900,\"receipt_url\":\"https://pay.stripe.com/receipts/test2\"},"
            + "{\"id\":\"in_without_receipt\",\"status\":\"paid\",\"amount_paid\":100}"
            + "]}";
        var capture = new CaptureHandler(json);
        var configuration = Config(
            ("Stripe:RestrictedApiKey", "rk_test_minimum"),
            ("Stripe:UseSimulatedPayments", "false"),
            ("Stripe:ProviderBaseUrl", "http://127.0.0.1:9/"),
            ("Stripe:BuilderCustomers:1", "cus_test"));
        var provider = new StripeHttpPaymentProvider(new HttpClient(capture), configuration);

        var invoices = (await provider.GetInvoicesAsync(1))!.ToList();

        Assert.Equal(3, invoices.Count);
        Assert.Equal("https://invoice.stripe.com/i/test1", invoices[0].ReceiptUrl);
        Assert.Equal("https://pay.stripe.com/receipts/test2", invoices[1].ReceiptUrl);
        Assert.Null(invoices[2].ReceiptUrl);
    }

    [Fact]
    [Trait("Flow", "SUBSCRIPTIONS.PURCHASE")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task Receipts_come_from_paid_sessions_without_customer_mapping()
    {
        var json = "{\"data\":["
            + "{\"id\":\"cs_paid_1\",\"payment_status\":\"paid\",\"amount_total\":79900,"
            + "\"metadata\":{\"builder_id\":\"1\",\"plan_id\":\"2\"},"
            + "\"payment_intent\":{\"amount_received\":79900,\"latest_charge\":{\"receipt_url\":\"https://pay.stripe.com/receipts/cs1\"}}},"
            + "{\"id\":\"cs_unpaid_1\",\"payment_status\":\"unpaid\",\"amount_total\":79900,"
            + "\"metadata\":{\"builder_id\":\"1\",\"plan_id\":\"2\"},"
            + "\"payment_intent\":{\"amount_received\":0}},"
            + "{\"id\":\"cs_other_builder\",\"payment_status\":\"paid\",\"amount_total\":79900,"
            + "\"metadata\":{\"builder_id\":\"2\",\"plan_id\":\"2\"},"
            + "\"payment_intent\":{\"amount_received\":79900,\"latest_charge\":{\"receipt_url\":\"https://pay.stripe.com/receipts/other\"}}}"
            + "]}";
        var capture = new CaptureHandler(json);
        var configuration = Config(
            ("Stripe:RestrictedApiKey", "rk_test_minimum"),
            ("Stripe:UseSimulatedPayments", "false"),
            ("Stripe:ProviderBaseUrl", "http://127.0.0.1:9/"));
        var provider = new StripeHttpPaymentProvider(new HttpClient(capture), configuration);

        var invoices = (await provider.GetInvoicesAsync(1))!.ToList();

        var mine = Assert.Single(invoices);
        Assert.Equal("cs_paid_1", mine.Id);
        Assert.Equal("paid", mine.Status);
        Assert.Equal(79900, mine.AmountInCents);
        Assert.Equal("https://pay.stripe.com/receipts/cs1", mine.ReceiptUrl);
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
