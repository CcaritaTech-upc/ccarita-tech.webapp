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

// Convergent Testing G3: DEVICES.CONTROL tiers. Lock serialization under
// burst, production-faithful error bodies, and deterministic fuzz partitions.
public sealed class DeviceTiersTests
{
    [Fact]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "C")]
    public async Task CONCURRENT_SAME_DEVICE_commands_serialize_to_one_shadow()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, SeedUnitDeviceAsync);
        var owner = Token(61, "owner61@example.test", "Owner");

        // Same value from every racer: the per-device lock must serialize the
        // burst into successes with one coherent shadow, whatever the order.
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            SendAsync(client, HttpMethod.Post, "/api/v1/devices/611/commands", owner,
                "{\"attribute\":\"brightness\",\"value\":80}")));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        foreach (var r in results) r.Dispose();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        Assert.Single(await db.DeviceShadows.Where(s => s.DeviceId == 611).ToListAsync());
        Assert.Equal(8, await db.DeviceCommands.Where(c => c.DeviceId == 611).CountAsync());
    }

    [Fact]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "B")]
    public async Task ERROR_CONTRACT_failure_paths_never_leak_internals()
    {
        // Production environment on purpose: Development prints stacks by design.
        await using var factory = new ProductionDeviceApiFactory();
        using var client = factory.CreateClient();
        var owner = Token(61, "owner61@example.test", "Owner");
        var cases = new List<HttpResponseMessage>
        {
            await SendAsync(client, HttpMethod.Get, "/api/v1/devices/999999", owner),
            await SendAsync(client, HttpMethod.Get, "/api/v1/devices/999999/energy", owner),
            await SendAsync(client, HttpMethod.Get, "/api/v1/devices/999999/status", owner),
        };
        using (var malformed = new StringContent("{not-json", Encoding.UTF8, "application/json"))
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/devices/611/commands") { Content = malformed };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner);
            cases.Add(await client.SendAsync(request));
        }
        cases.Add(await client.GetAsync("/api/v1/devices"));
        foreach (var response in cases)
        {
            using (response)
            {
                Assert.True(
                    response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized,
                    $"Failure path returned {response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
                Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("IoBuild.Api.", body, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task COMMAND_FUZZ_partitions_never_server_error()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, SeedUnitDeviceAsync);
        var owner = Token(61, "owner61@example.test", "Owner");
        var payloads = new[]
        {
            "{\"attribute\":\"brightness\",\"value\":-1}",
            "{\"attribute\":\"brightness\",\"value\":101}",
            "{\"attribute\":\"brightness\",\"value\":\"eighty\"}",
            "{\"attribute\":\"brightness\",\"value\":null}",
            "{\"attribute\":\"brightness\",\"value\":80.5}",
            "{\"attribute\":\"power\",\"value\":\"yes\"}",
            "{\"attribute\":\"mode\",\"value\":\"turbo\"}",
            "{\"attribute\":\"targetTemperature\",\"value\":15}",
            "{\"attribute\":\"targetTemperature\",\"value\":31}",
            $"{{\"attribute\":\"{new string('b', 500)}\",\"value\":1}}",
            "{\"attribute\":\"\",\"value\":1}",
            "{\"value\":1}",
            "{}",
        };
        foreach (var payload in payloads)
        {
            using var response = await SendAsync(client, HttpMethod.Post, "/api/v1/devices/611/commands", owner, payload);
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest,
                $"Fuzz partition returned {response.StatusCode}");
        }
    }

    [Fact]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task TELEMETRY_FUZZ_unknown_or_malformed_never_server_errors()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, SeedUnitDeviceAsync);
        var payloads = new[]
        {
            "{\"deviceId\":611,\"eventId\":\"e1\",\"occurredAt\":\"2026-09-17T10:00:00Z\",\"status\":\"online\",\"reportedJson\":\"{}\",\"energyKwh\":0.1}",
            "{\"deviceId\":999999,\"eventId\":\"e2\",\"occurredAt\":\"2026-09-17T10:00:00Z\",\"status\":\"online\",\"reportedJson\":\"{}\",\"energyKwh\":0.1}",
            "{\"deviceId\":-3,\"eventId\":\"e3\",\"occurredAt\":\"2026-09-17T10:00:00Z\",\"status\":\"online\",\"reportedJson\":\"{}\",\"energyKwh\":0.1}",
            "{\"deviceId\":611}",
        };
        foreach (var payload in payloads)
        {
            using var response = await client.PostAsync("/api/v1/devices/telemetry",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
                $"Telemetry fuzz returned {response.StatusCode}");
        }
        using (var malformed = new StringContent("{not-json", Encoding.UTF8, "application/json"))
        {
            using var response = await client.PostAsync("/api/v1/devices/telemetry", malformed);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private static async Task SeedUnitDeviceAsync(IoBuildDbContext db)
    {
        db.Projects.Add(new Project { Id = 61, BuilderId = 61, Name = "Tier Tower" });
        var unit = new Unit(61, "611", null, 6, "611") { Id = 611 };
        db.Units.Add(unit);
        db.UnitOwnerProjections.Add(new UnitOwnerProjection { UnitId = 611, OwnerUserId = 61, UpdatedAt = DateTimeOffset.UtcNow });
        db.Devices.Add(new Device { Id = 611, Name = "Tier Light", Type = "SmartLight", ProjectId = 61, UnitId = 611, OwnerId = 0, Status = "online" });
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

    private static async Task SeedAsync(WebApplicationFactory<Program> factory, Func<IoBuildDbContext, Task> seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    private class DeviceApiFactory : WebApplicationFactory<Program>
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

    private sealed class ProductionDeviceApiFactory : DeviceApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, Environments.Production);
            base.ConfigureWebHost(builder);
        }
    }
}
