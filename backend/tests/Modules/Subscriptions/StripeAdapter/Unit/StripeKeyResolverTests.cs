using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;

namespace IoBuild.Modules.Tests.Subscriptions.StripeAdapter.Contract;

public sealed partial class StripeKeyDisciplineTests
{
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
}
