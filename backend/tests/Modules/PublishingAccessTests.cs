using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IoBuild.Modules.Tests;

// Convergent Testing Tier A: PUBLISHING mutations require owning the project
// (directly or through the unit/client parent). Foreign ids read as not found.
public sealed class PublishingAccessTests
{
    [Fact]
    [Trait("Flow", "PUBLISHING.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task PROJECTS_create_read_update_delete_respect_ownership()
    {
        await using var factory = new PublishingApiFactory();
        using var client = factory.CreateClient();
        var me = Token(71, "me71@example.test", "Builder");
        var rival = Token(72, "rival72@example.test", "Builder");

        using var foreignCreate = await SendAsync(client, HttpMethod.Post, "/api/v1/projects", me,
            "{\"name\":\"Rogue\",\"description\":\"D\",\"location\":\"L\",\"totalUnits\":1,\"builderId\":72,\"imageUrl\":null}");
        Assert.Equal(HttpStatusCode.Forbidden, foreignCreate.StatusCode);

        var projectId = await CreateProjectAsync(client, me, 71, "Mine");

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"/api/v1/projects/{projectId}", me)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"/api/v1/projects/{projectId}", rival)).StatusCode);

        using var foreignPut = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}", rival,
            "{\"name\":\"Hacked\",\"description\":\"D\",\"location\":\"L\",\"totalUnits\":1,\"builderId\":72,\"imageUrl\":null}");
        Assert.Equal(HttpStatusCode.NotFound, foreignPut.StatusCode);

        using var ownPut = await SendAsync(client, HttpMethod.Put, $"/api/v1/projects/{projectId}", me,
            "{\"name\":\"Mine v2\",\"description\":\"D\",\"location\":\"L\",\"totalUnits\":1,\"builderId\":71,\"imageUrl\":null}");
        Assert.Equal(HttpStatusCode.NoContent, ownPut.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/projects/{projectId}", rival)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/projects/{projectId}", me)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/projects/1")).StatusCode);
    }

    [Fact]
    [Trait("Flow", "PUBLISHING.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task STRUCTURE_AND_UNITS_require_own_project()
    {
        await using var factory = new PublishingApiFactory();
        using var client = factory.CreateClient();
        var me = Token(73, "me73@example.test", "Builder");
        var rival = Token(74, "rival74@example.test", "Builder");
        var owner = Token(75, "owner75@example.test", "Owner");
        var projectId = await CreateProjectAsync(client, me, 73, "Struct Tower");

        using var rivalStructure = await SendAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", rival,
            "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":null}");
        Assert.Equal(HttpStatusCode.NotFound, rivalStructure.StatusCode);

        using var ownerStructure = await SendAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", owner,
            "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":null}");
        Assert.Equal(HttpStatusCode.Forbidden, ownerStructure.StatusCode);

        using var ownUnit = await SendAsync(client, HttpMethod.Post, "/api/v1/units", me,
            "{\"projectId\":" + projectId + ",\"unitNumber\":\"S1\",\"ownerId\":null,\"floor\":1,\"roomNumber\":\"S1\"}");
        Assert.Equal(HttpStatusCode.Created, ownUnit.StatusCode);
        var unitId = (await ownUnit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        using var foreignUnit = await SendAsync(client, HttpMethod.Post, "/api/v1/units", rival,
            "{\"projectId\":" + projectId + ",\"unitNumber\":\"S2\",\"ownerId\":null,\"floor\":1,\"roomNumber\":\"S2\"}");
        Assert.Equal(HttpStatusCode.NotFound, foreignUnit.StatusCode);

        using var foreignAssign = await SendAsync(client, new HttpMethod("PATCH"), $"/api/v1/units/{unitId}/assign-owner", rival,
            "{\"ownerEmail\":\"x@example.test\",\"ownerId\":null}");
        Assert.Equal(HttpStatusCode.NotFound, foreignAssign.StatusCode);
    }

    [Fact]
    [Trait("Flow", "PUBLISHING.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task CLIENTS_create_read_update_delete_respect_ownership()
    {
        await using var factory = new PublishingApiFactory();
        using var client = factory.CreateClient();
        var me = Token(76, "me76@example.test", "Builder");
        var rival = Token(77, "rival77@example.test", "Builder");
        var projectId = await CreateProjectAsync(client, me, 76, "Client Plaza");

        using var foreignCreate = await SendAsync(client, HttpMethod.Post, "/api/v1/clients", me,
            "{\"fullName\":\"Rogue\",\"projectName\":\"Client Plaza\",\"accountStatement\":\"Pending\",\"builderId\":77,\"projectId\":" + projectId + "}");
        Assert.Equal(HttpStatusCode.Forbidden, foreignCreate.StatusCode);

        using var created = await SendAsync(client, HttpMethod.Post, "/api/v1/clients", me,
            "{\"fullName\":\"Acme\",\"projectName\":\"Client Plaza\",\"accountStatement\":\"Pending\",\"builderId\":76,\"projectId\":" + projectId + ",\"email\":\"acme@example.test\"}");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"/api/v1/clients/{id}", me)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"/api/v1/clients/{id}", rival)).StatusCode);

        using var foreignPut = await SendAsync(client, HttpMethod.Put, $"/api/v1/clients/{id}", rival,
            "{\"fullName\":\"Hacked\",\"projectName\":\"Client Plaza\",\"accountStatement\":\"Pending\",\"builderId\":77,\"projectId\":" + projectId + "}");
        Assert.Equal(HttpStatusCode.NotFound, foreignPut.StatusCode);

        using var foreignDelete = await SendAsync(client, HttpMethod.Delete, $"/api/v1/clients/{id}", rival);
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);

        using var ownDelete = await SendAsync(client, HttpMethod.Delete, $"/api/v1/clients/{id}", me);
        Assert.Equal(HttpStatusCode.NoContent, ownDelete.StatusCode);
    }

    private static async Task<int> CreateProjectAsync(HttpClient client, string token, int builderId, string name)
    {
        using var response = await SendAsync(client, HttpMethod.Post, "/api/v1/projects", token,
            $"{{\"name\":\"{name}\",\"description\":\"D\",\"location\":\"L\",\"totalUnits\":1,\"builderId\":{builderId},\"imageUrl\":null}}");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static string Token(int id, string email, string role) => new IoBuild.Api.IAM.Infrastructure.Tokens.JwtTokenIssuer("iobuild-development-secret-must-be-replaced-before-production")
        .Issue(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = id, Email = email, Role = role });

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, string? json = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private sealed class PublishingApiFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IoBuildDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IoBuildDbContext>>();
            services.AddDbContext<IoBuildDbContext>(options => options.UseInMemoryDatabase(databaseName));
            var readiness = new IoBuild.Api.Readiness.MigrationReadiness();
            readiness.RecordMigrationSuccess();
            services.AddSingleton(readiness);
            services.RemoveAll<IHostedService>();
        }).ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mqtt:Enabled"] = "false"
        }));
    }
}
