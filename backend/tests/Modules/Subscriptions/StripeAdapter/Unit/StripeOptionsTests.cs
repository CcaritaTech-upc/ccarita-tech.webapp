using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;

namespace IoBuild.Modules.Tests.Subscriptions.StripeAdapter.Unit;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "StripeAdapter")]
[Trait("Layer", "Unit")]
[Trait("Dependency", "None")]
public sealed class StripeOptionsTests
{
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public void Stripe_configuration_requires_a_restricted_key_and_dynamic_payment_methods()
    {
        Assert.Throws<InvalidOperationException>(() => StripeIntegrationOptions.Create("sk_not_allowed"));
        var options = StripeIntegrationOptions.Create("rk_test_minimum");
        Assert.True(options.UsesDynamicPaymentMethods);
    }
}
