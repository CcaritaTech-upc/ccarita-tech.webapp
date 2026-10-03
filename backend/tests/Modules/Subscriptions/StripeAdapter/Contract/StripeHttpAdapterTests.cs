using System.Net;
using IoBuild.Api.CoreBusiness;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using IoBuild.Api.Subscriptions.Infrastructure.Stripe;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Subscriptions.StripeAdapter.Contract;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "StripeAdapter")]
[Trait("Layer", "Contract")]
[Trait("Dependency", "FakeHttp")]
public sealed class StripeHttpAdapterTests
{
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
            "{\"object\":\"list\",\"data\":[]}",
            "{\"object\":\"list\",\"data\":[{\"id\":\"in_123\",\"status\":\"paid\",\"amount_paid\":1200}]}");
        var provider = new StripeHttpPaymentProvider(new HttpClient(handler), Configuration("Stripe:ProviderBaseUrl", "https://payments.example", "Stripe:RestrictedApiKey", "rk_test_minimum", "Stripe:BuilderCustomers:7", "cus_builder_7"));

        var confirmation = await provider.ConfirmSessionAsync("cs_123");
        var invoices = await provider.GetInvoicesAsync(7);

        Assert.Equal(new PaymentSessionConfirmation("cs_123", "paid", 7, 3), confirmation);
        Assert.Single(invoices!);
        Assert.Equal("in_123", invoices![0].Id);
        Assert.Equal((HttpMethod.Get, "/v1/checkout/sessions/cs_123"), handler.Calls[0]);
        Assert.Equal((HttpMethod.Get, "/v1/checkout/sessions?limit=100&expand[]=data.payment_intent.latest_charge"), handler.Calls[1]);
        Assert.Equal((HttpMethod.Get, "/v1/invoices?customer=cus_builder_7&limit=100"), handler.Calls[2]);
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
    public async Task Payment_provider_fails_closed_when_no_provider_url_is_configured()
    {
        var provider = new StripeHttpPaymentProvider(new HttpClient(new RecordingHandler(HttpStatusCode.OK, "{}")), Configuration());

        Assert.Null(await provider.CreateCheckoutSessionAsync(new PaymentCheckoutRequest(7, 3, "https://success", "https://cancel"), StripeIntegrationOptions.Create("rk_test_minimum")));
    }
}
