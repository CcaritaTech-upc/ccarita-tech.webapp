using IoBuild.Api.IAM.Domain.Model.Commands;
using Microsoft.EntityFrameworkCore;

public sealed partial class IamTierBTests
{
    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "B")]
    public async Task IAM_REGISTRATION_NORMALIZATION_links_case_and_whitespace_variants()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await service.RegisterAsync(new RegisterUser("  ADA-NORM@Example.Test ", "secret123", "Builder"));
        var session = await service.SignInAsync(new SignIn("ada-norm@example.test", "secret123"));
        Assert.Equal("ada-norm@example.test", session.Email);
        Assert.Single(await db.IamUsers.ToListAsync());
    }
}
