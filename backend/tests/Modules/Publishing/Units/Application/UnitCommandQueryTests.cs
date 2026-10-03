using IoBuild.Api.Publishing.Application.Internal.CommandServices;
using IoBuild.Api.Publishing.Application.Internal.QueryServices;
using IoBuild.Api.Publishing.Domain.Services.Commands;
using IoBuild.Api.Publishing.Domain.Services.Queries;
using IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Repositories;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Publishing.Units.Application;

[Trait("Context", "Publishing")]
[Trait("Capability", "Units")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class UnitCommandQueryTests
{
    [Fact]
    [Trait("Category", "DDD")]
    public async Task UnitCommandService_creates_and_queries_unit_successfully()
    {
        await using var db = CreateDb();
        var repo = new UnitRepository(db);
        var commandService = new UnitCommandService(repo, db);
        var queryService = new UnitQueryService(repo);

        var unitId = await commandService.Handle(new CreateUnitCommand(1, "101", null, 1, "101"));
        Assert.True(unitId > 0);

        var retrieved = await queryService.Handle(new GetUnitByIdQuery(unitId));
        Assert.NotNull(retrieved);
        Assert.Equal("101", retrieved!.UnitNumber);
        Assert.Equal(1, retrieved.Floor);
        Assert.Equal(1, retrieved.ProjectId);
        Assert.Equal("available", retrieved.Status);
    }

    [Fact]
    [Trait("Category", "DDD")]
    public async Task AssignUnitOwner_synchronizes_with_unit_owner_projections()
    {
        await using var db = CreateDb();
        var user = new IamUser { Id = 42, Email = "owner@test.io", Role = "Owner" };
        db.IamUsers.Add(user);
        await db.SaveChangesAsync();

        var repo = new UnitRepository(db);
        var commandService = new UnitCommandService(repo, db);
        var queryService = new UnitQueryService(repo);

        var unitId = await commandService.Handle(new CreateUnitCommand(1, "201", null, 2, "201"));
        await commandService.Handle(new AssignUnitOwnerEmailCommand(unitId, "owner@test.io"));

        var unit = await queryService.Handle(new GetUnitByIdQuery(unitId));
        Assert.NotNull(unit);
        Assert.Equal("owner@test.io", unit!.OwnerEmail);
        Assert.Equal(42, unit.OwnerId);
        Assert.Equal("occupied", unit.Status);

        // Verify that UnitOwnerProjection was automatically synced
        var projection = await db.UnitOwnerProjections.FirstOrDefaultAsync(p => p.UnitId == unitId);
        Assert.NotNull(projection);
        Assert.Equal(42, projection!.OwnerUserId);
    }

    [Fact]
    [Trait("Category", "TDD")]
    public async Task AssignUnitOwner_when_user_does_not_exist_still_assigns_email_and_marks_occupied()
    {
        await using var db = CreateDb();
        var repo = new UnitRepository(db);
        var commandService = new UnitCommandService(repo, db);
        var queryService = new UnitQueryService(repo);

        var unitId = await commandService.Handle(new CreateUnitCommand(1, "301", null, 3, "301"));
        await commandService.Handle(new AssignUnitOwnerEmailCommand(unitId, "unregistered@resident.io"));

        var unit = await queryService.Handle(new GetUnitByIdQuery(unitId));
        Assert.NotNull(unit);
        Assert.Equal("unregistered@resident.io", unit!.OwnerEmail);
        Assert.Null(unit.OwnerId);
        Assert.Equal("occupied", unit.Status);

        // UnitOwnerProjections should NOT have an unlinked entry without valid OwnerUserId
        var projection = await db.UnitOwnerProjections.FirstOrDefaultAsync(p => p.UnitId == unitId);
        Assert.Null(projection);
    }

    [Fact]
    [Trait("Category", "TDD")]
    public async Task AssignUnitOwner_throws_KeyNotFoundException_for_invalid_unit()
    {
        await using var db = CreateDb();
        var repo = new UnitRepository(db);
        var commandService = new UnitCommandService(repo, db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            commandService.Handle(new AssignUnitOwnerEmailCommand(9999, "someone@test.io")));
    }
}
