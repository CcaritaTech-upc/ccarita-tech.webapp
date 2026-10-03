using System.Net;
using System.Net.Http.Json;

namespace IoBuild.Modules.Tests;

public sealed partial class IamTierATests
{
    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_REGISTRATION_DUPLICATE_HTTP_preserves_characterized_contract()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.test";
        var first = await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Builder\"}}"));
        var second = await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Builder\"}}"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        // Duplicate registration must return 409 Conflict to prevent re-registration
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_REGISTRATION_UNKNOWN_ROLE_is_rejected_without_user()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var email = $"escalate-{Guid.NewGuid():N}@example.test";
        var response = await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Admin\"}}"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // The rejected registration must not authenticate.
        var login = await client.PostAsync("/api/v1/sessions", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\"}}"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_REGISTRATION_ROLE_IS_CANONICAL_for_case_variants()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var email = $"canonical-{Guid.NewGuid():N}@example.test";
        var register = await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"builder\"}}"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var session = await client.PostAsync("/api/v1/sessions", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\"}}"));
        Assert.Equal(HttpStatusCode.Created, session.StatusCode);
        var authenticated = await session.Content.ReadFromJsonAsync<IoBuild.Api.IAM.Domain.Model.Commands.AuthenticatedUser>();
        Assert.Equal("Builder", authenticated!.Role);
    }

    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "B")]
    public async Task IAM_ERROR_CONTRACT_never_leaks_internals()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var badRegister = await client.PostAsync("/api/v1/users", Json("{\"email\":\"\",\"password\":\"\"}"));
        Assert.Equal(HttpStatusCode.BadRequest, badRegister.StatusCode);
        var badLogin = await client.PostAsync("/api/v1/sessions", Json("{\"email\":\"nobody@example.test\",\"password\":\"x\"}"));
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
        var noToken = await client.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

        foreach (var response in new[] { badRegister, badLogin, noToken })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("at IoBuild", body, StringComparison.Ordinal);
            Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
