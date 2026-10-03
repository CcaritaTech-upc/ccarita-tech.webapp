using IoBuild.Api.Profiles.Application.Internal.CommandServices;
using Microsoft.EntityFrameworkCore;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Profiles.Creation.Application;

[Trait("Context", "Profiles")]
[Trait("Capability", "Creation")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class ProfileCreationTests
{
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task CreateProfile_persists_owner_age_and_builder_years_as_distinct_fields()
    {
        await using var db = CreateDb();
        var service = new ProfileCommandService(db);
        var created = await service.CreateProfileAsync(
            userId: 99,
            name: "John Builder",
            username: "jbuilder",
            phoneNumber: "+51987654321",
            address: "Av. Primavera 123",
            secondEmail: "alt@iobuild.test",
            age: 32,
            photoUrl: "https://res.cloudinary.com/demo/image/upload/sample.jpg",
            yearsInBusiness: 0);

        Assert.Equal(99, created.UserId);
        Assert.Equal("John Builder", created.Name);
        Assert.Equal("jbuilder", created.Username);
        Assert.Equal("+51987654321", created.PhoneNumber);
        Assert.Equal("Av. Primavera 123", created.Address);
        Assert.Equal("alt@iobuild.test", created.SecondEmail);
        Assert.Equal(32, created.Age);
        Assert.Equal(0, created.YearsInBusiness);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/sample.jpg", created.PhotoUrl);

        var fromDb = await db.Profiles.SingleAsync(p => p.UserId == 99);
        Assert.Equal("+51987654321", fromDb.PhoneNumber);
        Assert.Equal("Av. Primavera 123", fromDb.Address);
        Assert.Equal("alt@iobuild.test", fromDb.SecondEmail);
        Assert.Equal(32, fromDb.Age);
        Assert.Equal(0, fromDb.YearsInBusiness);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    [Trait("Category", "CoreBusiness")]
    public async Task CreateProfile_rejects_years_in_business_outside_the_supported_range(int yearsInBusiness)
    {
        await using var db = CreateDb();
        var service = new ProfileCommandService(db);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.CreateProfileAsync(
            userId: 100,
            name: "Builder",
            username: "builder100",
            yearsInBusiness: yearsInBusiness));

        Assert.Empty(await db.Profiles.ToListAsync());
    }
}
