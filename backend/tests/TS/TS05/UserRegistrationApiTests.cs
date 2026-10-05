using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.TechnicalStories.TS05;

[Trait("TechnicalStory", "TS05")]
public sealed class UserRegistrationApiTests
{
    [Fact]
    public async Task Register_creates_account_returns_message_and_persists_hashed_password()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        const string email = "technical.story.builder@example.test";
        const string password = "builder-secret-123";

        using var response = await RegisterAsync(client, email, password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("User created successfully.", responseJson.GetProperty("message").GetString());
        Assert.False(responseJson.TryGetProperty("token", out _));
        Assert.False(responseJson.TryGetProperty("passwordHash", out _));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        var user = Assert.Single(await db.IamUsers.Where(item => item.Email == email).ToListAsync());
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(new PasswordHasher().Verify(password, user.PasswordHash));
    }

    [Fact]
    public async Task Register_returns_conflict_for_duplicate_email_case_insensitively()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        using var first = await RegisterAsync(client, "duplicate.story@example.test", "builder-secret-123");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var duplicate = await RegisterAsync(client, "DUPLICATE.STORY@example.test", "builder-secret-123");

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var error = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("already exists", error.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_returns_bad_request_for_invalid_email_or_short_password()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();

        using var invalidEmail = await RegisterAsync(client, "not-an-email", "builder-secret-123");
        using var shortPassword = await RegisterAsync(client, "short.password@example.test", "1234567");

        Assert.Equal(HttpStatusCode.BadRequest, invalidEmail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/users", new { email, password, role = "Builder" });
}
