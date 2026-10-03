using System.Net;
using System.Text.Json;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Publishing.Structure.Api;

[Trait("Context", "Publishing")]
[Trait("Capability", "Structure")]
[Trait("Layer", "Api")]
[Trait("Dependency", "InMemory")]
public sealed class ProjectStructureRouteTests
{
    [Fact]
    [Trait("Category", "CoreBusiness")]
    public async Task Builder_project_list_and_structure_route_preserve_legacy_statuses()
    {
        await using var factory = new CoreBusinessApiFactory();
        using var client = factory.CreateClient();
        var builderToken = Token(1, "builder@example.test", "Builder");
        var otherToken = Token(2, "other@example.test", "Developer");

        var project = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/projects", builderToken, "{\"name\":\"P\",\"description\":\"D\",\"location\":\"L\",\"totalUnits\":1,\"builderId\":1,\"imageUrl\":null}");
        var projectId = JsonDocument.Parse(await project.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt32();
        var builderList = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/projects", builderToken);
        var hiddenList = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/projects", otherToken);
        var invalid = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", builderToken, "{\"floors\":0,\"unitsPerFloor\":1}");
        var forbidden = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", otherToken, "{\"floors\":1,\"unitsPerFloor\":1}");
        var outOfRange = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", builderToken, "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":[2]}");
        var created = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", builderToken, "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":[1]}");
        var duplicate = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", builderToken, "{\"floors\":1,\"unitsPerFloor\":1}");

        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
        Assert.Contains("\"builderId\":1", await builderList.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("[]", await hiddenList.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
}
