using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Domain.Model.Entities;

public sealed partial class IamTierBTests
{
    [Fact]
    [Trait("Flow", "IAM.LOGOUT")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "B")]
    public async Task IAM_LOGOUT_EXPIRED_REVOCATION_is_not_considered_revoked()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await service.RegisterAsync(new RegisterUser("expiry@example.test", "secret123", "Builder"));
        var session = await service.SignInAsync(new SignIn("expiry@example.test", "secret123"));

        // Simulate an expired revocation row (cleanup boundary).
        var hash = IamService.HashToken(session.Token);
        db.RevokedTokens.Add(new RevokedToken { TokenHash = hash, ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1), RevokedAt = DateTimeOffset.UtcNow.AddDays(-8) });
        await db.SaveChangesAsync();

        Assert.False(await service.IsRevokedAsync(session.Token));
    }
}
