using System.Net;
using System.Net.Http.Json;

namespace IoBuild.Modules.Tests;

public sealed partial class IamTierATests
{
    [Fact]
    [Trait("Flow", "IAM.AUTHORIZED_ACCESS")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_AUTHORIZED_ACCESS_TAMPERED_TOKEN_is_rejected()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        var email = $"tamper-{Guid.NewGuid():N}@example.test";
        await client.PostAsync("/api/v1/users", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\",\"role\":\"Builder\"}}"));
        var session = await client.PostAsync("/api/v1/sessions", Json($"{{\"email\":\"{email}\",\"password\":\"secret123\"}}"));
        var token = (await session.Content.ReadFromJsonAsync<IoBuild.Api.IAM.Domain.Model.Commands.AuthenticatedUser>())!.Token;

        using var tampered = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        tampered.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token + "tampered");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(tampered)).StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.AUTHORIZED_ACCESS")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task IAM_AUTHORIZED_ACCESS_MISSING_TOKEN_is_rejected()
    {
        await using var factory = new TierAApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users")).StatusCode);
    }
}
