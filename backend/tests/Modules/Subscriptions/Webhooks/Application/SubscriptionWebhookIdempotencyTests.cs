using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests.Subscriptions.Purchase.Api;

public sealed partial class SubscriptionPurchaseFlowTests
{
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
}
