using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using Microsoft.EntityFrameworkCore;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Subscriptions.Webhooks.Application;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "Webhooks")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class StripeWebhookWorkflowTests
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
}
