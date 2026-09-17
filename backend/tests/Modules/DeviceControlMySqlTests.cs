using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.TestKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IoBuild.Modules.Tests;

// Convergent Testing G1: DEVICES.CONTROL durability on the production engine.
// Opt-in: skips with success without IOBUILD_TEST_MYSQL_CONNECTION. Dedicated
// high ids, deletes only what it created.
public sealed class DeviceControlMySqlTests
{
    private const int OwnerId = 91841;

    [Fact]
    [Trait("Category", "Devices")]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Command_and_shadow_are_durable_on_mysql()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using (var admin = MySqlFixture.CreateIsolatedContext(connectionString))
        {
            admin.Projects.Add(new Project { Id = OwnerId, BuilderId = OwnerId, Name = "Probe Plaza" });
            var unit = new Unit(OwnerId, "91841", null, 9, "91841") { Id = OwnerId };
            admin.Units.Add(unit);
            admin.UnitOwnerProjections.Add(new UnitOwnerProjection { UnitId = OwnerId, OwnerUserId = OwnerId, UpdatedAt = DateTimeOffset.UtcNow });
            admin.Devices.Add(new Device { Id = OwnerId, Name = "Probe Light", Type = "SmartLight", ProjectId = OwnerId, UnitId = OwnerId, OwnerId = 0, Status = "online" });
            await admin.SaveChangesAsync();
        }

        try
        {
            await using var factory = new MySqlDeviceApiFactory(connectionString);
            using var client = factory.CreateClient();
            var owner = Token(OwnerId);
            using var command = await SendAsync(client, HttpMethod.Post, $"/api/v1/devices/{OwnerId}/commands", owner,
                "{\"attribute\":\"brightness\",\"value\":70}");
            Assert.Equal(HttpStatusCode.OK, command.StatusCode);

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            var rows = await reader.DeviceCommands.Where(c => c.DeviceId == OwnerId).ToListAsync();
            Assert.Single(rows);
            Assert.True(rows[0].PublishAttempts >= 0);
            var shadow = await reader.DeviceShadows.SingleAsync(s => s.DeviceId == OwnerId);
            Assert.Contains("brightness", shadow.DesiredJson);
            Assert.True(shadow.ShadowVersion >= 1);
        }
        finally
        {
            await CleanupAsync(connectionString);
        }
    }

    [Fact]
    [Trait("Category", "Devices")]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Telemetry_ingest_is_durable_and_visible_on_status()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using (var admin = MySqlFixture.CreateIsolatedContext(connectionString))
        {
            admin.Projects.Add(new Project { Id = OwnerId, BuilderId = OwnerId, Name = "Probe Plaza" });
            var unit = new Unit(OwnerId, "91841", null, 9, "91841") { Id = OwnerId };
            admin.Units.Add(unit);
            admin.UnitOwnerProjections.Add(new UnitOwnerProjection { UnitId = OwnerId, OwnerUserId = OwnerId, UpdatedAt = DateTimeOffset.UtcNow });
            admin.Devices.Add(new Device { Id = OwnerId, Name = "Probe Light", Type = "SmartLight", ProjectId = OwnerId, UnitId = OwnerId, OwnerId = 0, Status = "online" });
            await admin.SaveChangesAsync();
        }

        try
        {
            await using var factory = new MySqlDeviceApiFactory(connectionString);
            using var client = factory.CreateClient();
            var owner = Token(OwnerId);
            using var telemetry = await client.PostAsync("/api/v1/devices/telemetry",
                new StringContent($"{{\"deviceId\":{OwnerId},\"eventId\":\"evt-probe-1\",\"occurredAt\":\"2026-09-17T10:00:00Z\",\"status\":\"online\",\"reportedJson\":\"{{\\\"brightness\\\":70}}\",\"energyKwh\":0.2}}",
                    Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, telemetry.StatusCode);

            var status = await (await SendAsync(client, HttpMethod.Get, $"/api/v1/devices/{OwnerId}/status", owner)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(OwnerId, status.GetProperty("deviceId").GetInt32());

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            Assert.Single(await reader.DeviceTelemetry.Where(t => t.DeviceId == OwnerId).ToListAsync());
        }
        finally
        {
            await CleanupAsync(connectionString);
        }
    }

    private static async Task CleanupAsync(string connectionString)
    {
        await using var cleaner = MySqlFixture.CreateIsolatedContext(connectionString);
        var recoveries = await cleaner.TelemetryRecoveries.Where(r => r.EventId == "evt-probe-1").ToListAsync();
        if (recoveries.Count > 0) cleaner.TelemetryRecoveries.RemoveRange(recoveries);
        var commands = await cleaner.DeviceCommands.Where(c => c.DeviceId == OwnerId).ToListAsync();
        if (commands.Count > 0) cleaner.DeviceCommands.RemoveRange(commands);
        var telemetry = await cleaner.DeviceTelemetry.Where(t => t.DeviceId == OwnerId).ToListAsync();
        if (telemetry.Count > 0) cleaner.DeviceTelemetry.RemoveRange(telemetry);
        var shadows = await cleaner.DeviceShadows.Where(s => s.DeviceId == OwnerId).ToListAsync();
        if (shadows.Count > 0) cleaner.DeviceShadows.RemoveRange(shadows);
        var devices = await cleaner.Devices.Where(d => d.Id == OwnerId).ToListAsync();
        if (devices.Count > 0) cleaner.Devices.RemoveRange(devices);
        var projections = await cleaner.UnitOwnerProjections.Where(p => p.UnitId == OwnerId).ToListAsync();
        if (projections.Count > 0) cleaner.UnitOwnerProjections.RemoveRange(projections);
        var units = await cleaner.Units.Where(u => u.Id == OwnerId).ToListAsync();
        if (units.Count > 0) cleaner.Units.RemoveRange(units);
        var projects = await cleaner.Projects.Where(p => p.Id == OwnerId).ToListAsync();
        if (projects.Count > 0) cleaner.Projects.RemoveRange(projects);
        await cleaner.SaveChangesAsync();
    }

    private static string Token(int id) => new IoBuild.Api.IAM.Infrastructure.Tokens.JwtTokenIssuer("iobuild-development-secret-must-be-replaced-before-production")
        .Issue(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = id, Email = $"probe{id}@example.test", Role = "Owner" });

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, string? json = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private sealed class MySqlDeviceApiFactory(string connectionString) : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IoBuildDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IoBuildDbContext>>();
            services.AddDbContext<IoBuildDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
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
