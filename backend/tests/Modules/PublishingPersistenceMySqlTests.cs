using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.TestKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IoBuild.Modules.Tests;

// Convergent Testing G1: PUBLISHING.MANAGE guarantees on the production engine.
// Structure provisioning determinism plus ownership durability. Opt-in: skips
// with success without IOBUILD_TEST_MYSQL_CONNECTION. Dedicated high ids,
// deletes only what it created.
public sealed class PublishingPersistenceMySqlTests
{
    private const int BuilderId = 91851;

    [Fact]
    [Trait("Category", "Publishing")]
    [Trait("Flow", "PUBLISHING.MANAGE")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Structure_provisions_deterministically_and_rejects_redefine_on_mysql()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new MySqlPublishingApiFactory(connectionString);
        using var client = factory.CreateClient();
        var me = Token(BuilderId);
        try
        {
            using var project = await SendAsync(client, HttpMethod.Post, "/api/v1/projects", me,
                $"{{\"name\":\"Probe Towers\",\"description\":\"D\",\"location\":\"L\",\"totalUnits\":6,\"builderId\":{BuilderId},\"imageUrl\":null}}");
            Assert.Equal(HttpStatusCode.Created, project.StatusCode);
            var projectId = (await project.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            using var structure = await SendAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", me,
                "{\"floors\":2,\"unitsPerFloor\":3,\"floorNumbers\":null}");
            Assert.Equal(HttpStatusCode.Created, structure.StatusCode);

            using var redefine = await SendAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", me,
                "{\"floors\":2,\"unitsPerFloor\":3,\"floorNumbers\":null}");
            Assert.Equal(HttpStatusCode.Conflict, redefine.StatusCode);

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            Assert.Equal(6, await reader.Units.Where(u => u.ProjectId == projectId).CountAsync());
            // 2 floors x 3 floor devices plus 6 units x 2 unit devices.
            Assert.Equal(6, await reader.Devices.Where(d => d.ProjectId == projectId && d.Source == "FloorProvisioned").CountAsync());
            Assert.Equal(12, await reader.Devices.Where(d => d.ProjectId == projectId && d.Source == "UnitProvisioned").CountAsync());
        }
        finally
        {
            await CleanupProjectAsync(connectionString);
        }
    }

    [Fact]
    [Trait("Category", "Publishing")]
    [Trait("Flow", "PUBLISHING.MANAGE")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Ownership_boundaries_hold_on_mysql()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new MySqlPublishingApiFactory(connectionString);
        using var client = factory.CreateClient();
        var me = Token(BuilderId);
        var rival = Token(91852);
        try
        {
            using var project = await SendAsync(client, HttpMethod.Post, "/api/v1/projects", me,
                $"{{\"name\":\"Probe Plaza\",\"description\":\"D\",\"location\":\"L\",\"totalUnits\":1,\"builderId\":{BuilderId},\"imageUrl\":null}}");
            var projectId = (await project.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"/api/v1/projects/{projectId}", rival)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/projects/{projectId}", rival)).StatusCode);

            using var ownDelete = await SendAsync(client, HttpMethod.Delete, $"/api/v1/projects/{projectId}", me);
            Assert.Equal(HttpStatusCode.NoContent, ownDelete.StatusCode);

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            Assert.Empty(await reader.Projects.Where(p => p.Id == projectId).ToListAsync());
        }
        finally
        {
            await CleanupProjectAsync(connectionString);
        }
    }

    private static async Task CleanupProjectAsync(string connectionString)
    {
        await using var cleaner = MySqlFixture.CreateIsolatedContext(connectionString);
        var projectIds = await cleaner.Projects.Where(p => p.BuilderId == BuilderId).Select(p => p.Id).ToListAsync();
        if (projectIds.Count == 0) return;
        var deviceIds = await cleaner.Devices.Where(d => projectIds.Contains(d.ProjectId)).Select(d => d.Id).ToListAsync();
        var unitIds = await cleaner.Units.Where(u => projectIds.Contains(u.ProjectId)).Select(u => u.Id).ToListAsync();
        if (deviceIds.Count > 0)
        {
            cleaner.DeviceCommands.RemoveRange(await cleaner.DeviceCommands.Where(c => deviceIds.Contains(c.DeviceId)).ToListAsync());
            cleaner.DeviceTelemetry.RemoveRange(await cleaner.DeviceTelemetry.Where(t => deviceIds.Contains(t.DeviceId)).ToListAsync());
            cleaner.DeviceShadows.RemoveRange(await cleaner.DeviceShadows.Where(s => deviceIds.Contains(s.DeviceId)).ToListAsync());
            cleaner.DeviceProjections.RemoveRange(await cleaner.DeviceProjections.Where(p => deviceIds.Contains(p.DeviceId)).ToListAsync());
            cleaner.Devices.RemoveRange(await cleaner.Devices.Where(d => deviceIds.Contains(d.Id)).ToListAsync());
        }
        if (unitIds.Count > 0)
        {
            cleaner.UnitOwnerProjections.RemoveRange(await cleaner.UnitOwnerProjections.Where(p => unitIds.Contains(p.UnitId)).ToListAsync());
            cleaner.Units.RemoveRange(await cleaner.Units.Where(u => unitIds.Contains(u.Id)).ToListAsync());
        }
        cleaner.UnitProjections.RemoveRange(await cleaner.UnitProjections.Where(p => projectIds.Contains(p.ProjectId)).ToListAsync());
        cleaner.Clients.RemoveRange(await cleaner.Clients.Where(c => c.BuilderId == BuilderId).ToListAsync());
        cleaner.Projects.RemoveRange(await cleaner.Projects.Where(p => p.BuilderId == BuilderId).ToListAsync());
        await cleaner.SaveChangesAsync();
    }

    private static string Token(int id) => new IoBuild.Api.IAM.Infrastructure.Tokens.JwtTokenIssuer("iobuild-development-secret-must-be-replaced-before-production")
        .Issue(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = id, Email = $"probe{id}@example.test", Role = "Builder" });

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, string? json = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    private sealed class MySqlPublishingApiFactory(string connectionString) : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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
