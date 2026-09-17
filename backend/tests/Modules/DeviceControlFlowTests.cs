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

// Convergent Testing G1: DEVICES.CONTROL command→status flow versioned end to
// end at the API boundary (simulated transport: Mqtt disabled, no broker).
public sealed class DeviceControlFlowTests
{
    [Fact]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Contract")]
    [Trait("Risk", "A")]
    public async Task CONTROL_HAPPY_PATH_command_telemetry_and_status_converge()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, SeedUnitDeviceAsync);
        var owner = Token(51, "owner51@example.test", "Owner");

        using var command = await SendAsync(client, HttpMethod.Post, "/api/v1/devices/511/commands", owner,
            "{\"attribute\":\"brightness\",\"value\":80}");
        Assert.Equal(HttpStatusCode.OK, command.StatusCode);

        using var telemetry = await client.PostAsync("/api/v1/devices/telemetry",
            Json("{\"deviceId\":511,\"eventId\":\"evt-1\",\"occurredAt\":\"2026-09-17T10:00:00Z\",\"status\":\"online\",\"reportedJson\":\"{\\\"brightness\\\":80}\",\"energyKwh\":0.1}"));
        Assert.Equal(HttpStatusCode.OK, telemetry.StatusCode);

        var status = await (await SendAsync(client, HttpMethod.Get, "/api/v1/devices/511/status", owner)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(511, status.GetProperty("deviceId").GetInt32());
        Assert.Equal("online", status.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task CONTROL_REJECTS_wrong_role_wrong_unit_and_bad_attribute()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, SeedUnitDeviceAsync);
        var owner = Token(51, "owner51@example.test", "Owner");
        var stranger = Token(52, "stranger52@example.test", "Owner");
        var builder = Token(53, "builder53@example.test", "Builder");

        using var foreign = await SendAsync(client, HttpMethod.Post, "/api/v1/devices/511/commands", stranger,
            "{\"attribute\":\"brightness\",\"value\":80}");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);

        using var wrongRole = await SendAsync(client, HttpMethod.Post, "/api/v1/devices/511/commands", builder,
            "{\"attribute\":\"brightness\",\"value\":80}");
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);

        using var badAttribute = await SendAsync(client, HttpMethod.Post, "/api/v1/devices/511/commands", owner,
            "{\"attribute\":\"teleport\",\"value\":1}");
        Assert.Equal(HttpStatusCode.BadRequest, badAttribute.StatusCode);

        using var badRange = await SendAsync(client, HttpMethod.Post, "/api/v1/devices/511/commands", owner,
            "{\"attribute\":\"brightness\",\"value\":1000}");
        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);

        using var missing = await SendAsync(client, HttpMethod.Post, "/api/v1/devices/999999/commands", owner,
            "{\"attribute\":\"brightness\",\"value\":80}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var anonymous = await client.PostAsync("/api/v1/devices/511/commands",
            Json("{\"attribute\":\"brightness\",\"value\":80}"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private static async Task SeedUnitDeviceAsync(IoBuildDbContext db)
    {
        db.Projects.Add(new Project { Id = 51, BuilderId = 51, Name = "Control Tower" });
        var unit = new Unit(51, "511", null, 5, "511") { Id = 511 };
        db.Units.Add(unit);
        db.UnitOwnerProjections.Add(new UnitOwnerProjection { UnitId = 511, OwnerUserId = 51, UpdatedAt = DateTimeOffset.UtcNow });
        db.Devices.Add(new Device { Id = 511, Name = "Hall Light", Type = "SmartLight", ProjectId = 51, UnitId = 511, OwnerId = 0, Status = "online" });
        await db.SaveChangesAsync();
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

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

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
