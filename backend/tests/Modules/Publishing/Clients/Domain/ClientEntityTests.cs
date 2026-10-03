using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Publishing.Domain.Model.ValueObjects;

namespace IoBuild.Modules.Tests.Publishing.Clients.Domain;

[Trait("Context", "Publishing")]
[Trait("Capability", "Clients")]
[Trait("Layer", "Domain")]
[Trait("Dependency", "None")]
public sealed class ClientEntityTests
{
    [Fact]
    [Trait("Category", "BDD")]
    public void Given_client_details_when_client_is_created_then_details_and_default_status_are_kept()
    {
        var client = new Client(
            "Alex Resident",
            "North Tower",
            EAccountStatement.Pending.ToString(),
            builderId: 3,
            projectId: 7,
            email: "alex@example.com");

        Assert.Equal("Alex Resident", client.FullName);
        Assert.Equal("North Tower", client.ProjectName);
        Assert.Equal(EAccountStatement.Pending.ToString(), client.AccountStatement);
        Assert.Equal(3, client.BuilderId);
        Assert.Equal(7, client.ProjectId);
        Assert.Equal("alex@example.com", client.Email);
        Assert.Null(client.UnitId);
        Assert.Null(client.UnitNumber);
    }

    [Fact]
    [Trait("Category", "BDD")]
    public void Given_an_existing_client_when_details_and_unit_are_updated_then_new_values_replace_old_values()
    {
        var client = new Client(
            "Alex Resident",
            "North Tower",
            EAccountStatement.Pending.ToString(),
            builderId: 3,
            projectId: 7,
            email: "alex@example.com",
            unitId: 11,
            unitNumber: "A-101");

        client.Update(
            "Alex Rivera",
            "South Tower",
            EAccountStatement.Paid.ToString(),
            builderId: 4,
            projectId: 8,
            email: "alex.rivera@example.com",
            phoneNumber: "+1-555-0100",
            address: "8 South Street",
            unitId: 12,
            unitNumber: "B-202");

        Assert.Equal("Alex Rivera", client.FullName);
        Assert.Equal("South Tower", client.ProjectName);
        Assert.Equal(EAccountStatement.Paid.ToString(), client.AccountStatement);
        Assert.Equal(4, client.BuilderId);
        Assert.Equal(8, client.ProjectId);
        Assert.Equal("alex.rivera@example.com", client.Email);
        Assert.Equal("+1-555-0100", client.PhoneNumber);
        Assert.Equal("8 South Street", client.Address);
        Assert.Equal(12, client.UnitId);
        Assert.Equal("B-202", client.UnitNumber);
    }
}
