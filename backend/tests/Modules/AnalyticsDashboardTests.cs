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

// Convergent Testing G1: ANALYTICS.VIEW dashboards return data for seeded
// tenants through the query services (projection self-sync included).
public sealed class AnalyticsDashboardTests
{
    [Fact]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Contract")]
    [Trait("Risk", "A")]
    public async Task BUILDER_DASHBOARD_returns_seeded_metrics()
    {
        await using var factory = new AnalyticsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, SeedTenantAsync);
        var builder = Token(95, "builder95@example.test", "Builder");

        var metrics = await (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/builders/95/metrics", builder))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, metrics.GetProperty("totalDevices").GetInt32());
        Assert.Equal(1, metrics.GetProperty("activeProjectsCount").GetInt32());
        Assert.Equal(1, metrics.GetProperty("totalUnits").GetInt32());
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Contract")]
    [Trait("Risk", "A")]
    public async Task OWNER_DASHBOARD_returns_seeded_metrics()
    {
        await using var factory = new AnalyticsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, SeedTenantAsync);
        var owner = Token(96, "owner96@example.test", "Owner");

        var metrics = await (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/owners/96/metrics", owner))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, metrics.GetProperty("myUnitsCount").GetInt32());
        var health = metrics.GetProperty("deviceHealthStatus").EnumerateArray().ToList();
        Assert.Single(health);
        Assert.Equal("Seed Light", health[0].GetProperty("deviceName").GetString());
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "B")]
    public async Task ENERGY_WINDOW_clamps_without_server_error()
    {
        await using var factory = new AnalyticsApiFactory();
        using var client = factory.CreateClient();
        var builder = Token(95, "builder95@example.test", "Builder");

        foreach (var minutes in new[] { "0", "5", "999", "NaN" })
        {
            using var response = await SendAsync(client, HttpMethod.Get, $"/api/v1/analytics/builders/95/energy?minutes={minutes}", builder);
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest,
                $"Energy window {minutes} returned {response.StatusCode}");
        }
    }

    internal static async Task SeedTenantAsync(IoBuildDbContext db)
    {
        db.IamUsers.Add(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = 95, Email = "builder95@example.test", PasswordHash = "hash", Role = "Builder" });
        db.IamUsers.Add(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = 96, Email = "owner96@example.test", PasswordHash = "hash", Role = "Owner" });
        db.Projects.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Project { Id = 95, BuilderId = 95, Name = "Metrics Towers" });
        var unit = new IoBuild.Api.Publishing.Domain.Model.Aggregates.Unit(95, "951", null, 9, "951") { Id = 951 };
        unit.OwnerId = 96;
        unit.OwnerEmail = "owner96@example.test";
        unit.Status = "occupied";
        db.Units.Add(unit);
        db.Devices.Add(new IoBuild.Api.Devices.Domain.Model.Aggregates.Device { Id = 951, Name = "Seed Light", Type = "SmartLight", ProjectId = 95, UnitId = 951, OwnerId = 96, Status = "online" });
        await db.SaveChangesAsync();
    }

    [Fact]
    [Trait("Category", "Analytics")]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Dashboards_and_projections_are_durable_on_mysql()
    {
        var connectionString = IoBuild.TestKit.MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        const int builderId = 91861;
        const int ownerId = 91862;
        await using (var admin = IoBuild.TestKit.MySqlFixture.CreateIsolatedContext(connectionString))
        {
            admin.IamUsers.Add(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = builderId, Email = "probe91861@example.test", PasswordHash = "hash", Role = "Builder" });
            admin.IamUsers.Add(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = ownerId, Email = "probe91862@example.test", PasswordHash = "hash", Role = "Owner" });
            admin.Projects.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Project { Id = builderId, BuilderId = builderId, Name = "Probe Metrics" });
            var unit = new IoBuild.Api.Publishing.Domain.Model.Aggregates.Unit(builderId, "91861", null, 9, "91861") { Id = builderId };
            unit.OwnerId = ownerId;
            unit.OwnerEmail = "probe91862@example.test";
            unit.Status = "occupied";
            admin.Units.Add(unit);
            admin.Devices.Add(new IoBuild.Api.Devices.Domain.Model.Aggregates.Device { Id = builderId, Name = "Probe Light", Type = "SmartLight", ProjectId = builderId, UnitId = builderId, OwnerId = ownerId, Status = "online" });
            await admin.SaveChangesAsync();
        }

        try
        {
            await using var factory = new MySqlAnalyticsApiFactory(connectionString);
            using var client = factory.CreateClient();

            var builderMetrics = await (await SendAsync(client, HttpMethod.Get, $"/api/v1/analytics/builders/{builderId}/metrics", Token(builderId, "probe91861@example.test", "Builder")))
                .Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, builderMetrics.GetProperty("totalDevices").GetInt32());

            var ownerMetrics = await (await SendAsync(client, HttpMethod.Get, $"/api/v1/analytics/owners/{ownerId}/metrics", Token(ownerId, "probe91862@example.test", "Owner")))
                .Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, ownerMetrics.GetProperty("myUnitsCount").GetInt32());

            await using var reader = IoBuild.TestKit.MySqlFixture.CreateIsolatedContext(connectionString);
            Assert.True(await reader.ProjectProjections.AnyAsync(p => p.ProjectId == builderId));
            Assert.True(await reader.UnitProjections.AnyAsync(p => p.UnitId == builderId));
            Assert.True(await reader.DeviceProjections.AnyAsync(p => p.DeviceId == builderId));
        }
        finally
        {
            await using var cleaner = IoBuild.TestKit.MySqlFixture.CreateIsolatedContext(connectionString);
            cleaner.DeviceProjections.RemoveRange(await cleaner.DeviceProjections.Where(p => p.DeviceId == builderId).ToListAsync());
            cleaner.UnitProjections.RemoveRange(await cleaner.UnitProjections.Where(p => p.UnitId == builderId).ToListAsync());
            cleaner.ProjectProjections.RemoveRange(await cleaner.ProjectProjections.Where(p => p.ProjectId == builderId).ToListAsync());
            cleaner.Devices.RemoveRange(await cleaner.Devices.Where(d => d.Id == builderId).ToListAsync());
            cleaner.Units.RemoveRange(await cleaner.Units.Where(u => u.Id == builderId).ToListAsync());
            cleaner.Projects.RemoveRange(await cleaner.Projects.Where(p => p.Id == builderId).ToListAsync());
            cleaner.IamUsers.RemoveRange(await cleaner.IamUsers.Where(u => u.Id == builderId || u.Id == ownerId).ToListAsync());
            await cleaner.SaveChangesAsync();
        }
    }

    private sealed class MySqlAnalyticsApiFactory(string connectionString) : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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

    internal static string Token(int id, string email, string role) => new IoBuild.Api.IAM.Infrastructure.Tokens.JwtTokenIssuer("iobuild-development-secret-must-be-replaced-before-production")
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

    private sealed class AnalyticsApiFactory : WebApplicationFactory<Program>
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
