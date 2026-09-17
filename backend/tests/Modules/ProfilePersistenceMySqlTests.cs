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

// Convergent Testing G1: PROFILES.MANAGE persistence guarantees on the
// production engine. Opt-in: skips with success without
// IOBUILD_TEST_MYSQL_CONNECTION. Uses dedicated probe user ids and deletes
// only the rows it created.
public sealed class ProfilePersistenceMySqlTests
{
    private const int ProbeUserId = 91831;

    [Fact]
    [Trait("Category", "Profiles")]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Create_read_update_roundtrip_on_mysql()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var factory = new MySqlProfileApiFactory(connectionString);
        using var client = factory.CreateClient();
        var me = Token(ProbeUserId);
        try
        {
            using var created = await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me,
                $"{{\"userId\":{ProbeUserId},\"name\":\"Probe\",\"username\":\"probe91831\",\"phoneNumber\":\"+51000000002\"}}");
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var list = await (await SendAsync(client, HttpMethod.Get, "/api/v1/profiles", me)).Content.ReadFromJsonAsync<List<JsonElement>>();
            var mine = Assert.Single(list!);
            Assert.Equal("Probe", mine.GetProperty("name").GetString());

            var id = mine.GetProperty("id").GetInt32();
            using var updated = await SendAsync(client, HttpMethod.Put, $"/api/v1/profiles/{id}", me,
                $"{{\"userId\":{ProbeUserId},\"name\":\"Probe II\",\"username\":\"probe91831\",\"address\":\"Av Probe 1\"}}");
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            var durable = await reader.Profiles.SingleAsync(p => p.UserId == ProbeUserId);
            Assert.Equal("Probe II", durable.Name);
            Assert.Equal("Av Probe 1", durable.Address);
        }
        finally
        {
            await using var cleaner = MySqlFixture.CreateIsolatedContext(connectionString);
            var rows = await cleaner.Profiles.Where(p => p.UserId == ProbeUserId).ToListAsync();
            if (rows.Count > 0)
            {
                cleaner.Profiles.RemoveRange(rows);
                await cleaner.SaveChangesAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", "Profiles")]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Duplicate_create_for_same_user_conflicts_without_server_error()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        const int dupeUserId = 91832;
        await using var factory = new MySqlProfileApiFactory(connectionString);
        using var client = factory.CreateClient();
        var me = Token(dupeUserId);
        try
        {
            using var first = await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me,
                $"{{\"userId\":{dupeUserId},\"name\":\"Dupe\",\"username\":\"dupe91832\"}}");
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            using var second = await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me,
                $"{{\"userId\":{dupeUserId},\"name\":\"Dupe\",\"username\":\"dupe91832\"}}");
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            Assert.Single(await reader.Profiles.Where(p => p.UserId == dupeUserId).ToListAsync());
        }
        finally
        {
            await using var cleaner = MySqlFixture.CreateIsolatedContext(connectionString);
            var rows = await cleaner.Profiles.Where(p => p.UserId == dupeUserId || p.UserId == ProbeUserId).ToListAsync();
            if (rows.Count > 0)
            {
                cleaner.Profiles.RemoveRange(rows);
                await cleaner.SaveChangesAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", "Profiles")]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "A")]
    [Trait("Dependency", "MySql")]
    public async Task Photo_swap_is_durable_on_mysql()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        const int photoUserId = 91833;
        await using var db = MySqlFixture.CreateIsolatedContext(connectionString);
        try
        {
            db.Profiles.Add(new IoBuild.Api.Profiles.Domain.Model.Aggregates.Profile { UserId = photoUserId, Name = "Ph", Username = "ph91833" });
            await db.SaveChangesAsync();

            var workflow = new IoBuild.Api.Profiles.Application.Internal.CommandServices.ProfilePhotoWorkflow(
                db, new FixedUploader("https://cloud.test/ph1"));
            Assert.True(await workflow.ReplaceAsync(photoUserId, "", "bytes-1"));

            await using var reader = MySqlFixture.CreateIsolatedContext(connectionString);
            var durable = await reader.Profiles.SingleAsync(p => p.UserId == photoUserId);
            Assert.Equal("https://cloud.test/ph1", durable.PhotoUrl);
            Assert.StartsWith("sha256:", durable.PhotoReference);
        }
        finally
        {
            var rows = await db.Profiles.Where(p => p.UserId == photoUserId).ToListAsync();
            if (rows.Count > 0)
            {
                db.Profiles.RemoveRange(rows);
                await db.SaveChangesAsync();
            }
            await db.DisposeAsync();
        }
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

    private sealed class FixedUploader(string reference) : IoBuild.Api.Profiles.Infrastructure.Cloudinary.ICloudinaryUploader
    {
        public Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult<string?>(reference);
    }

    private sealed class MySqlProfileApiFactory(string connectionString) : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
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
