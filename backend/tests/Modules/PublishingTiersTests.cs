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

// Convergent Testing G3: PUBLISHING tiers. Validation matrix, production-faithful
// error bodies, and deterministic fuzz partitions at the API boundary.
public sealed class PublishingTiersTests
{
    [Fact]
    [Trait("Flow", "PUBLISHING.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task STRUCTURE_VALIDATION_rejects_bad_shapes_before_provisioning()
    {
        await using var factory = new PublishingApiFactory();
        using var client = factory.CreateClient();
        var me = Token(81, "me81@example.test", "Builder");
        var projectId = await CreateProjectAsync(client, me, 81, "Validation Towers");

        using var zeroFloors = await SendAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", me,
            "{\"floors\":0,\"unitsPerFloor\":1,\"floorNumbers\":null}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, zeroFloors.StatusCode);

        using var outOfRange = await SendAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", me,
            "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":[7]}");
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);

        using var missing = await SendAsync(client, HttpMethod.Post, "/api/v1/projects/999999/structure", me,
            "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":null}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var nonBuilder = await SendAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure",
            Token(82, "owner82@example.test", "Owner"),
            "{\"floors\":1,\"unitsPerFloor\":1,\"floorNumbers\":null}");
        Assert.Equal(HttpStatusCode.Forbidden, nonBuilder.StatusCode);
    }

    [Fact]
    [Trait("Flow", "PUBLISHING.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "B")]
    public async Task ERROR_CONTRACT_failure_paths_never_leak_internals()
    {
        // Production environment on purpose: Development prints stacks by design.
        await using var factory = new ProductionPublishingApiFactory();
        using var client = factory.CreateClient();
        var me = Token(83, "me83@example.test", "Builder");
        var cases = new List<HttpResponseMessage>
        {
            await SendAsync(client, HttpMethod.Get, "/api/v1/projects/999999", me),
            await SendAsync(client, HttpMethod.Get, "/api/v1/units/999999", me),
            await SendAsync(client, HttpMethod.Get, "/api/v1/clients/999999", me),
        };
        using (var malformed = new StringContent("{not-json", Encoding.UTF8, "application/json"))
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects") { Content = malformed };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", me);
            cases.Add(await client.SendAsync(request));
        }
        cases.Add(await client.GetAsync("/api/v1/projects/1"));
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
    [Trait("Flow", "PUBLISHING.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task PAYLOAD_FUZZ_partitions_never_server_error()
    {
        await using var factory = new PublishingApiFactory();
        using var client = factory.CreateClient();
        var me = Token(84, "me84@example.test", "Builder");
        var projectId = await CreateProjectAsync(client, me, 84, "Fuzz Plaza");
        var payloads = new (string Path, string Body)[]
        {
            ("/api/v1/units", $"{{\"projectId\":{projectId},\"unitNumber\":\"\",\"ownerId\":null,\"floor\":-2,\"roomNumber\":\"\"}}"),
            ("/api/v1/units", $"{{\"projectId\":-9,\"unitNumber\":\"F1\",\"ownerId\":null,\"floor\":1,\"roomNumber\":\"F1\"}}"),
            ("/api/v1/units", $"{{\"projectId\":{projectId},\"unitNumber\":\"{new string('U', 500)}\",\"ownerId\":null,\"floor\":1,\"roomNumber\":\"X\"}}"),
            ("/api/v1/clients", $"{{\"fullName\":\"\",\"projectName\":\"\",\"accountStatement\":\"\",\"builderId\":84,\"projectId\":{projectId}}}"),
            ($"/api/v1/units/{projectId}/assign-owner", "{\"ownerEmail\":null,\"ownerId\":null}"),
        };
        foreach (var (path, body) in payloads)
        {
            var method = path.EndsWith("assign-owner") ? new HttpMethod("PATCH") : HttpMethod.Post;
            using var response = await SendAsync(client, method, path, me, body);
            Assert.True(
                response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity,
                $"Fuzz partition {path} returned {response.StatusCode}");
        }
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

    private class PublishingApiFactory : WebApplicationFactory<Program>
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

    private sealed class ProductionPublishingApiFactory : PublishingApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, Environments.Production);
            base.ConfigureWebHost(builder);
        }
    }
}
