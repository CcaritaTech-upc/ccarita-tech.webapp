using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using IoBuild.Api.Subscriptions.Application.Internal.QueryServices;
using IoBuild.Api.Subscriptions.Domain.Services.Commands;
using IoBuild.Api.Subscriptions.Domain.Services.Queries;
using IoBuild.Api.Subscriptions.Infrastructure.Persistence.EFC.Repositories;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Subscriptions.Plans.Application;

[Trait("Context", "Subscriptions")]
[Trait("Capability", "Plans")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class PlanCommandQueryTests
{
    [Fact]
    [Trait("Category", "DDD")]
    public async Task PlanCommandService_creates_and_queries_plans()
    {
        await using var db = CreateDb();
        var repo = new PlanRepository(db);
        var commandService = new PlanCommandService(repo, db);
        var queryService = new PlanQueryService(repo);

        var planId = await commandService.Handle(new CreatePlanCommand("Enterprise", "Full building access", 499.99m, "monthly", "[\"IoT\",\"Unlimited Units\"]"));
        Assert.True(planId > 0);

        var plan = await queryService.Handle(new GetPlanByIdQuery(planId));
        Assert.NotNull(plan);
        Assert.Equal("Enterprise", plan!.Name);
        Assert.Equal(499.99m, plan.Price);

        var allPlans = await queryService.Handle(new GetAllPlansQuery());
        Assert.Single(allPlans);
    }
}
