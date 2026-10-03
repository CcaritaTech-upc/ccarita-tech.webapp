using IoBuild.Api.Publishing.Domain.Model.Aggregates;

namespace IoBuild.Modules.Tests.Publishing.Units.Domain;

[Trait("Context", "Publishing")]
[Trait("Capability", "Units")]
[Trait("Layer", "Domain")]
[Trait("Dependency", "None")]
public sealed class UnitEntityTests
{
    [Fact]
    [Trait("Category", "BDD")]
    public void Given_a_unit_without_a_room_number_when_created_then_unit_number_is_used_as_room_number()
    {
        var unit = new Unit(projectId: 7, unitNumber: "A-101");

        Assert.Equal(7, unit.ProjectId);
        Assert.Equal("A-101", unit.UnitNumber);
        Assert.Equal("A-101", unit.RoomNumber);
        Assert.Equal("available", unit.Status);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_an_available_unit_when_an_owner_is_assigned_then_unit_becomes_occupied()
    {
        var unit = new Unit(projectId: 7, unitNumber: "A-101");

        unit.AssignOwner("owner@example.com", ownerId: 42);

        Assert.Equal("owner@example.com", unit.OwnerEmail);
        Assert.Equal(42, unit.OwnerId);
        Assert.Equal("occupied", unit.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "BDD")]
    public void Given_an_occupied_unit_when_owner_email_is_cleared_then_unit_becomes_available(string? email)
    {
        var unit = new Unit(projectId: 7, unitNumber: "A-101");
        unit.AssignOwner("owner@example.com", ownerId: 42);

        unit.AssignOwner(email);

        Assert.Null(unit.OwnerEmail);
        Assert.Null(unit.OwnerId);
        Assert.Equal("available", unit.Status);
    }
}
