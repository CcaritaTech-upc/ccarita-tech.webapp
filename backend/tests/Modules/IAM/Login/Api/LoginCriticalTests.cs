using System.Net;

namespace IoBuild.Modules.Tests;

public sealed partial class IamTierATests
{
    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_LOGIN_INVALID_CREDENTIALS_rejects_wrong_password_without_token()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var email = $"tier-a-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Builder\"}}"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsync("/api/v1/sessions", Json($"{{\"email\":\"{email}\",\"password\":\"wrong-password\"}}"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_LOGIN_UNKNOWN_USER_is_rejected()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var login = await client.PostAsync("/api/v1/sessions", Json("{\"email\":\"unknown-tier-a@example.test\",\"password\":\"secret123\"}"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
