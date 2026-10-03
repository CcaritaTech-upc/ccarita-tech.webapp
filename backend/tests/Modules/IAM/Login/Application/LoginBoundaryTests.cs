using IoBuild.Api.IAM.Domain.Model.Commands;

public sealed partial class IamTierCTests
{
    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "C")]
    public async Task IAM_LOGIN_OVERSIZED_INPUT_fails_closed()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        var oversized = new string('a', 5000) + "@example.test";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SignInAsync(new SignIn(oversized, "secret123")));
    }
}
