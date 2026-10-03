using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests.Analytics.Dashboard.Api;

public sealed partial class AnalyticsDashboardTests
{
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

    [Fact]
    [Trait("Category", "Analytics")]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "C")]
    [Trait("Dependency", "MySql")]
    public async Task Concurrent_dashboards_stay_consistent_on_mysql()
    {
        var connectionString = IoBuild.TestKit.MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        const int builderId = 91863;
        await using (var admin = IoBuild.TestKit.MySqlFixture.CreateIsolatedContext(connectionString))
        {
            admin.IamUsers.Add(new IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser { Id = builderId, Email = "probe91863@example.test", PasswordHash = "hash", Role = "Builder" });
            admin.Projects.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Project { Id = builderId, BuilderId = builderId, Name = "Race Metrics" });
            var unit = new IoBuild.Api.Publishing.Domain.Model.Aggregates.Unit(builderId, "91863", null, 9, "91863") { Id = builderId };
            admin.Units.Add(unit);
            admin.Devices.Add(new IoBuild.Api.Devices.Domain.Model.Aggregates.Device { Id = builderId, Name = "L", Type = "SmartLight", ProjectId = builderId, UnitId = builderId, OwnerId = 0, Status = "online" });
            await admin.SaveChangesAsync();
        }

        try
        {
            await using var factory = new MySqlAnalyticsApiFactory(connectionString);
            using var client = factory.CreateClient();
            var token = Token(builderId, "probe91863@example.test", "Builder");

            // Six racers on a tenant with no projections yet: the per-user gate
            // must serialize the self-sync so every load succeeds identically.
            var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/analytics/builders/{builderId}/metrics");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                return client.SendAsync(request);
            }));
            Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            foreach (var r in results) r.Dispose();

            await using var reader = IoBuild.TestKit.MySqlFixture.CreateIsolatedContext(connectionString);
            Assert.Single(await reader.ProjectProjections.Where(p => p.ProjectId == builderId).ToListAsync());
            Assert.Single(await reader.UnitProjections.Where(p => p.UnitId == builderId).ToListAsync());
            Assert.Single(await reader.DeviceProjections.Where(p => p.DeviceId == builderId).ToListAsync());
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
            cleaner.IamUsers.RemoveRange(await cleaner.IamUsers.Where(u => u.Id == builderId).ToListAsync());
            await cleaner.SaveChangesAsync();
        }
    }
}
