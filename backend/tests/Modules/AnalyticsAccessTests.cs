using System.Net;
using System.Net.Http.Headers;
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

// Convergent Testing Tier A: ANALYTICS.VIEW serves only the caller on every
// route; project insights require owning the project or occupying a unit.
public sealed class AnalyticsAccessTests
{
    [Fact]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task METRICS_AND_ENERGY_require_self()
    {
        await using var factory = new AnalyticsApiFactory();
        using var client = factory.CreateClient();
        var me = Token(91, "me91@example.test", "Builder");
        var other = Token(92, "other92@example.test", "Builder");

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/builders/92/metrics", me)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/owners/92/metrics", me)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/builders/92/energy?minutes=5", me)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/analytics/builders/91/metrics")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/analytics/owners/91/metrics")).StatusCode);

        using var ownEnergy = await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/builders/91/energy?minutes=0", me);
        Assert.Equal(HttpStatusCode.OK, ownEnergy.StatusCode);
        _ = other;
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task INSIGHTS_require_project_relationship()
    {
        await using var factory = new AnalyticsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, async db =>
        {
            db.Projects.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Project { Id = 91, BuilderId = 91, Name = "Insight Towers" });
            var unit = new IoBuild.Api.Publishing.Domain.Model.Aggregates.Unit(91, "911", null, 9, "911") { Id = 911 };
            db.Units.Add(unit);
            db.UnitOwnerProjections.Add(new IoBuild.Api.Devices.Domain.Model.Entities.UnitOwnerProjection { UnitId = 911, OwnerUserId = 93, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        var me = Token(91, "me91@example.test", "Builder");
        var stranger = Token(94, "stranger94@example.test", "Builder");
        var occupant = Token(93, "occ93@example.test", "Owner");

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/insights?projectId=91", me)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/insights?projectId=91", occupant)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/insights?projectId=91", stranger)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/insights?projectId=999999", me)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/analytics/insights?projectId=91")).StatusCode);
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
