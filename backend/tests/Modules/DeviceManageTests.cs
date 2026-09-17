using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IoBuild.Modules.Tests;

// Convergent Testing Tier A: DEVICES mutations require a manager (the unit
// owner for unit devices, the project builder for project devices).
public sealed class DeviceManageTests
{
    [Fact]
    [Trait("Flow", "DEVICES.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task OWNER_MANAGES_OWN_UNIT_DEVICE_but_not_others()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, async db =>
        {
            db.Projects.Add(new Project { Id = 41, BuilderId = 41, Name = "Owner Tower" });
            var unit = new Unit(41, "415", null, 4, "415") { Id = 415 };
            db.Units.Add(unit);
            db.UnitOwnerProjections.Add(new UnitOwnerProjection { UnitId = 415, OwnerUserId = 41, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        var owner = Token(41, "owner41@example.test", "Owner");
        var stranger = Token(42, "stranger42@example.test", "Owner");

        using var created = await SendAsync(client, HttpMethod.Post, "/api/v1/devices", owner,
            "{\"name\":\"Hall Light\",\"type\":\"SmartLight\",\"location\":\"Hall\",\"projectId\":41,\"unitId\":415,\"status\":\"online\"}");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        using var foreignCreate = await SendAsync(client, HttpMethod.Post, "/api/v1/devices", stranger,
            "{\"name\":\"Rogue Light\",\"type\":\"SmartLight\",\"location\":\"Hall\",\"projectId\":41,\"unitId\":415,\"status\":\"online\"}");
        Assert.Equal(HttpStatusCode.Forbidden, foreignCreate.StatusCode);

        using var ownPut = await SendAsync(client, HttpMethod.Put, $"/api/v1/devices/{id}", owner,
            "{\"name\":\"Hall Light v2\",\"type\":\"SmartLight\",\"location\":\"Hall\",\"projectId\":41,\"status\":\"online\"}");
        Assert.Equal(HttpStatusCode.NoContent, ownPut.StatusCode);

        using var foreignPut = await SendAsync(client, HttpMethod.Put, $"/api/v1/devices/{id}", stranger,
            "{\"name\":\"Hacked\",\"type\":\"SmartLight\",\"location\":\"Hall\",\"projectId\":41,\"status\":\"online\"}");
        Assert.Equal(HttpStatusCode.NotFound, foreignPut.StatusCode);

        using var foreignDelete = await SendAsync(client, HttpMethod.Delete, $"/api/v1/devices/{id}", stranger);
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);

        using var ownDelete = await SendAsync(client, HttpMethod.Delete, $"/api/v1/devices/{id}", owner);
        Assert.Equal(HttpStatusCode.NoContent, ownDelete.StatusCode);
    }

    [Fact]
    [Trait("Flow", "DEVICES.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task BUILDER_MANAGES_OWN_PROJECT_DEVICE_but_not_others()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, async db =>
        {
            db.Projects.Add(new Project { Id = 43, BuilderId = 43, Name = "Builder Plaza" });
            db.Devices.Add(new Device { Id = 431, Name = "Floor Meter", Type = "SmartMeter", ProjectId = 43, Status = "online" });
            await db.SaveChangesAsync();
        });
        var builder = Token(43, "builder43@example.test", "Builder");
        var rival = Token(44, "rival44@example.test", "Builder");

        using var ownPut = await SendAsync(client, HttpMethod.Put, "/api/v1/devices/431", builder,
            "{\"name\":\"Renamed Meter\",\"type\":\"SmartMeter\",\"location\":\"Floor 1\",\"projectId\":43,\"status\":\"online\"}");
        Assert.Equal(HttpStatusCode.NoContent, ownPut.StatusCode);

        using var rivalPut = await SendAsync(client, HttpMethod.Put, "/api/v1/devices/431", rival,
            "{\"name\":\"Hacked\",\"type\":\"SmartMeter\",\"location\":\"Floor 1\",\"projectId\":43,\"status\":\"online\"}");
        Assert.Equal(HttpStatusCode.NotFound, rivalPut.StatusCode);

        using var rivalDelete = await SendAsync(client, HttpMethod.Delete, "/api/v1/devices/431", rival);
        Assert.Equal(HttpStatusCode.NotFound, rivalDelete.StatusCode);

        using var ownDelete = await SendAsync(client, HttpMethod.Delete, "/api/v1/devices/431", builder);
        Assert.Equal(HttpStatusCode.NoContent, ownDelete.StatusCode);

        using var gone = await SendAsync(client, HttpMethod.Get, "/api/v1/devices/431", builder);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    [Trait("Flow", "DEVICES.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task MISSING_TOKEN_is_unauthorized_on_mutations()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync("/api/v1/devices/1",
            new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/v1/devices/1")).StatusCode);
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

    private static async Task SeedAsync(WebApplicationFactory<Program> factory, Func<IoBuildDbContext, Task> seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    private sealed class DeviceApiFactory : WebApplicationFactory<Program>
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
