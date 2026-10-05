using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.TechnicalStories.TS02;

[Trait("TechnicalStory", "TS02")]
public sealed class ProjectCreateApiTests
{
    [Fact]
    public async Task Create_returns_created_project_with_server_generated_fields()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        const int builderId = 7201;
        var token = TechnicalStoryApiTestSupport.Token(builderId);
        var payload = JsonSerializer.Serialize(new
        {
            name = "North Garden",
            description = "Residential homes near the central park",
            location = "Av Primavera 123 Lima",
            totalUnits = 24,
            imageUrl = "https://example.test/north-garden.jpg"
        });

        using var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/projects", token, payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var project = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(project.GetProperty("id").GetInt32() > 0);
        Assert.Equal(builderId, project.GetProperty("builderId").GetInt32());
        Assert.Equal("North Garden", project.GetProperty("name").GetString());
        Assert.Equal(24, project.GetProperty("totalUnits").GetInt32());
        Assert.True(project.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task Create_returns_unprocessable_entity_with_error_for_invalid_text()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        var token = TechnicalStoryApiTestSupport.Token(7202);
        var payload = JsonSerializer.Serialize(new
        {
            name = "",
            description = "Residential homes near the central park",
            location = "Av Primavera 123 Lima",
            totalUnits = 2
        });

        using var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/projects", token, payload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task Create_rejects_builder_id_that_does_not_match_authenticated_builder()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        var token = TechnicalStoryApiTestSupport.Token(7203);
        var payload = JsonSerializer.Serialize(new
        {
            name = "North Garden",
            description = "Residential homes near the central park",
            location = "Av Primavera 123 Lima",
            totalUnits = 2,
            builderId = 9999
        });

        using var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/projects", token, payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
