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

// Convergent Testing G3: ANALYTICS tiers. Insights determinism, production-
// faithful error bodies, and read-path fuzz partitions.
public sealed class AnalyticsTiersTests
{
    [Fact]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task INSIGHTS_are_deterministic_regardless_of_inputs()
    {
        await using var factory = new AnalyticsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, AnalyticsDashboardTests.SeedTenantAsync);
        var me = AnalyticsDashboardTests.Token(95, "builder95@example.test", "Builder");

        foreach (var query in new[]
        {
            "/api/v1/analytics/insights?projectId=95",
            "/api/v1/analytics/insights?projectId=95&metric=energy",
            "/api/v1/analytics/insights?projectId=95&metric=garbage-metric",
            "/api/v1/analytics/insights?projectId=95&metric=temperature&startDate=2026-09-20T00:00:00Z&endDate=2026-09-10T00:00:00Z",
            "/api/v1/analytics/insights?projectId=95&metric=temperature&startDate=2020-01-01T00:00:00Z&endDate=2026-09-17T00:00:00Z",
        })
        {
            using var response = await SendAsync(client, HttpMethod.Get, query, me);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("[]", await response.Content.ReadAsStringAsync());
        }

        using var badDate = await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/insights?projectId=95&startDate=not-a-date", me);
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "B")]
    public async Task ERROR_CONTRACT_failure_paths_never_leak_internals()
    {
        // Production environment on purpose: Development prints stacks by design.
        await using var factory = new ProductionAnalyticsApiFactory();
        using var client = factory.CreateClient();
        var me = AnalyticsDashboardTests.Token(95, "builder95@example.test", "Builder");
        var cases = new List<HttpResponseMessage>
        {
            await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/builders/999999/metrics", me),
            await SendAsync(client, HttpMethod.Get, "/api/v1/analytics/insights?projectId=999999", me),
        };
        cases.Add(await client.GetAsync("/api/v1/analytics/builders/95/metrics"));
        foreach (var response in cases)
        {
            using (response)
            {
                Assert.True(
                    response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                    $"Failure path returned {response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
                Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("IoBuild.Api.", body, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.VIEW")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task READ_FUZZ_partitions_never_server_error()
    {
        await using var factory = new AnalyticsApiFactory();
        using var client = factory.CreateClient();
        var me = AnalyticsDashboardTests.Token(95, "builder95@example.test", "Builder");
        var paths = new[]
        {
            "/api/v1/analytics/builders/0/metrics",
            "/api/v1/analytics/builders/-5/metrics",
            "/api/v1/analytics/owners/0/energy?minutes=5",
            "/api/v1/analytics/builders/95/energy?minutes=-5",
            "/api/v1/analytics/insights",
            "/api/v1/analytics/insights?projectId=abc",
        };
        foreach (var path in paths)
        {
            using var response = await SendAsync(client, HttpMethod.Get, path, me);
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
                $"Read fuzz {path} returned {response.StatusCode}");
        }
    }

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

    private class AnalyticsApiFactory : WebApplicationFactory<Program>
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

    private sealed class ProductionAnalyticsApiFactory : AnalyticsApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, Environments.Production);
            base.ConfigureWebHost(builder);
        }
    }
}
