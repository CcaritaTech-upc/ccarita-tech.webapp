using System.Net;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Subscriptions.Purchase.Api;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "Purchase")]
[Trait("Layer", "Api")]
[Trait("Dependency", "InMemory")]
public sealed class PaymentRouteTests
{
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Payment_routes_use_the_injected_provider_and_publish_dynamic_method_policy()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();

        var buyer = Token(7, "builder@example.test", "Builder");
        var checkout = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/subscriptions/payments/sessions", buyer, "{\"builderId\":7,\"planId\":3,\"successUrl\":\"https://success\",\"cancelUrl\":\"https://cancel\"}");
        var confirmation = await client.PatchAsync("/api/v1/subscriptions/payments/sessions/cs_fake", null);
        var invoices = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/subscriptions/payments/invoices?builderId=7", buyer);

        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        Assert.Contains("\"usesDynamicPaymentMethods\":true", await checkout.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, invoices.StatusCode);
    }
}
