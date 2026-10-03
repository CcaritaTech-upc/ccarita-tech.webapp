using IoBuild.Api.Publishing.Application.Internal.CommandServices;
using IoBuild.Api.Publishing.Application.Internal.QueryServices;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Services.Commands;
using IoBuild.Api.Publishing.Domain.Services.Queries;
using IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Repositories;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Publishing.Clients.Application;

[Trait("Context", "Publishing")]
[Trait("Capability", "Clients")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class ClientCommandQueryTests
{
    [Fact]
    [Trait("Category", "DDD")]
    public async Task ClientCommandService_supports_full_crud_lifecycle()
    {
        await using var db = CreateDb();
        var repo = new ClientRepository(db);
        var commandService = new ClientCommandService(repo, db);
        var queryService = new ClientQueryService(repo);

        var clientId = await commandService.Handle(new CreateClientCommand("Acme Corp", "Tower Alpha", "Paid", 10, 1));
        Assert.True(clientId > 0);

        var client = await queryService.Handle(new GetClientByIdQuery(clientId));
        Assert.NotNull(client);
        Assert.Equal("Acme Corp", client!.FullName);

        await commandService.Handle(new UpdateClientCommand(clientId, "Acme International", "Tower Alpha", "Pending", 10, 1));
        var updated = await queryService.Handle(new GetClientByIdQuery(clientId));
        Assert.Equal("Acme International", updated!.FullName);
        Assert.Equal("Pending", updated.AccountStatement);

        await commandService.Handle(new DeleteClientCommand(clientId));
        var deleted = await queryService.Handle(new GetClientByIdQuery(clientId));
        Assert.Null(deleted);
    }

    [Fact]
    [Trait("Category", "DDD")]
    public async Task ClientCommandService_links_unit_and_owner_projection()
    {
        await using var db = CreateDb();
        var unit = new Unit(1, "101", null, 1, "101");
        db.Units.Add(unit);
        var user = new IamUser { Email = "owner@domain.com", PasswordHash = "hash", Role = "Owner" };
        db.IamUsers.Add(user);
        await db.SaveChangesAsync();

        var repo = new ClientRepository(db);
        var commandService = new ClientCommandService(repo, db);
        var queryService = new ClientQueryService(repo);

        var clientId = await commandService.Handle(new CreateClientCommand(
            "Jane Doe", "Tower Alpha", "Paid", 10, 1,
            Email: "owner@domain.com",
            PhoneNumber: "+51999888777",
            Address: "Av. Central 123",
            UnitId: unit.Id));

        var client = await queryService.Handle(new GetClientByIdQuery(clientId));
        Assert.NotNull(client);
        Assert.Equal("Jane Doe", client!.FullName);
        Assert.Equal("owner@domain.com", client.Email);
        Assert.Equal(unit.Id, client.UnitId);
        Assert.Equal("101", client.UnitNumber);

        var updatedUnit = await db.Units.FindAsync(unit.Id);
        Assert.Equal("occupied", updatedUnit!.Status);
        Assert.Equal("owner@domain.com", updatedUnit.OwnerEmail);
        Assert.Equal(user.Id, updatedUnit.OwnerId);

        var projection = await db.UnitOwnerProjections.FirstOrDefaultAsync(p => p.UnitId == unit.Id);
        Assert.NotNull(projection);
        Assert.Equal(user.Id, projection!.OwnerUserId);
    }
}
