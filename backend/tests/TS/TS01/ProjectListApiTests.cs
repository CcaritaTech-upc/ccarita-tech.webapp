using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.TechnicalStories.TS01;

[Trait("TechnicalStory", "TS01")]
public sealed class ProjectListApiTests
{
    [Fact]
    public async Task List_returns_only_authenticated_builders_projects_with_contract_fields()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        const int builderId = 7101;
        const int otherBuilderId = 7102;
        var builderToken = TechnicalStoryApiTestSupport.Token(builderId);
        var otherBuilderToken = TechnicalStoryApiTestSupport.Token(otherBuilderId);

        var ownProjectId = await TechnicalStoryApiTestSupport.CreateProjectAsync(client, builderToken, builderId, "Cedar Heights");
        await TechnicalStoryApiTestSupport.CreateProjectAsync(client, otherBuilderToken, otherBuilderId, "Harbor Residences");

        using var response = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/projects", builderToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<JsonElement>();
        var project = Assert.Single(projects.EnumerateArray());
        Assert.Equal(ownProjectId, project.GetProperty("id").GetInt32());
        foreach (var property in new[]
        {
            "name", "description", "location", "totalUnits", "occupiedUnits", "builderId",
            "imageUrl", "structureDefined", "createdAt"
        })
        {
            Assert.True(project.TryGetProperty(property, out _), $"Project response is missing '{property}'.");
        }
        Assert.False(project.TryGetProperty("status", out _));
        Assert.False(project.TryGetProperty("occupancyRate", out _));
    }

    [Fact]
    public async Task List_returns_empty_array_when_builder_has_no_projects()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        var token = TechnicalStoryApiTestSupport.Token(7103);

        using var response = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/projects", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, projects.ValueKind);
        Assert.Empty(projects.EnumerateArray());
    }
}
