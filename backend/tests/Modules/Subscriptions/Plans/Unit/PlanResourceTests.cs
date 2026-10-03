using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;

namespace IoBuild.Modules.Tests.Subscriptions.Plans.Unit;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "Plans")]
[Trait("Layer", "Unit")]
[Trait("Dependency", "None")]
public sealed class PlanResourceTests
{
    [Theory]
    [Trait("Category", "TDD")]
    [InlineData("[\"Up to 50 IoT devices\",\"Basic dashboard\"]", 2, "Up to 50 IoT devices")]
    [InlineData("[]", 0, null)]
    [InlineData("", 0, null)]
    [InlineData("   ", 0, null)]
    [InlineData(null, 0, null)]
    [InlineData("invalid_json_string", 0, null)]
    [InlineData("{\"key\":\"value\"}", 0, null)]
    public void PlanResource_features_deserialization_is_completely_resilient(string? featuresJson, int expectedCount, string? firstFeature)
    {
        var resource = new IoBuild.Api.Subscriptions.Interfaces.REST.Resources.PlanResource(
            1, "Test Plan", "Description", 99.99m, "monthly", featuresJson!);

        Assert.NotNull(resource.Features);
        Assert.Equal(expectedCount, resource.Features.Count);
        if (firstFeature is not null)
        {
            Assert.Equal(firstFeature, resource.Features[0]);
        }
    }

    [Fact]
    [Trait("Category", "TDD")]
    public void PlanResourceFromEntityAssembler_correctly_maps_all_plan_properties()
    {
        var plan = new Plan("Starter", "Small projects", 299m, "monthly", "[\"50 Devices\",\"Email Support\"]") { Id = 10 };
        var resource = IoBuild.Api.Subscriptions.Interfaces.REST.Transform.PlanResourceFromEntityAssembler.ToResourceFromEntity(plan);

        Assert.Equal(10, resource.Id);
        Assert.Equal("Starter", resource.Name);
        Assert.Equal(299m, resource.Price);
        Assert.Equal("monthly", resource.Interval);
        Assert.Equal(2, resource.Features.Count);
        Assert.Equal("50 Devices", resource.Features[0]);
    }
}
