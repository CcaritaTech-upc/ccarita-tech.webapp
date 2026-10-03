using IoBuild.Api.IAM.Domain.Model.Commands;
using Microsoft.EntityFrameworkCore;

public sealed partial class IamTierDTests
{
    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_NORMALIZATION_IS_IDEMPOTENT_for_representative_inputs()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        foreach (var raw in new[] { "  Mixed@Example.Test ", "MIXED@example.test", "mixed@EXAMPLE.test" })
        {
            await service.RegisterAsync(new RegisterUser(raw, "secret123", "Builder"));
        }
        var users = await db.IamUsers.ToListAsync();
        Assert.Single(users);
        Assert.Equal("mixed@example.test", users[0].Email);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "D")]
    public async Task IAM_REGISTRATION_GUARD_MUTANTS_every_bypass_throws()
    {
        // Kills mutants that delete any single fail-closed guard.
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("", "secret123", "Owner")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("   ", "secret123", "Owner")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("mut@example.test", "", "Owner")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("mut@example.test", "   ", "Owner")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("mut@example.test", "secret123", "Admin")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(new RegisterUser("mut@example.test", "secret123", "")));
        Assert.Empty(await db.IamUsers.ToListAsync());
    }
}
