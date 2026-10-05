using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.TechnicalStories.TS03;

[Trait("TechnicalStory", "TS03")]
public sealed class ClientListApiTests
{
    [Fact]
    public async Task List_filters_by_owned_project_and_returns_client_resource_fields()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        const int builderId = 7301;
        var token = TechnicalStoryApiTestSupport.Token(builderId);
        var projectId = await TechnicalStoryApiTestSupport.CreateProjectAsync(client, token, builderId, "Client Garden");
        await CreateClientAsync(client, token, builderId, projectId, "Maria Lopez", "maria.lopez@example.test");

        using var response = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/clients?builderId={builderId}&projectId={projectId}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var clients = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(clients.EnumerateArray());
        Assert.Equal("Maria Lopez", item.GetProperty("fullName").GetString());
        Assert.Equal("Pending", item.GetProperty("accountStatement").GetString());
        Assert.Equal(projectId, item.GetProperty("projectId").GetInt32());
        foreach (var property in new[]
        {
            "id", "fullName", "projectName", "accountStatement", "builderId", "projectId",
            "email", "phoneNumber", "address", "unitId", "unitNumber", "deviceCount"
        })
        {
            Assert.True(item.TryGetProperty(property, out _), $"Client response is missing '{property}'.");
        }
        Assert.False(item.TryGetProperty("associatedProject", out _));
    }

    [Fact]
    public async Task List_returns_empty_array_when_owned_project_has_no_clients()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        const int builderId = 7302;
        var token = TechnicalStoryApiTestSupport.Token(builderId);
        var projectId = await TechnicalStoryApiTestSupport.CreateProjectAsync(client, token, builderId, "Empty Client Garden");

        using var response = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/clients?projectId={projectId}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var clients = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, clients.ValueKind);
        Assert.Empty(clients.EnumerateArray());
    }

    [Fact]
    public async Task List_forbids_builder_filter_that_does_not_match_token()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        var token = TechnicalStoryApiTestSupport.Token(7303);

        using var response = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/clients?builderId=9999", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task CreateClientAsync(HttpClient client, string token, int builderId, int projectId, string fullName, string email)
    {
        var payload = JsonSerializer.Serialize(new
        {
            fullName,
            projectName = "Client Garden",
            accountStatement = "Pending",
            builderId,
            projectId,
            email,
            phoneNumber = "+51987654321",
            address = "Av Primavera 123 Lima"
        });

        using var response = await SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/clients", token, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
