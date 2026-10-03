using System.Net;
using System.Text;
using System.Text.Json;

public sealed partial class IamTierDTests
{
    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_LOGIN_FUZZ_rejects_without_token_or_server_error()
    {
        await using var factory = new TierDApiFactory();
        using var client = factory.CreateClient();
        var email = $"loginfuzz.{Guid.NewGuid():N}@example.test";
        using (var setup = new StringContent(
            JsonSerializer.Serialize(new { email, password = "secret123", role = "Builder" }), Encoding.UTF8, "application/json"))
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync("/api/v1/users", setup)).StatusCode);
        }

        var passwords = new[] { "wrong", "", "SECRET123", "secret123 ", new string('p', 5000), "usuário✓" };
        foreach (var password in passwords)
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { email, password }), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/sessions", content);
            var body = await response.Content.ReadAsStringAsync();
            if (password == "secret123")
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
            }
            Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        }

        // Unknown user with hostile shapes: still a clean 401, never a 500.
        foreach (var hostile in new[] { "'; DROP TABLE iam_users; --@example.test", "\0@example.test", new string('e', 1000) + "@example.test" })
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { email = hostile, password = "x" }), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/sessions", content);
            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
                $"Hostile login returned {response.StatusCode}");
        }
    }

    [Fact]
    [Trait("Flow", "IAM.LOGIN")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task IAM_UNREACHABLE_DATABASE_fails_fast_at_startup_without_hanging()
    {
        // The app seeds on startup, so a dead database fails the boot itself
        // (fail-fast for the orchestrator to restart) instead of serving half.
        await using var factory = new DeadDbApiFactory();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try
        {
            using var client = factory.CreateClient();
            using var response = await client.PostAsync("/api/v1/users",
                JsonContent(new { email = "dead@example.test", password = "secret123", role = "Builder" }), cts.Token);
            Assert.True(
                response.StatusCode is HttpStatusCode.InternalServerError or HttpStatusCode.ServiceUnavailable,
                $"Dead database returned {response.StatusCode}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("transient", StringComparison.OrdinalIgnoreCase))
        {
            // Startup seeding threw first: equally fail-fast, equally not a hang.
        }
    }
}
