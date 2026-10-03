using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.TestKit;

public sealed partial class IamTierBTests
{
    [Fact]
    [Trait(Traits.Flow, "IAM.LOGIN")]
    [Trait(Traits.Layer, Layers.Application)]
    [Trait(Traits.Risk, Risks.B)]
    public async Task IAM_LOGIN_BLANK_EMAIL_is_rejected_without_user_lookup()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SignInAsync(new SignIn("   ", "secret123")));
    }
}
