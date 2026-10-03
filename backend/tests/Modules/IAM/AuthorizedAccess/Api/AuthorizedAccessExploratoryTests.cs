using System.Net;

public sealed partial class IamTierDTests
{
    [Fact]
    [Trait("Flow", "IAM.AUTHORIZED_ACCESS")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_AUTHORIZED_ACCESS_CORRUPT_BEARER_is_rejected()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "!!!not-a-jwt!!!");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    [Trait("Flow", "IAM.AUTHORIZED_ACCESS")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_MALFORMED_AUTHORIZATION_never_server_errors()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        var headers = new[]
        {
            null as string,
            "Bearer",
            "Bearer ",
            "Bearer !!!not-a-jwt!!!",
            "Basic dXNlcjpwYXNz",
            "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.invalid-signature",
            new string('B', 5000),
        };
        foreach (var header in headers)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
            if (header is not null) request.Headers.TryAddWithoutValidation("Authorization", header);
            using var response = await client.SendAsync(request);
            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
                $"Authorization variant returned {response.StatusCode}");
        }
    }
}
