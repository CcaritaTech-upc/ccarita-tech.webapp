using IoBuild.Api.IAM.Infrastructure.Hashing;

public sealed partial class IamTierDTests
{
    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "D")]
    public void IAM_LOGIN_HASH_MUTANT_wrong_password_never_verifies()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correct-secret");
        // Kills the mutant that returns true unconditionally.
        Assert.False(hasher.Verify("wrong-secret", hash));
        Assert.False(hasher.Verify("", hash));
        Assert.True(hasher.Verify("correct-secret", hash));
    }
}
