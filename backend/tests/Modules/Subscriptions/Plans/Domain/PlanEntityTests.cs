using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;

namespace IoBuild.Modules.Tests.Subscriptions.Plans.Domain;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "Plans")]
[Trait("Layer", "Domain")]
[Trait("Dependency", "None")]
public sealed class PlanEntityTests
{
    [Fact]
    [Trait("Category", "BDD")]
    public void Given_a_plan_name_description_and_price_when_created_then_monthly_interval_and_empty_features_are_defaulted()
    {
        var plan = new Plan("Starter", "For small projects", 29.99m);

        Assert.Equal("Starter", plan.Name);
        Assert.Equal("For small projects", plan.Description);
        Assert.Equal(29.99m, plan.Price);
        Assert.Equal("monthly", plan.Interval);
        Assert.Equal("[]", plan.FeaturesJson);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_an_existing_plan_when_plan_details_are_updated_then_new_values_are_kept()
    {
        var plan = new Plan("Starter", "For small projects", 29.99m);

        plan.Update("Professional", "For growing projects", 89.99m, "yearly", "[\"Analytics\"]");

        Assert.Equal("Professional", plan.Name);
        Assert.Equal("For growing projects", plan.Description);
        Assert.Equal(89.99m, plan.Price);
        Assert.Equal("yearly", plan.Interval);
        Assert.Equal("[\"Analytics\"]", plan.FeaturesJson);
    }
}
