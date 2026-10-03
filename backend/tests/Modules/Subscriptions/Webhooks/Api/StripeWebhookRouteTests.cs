using System.Text;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Subscriptions.Webhooks.Api;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "Webhooks")]
[Trait("Layer", "Api")]
[Trait("Dependency", "InMemory")]
public sealed class StripeWebhookRouteTests
{
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
}
