using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IoBuild.Modules.Tests.TestSupport;

namespace IoBuild.Modules.Tests.TechnicalStories;

internal static class TechnicalStoryApiTestSupport
{
    internal static CoreBusinessTestSupport.CoreBusinessApiFactory CreateFactory() => new();

    internal static string Token(int id, string role = "Builder") =>
        CoreBusinessTestSupport.Token(id, $"{role.ToLowerInvariant()}{id}@example.test", role);

    internal static async Task<int> CreateProjectAsync(HttpClient client, string token, int builderId, string name)
    {
        var payload = JsonSerializer.Serialize(new
        {
            name,
            description = "Residential homes near the central park",
            location = "Av Primavera 123 Lima",
            totalUnits = 2,
            builderId,
            imageUrl = "https://example.test/project.jpg"
        });
        using var response = await CoreBusinessTestSupport.SendAuthorizedAsync(client, HttpMethod.Post, "/api/v1/projects", token, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }
}
