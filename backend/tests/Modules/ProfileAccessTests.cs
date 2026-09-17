using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.Api.Profiles.Infrastructure.Cloudinary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IoBuild.Modules.Tests;

// Convergent Testing Tier A: PROFILES.MANAGE ownership on every endpoint plus
// the photo compare-and-swap workflow with a fake uploader (never Cloudinary).
public sealed class ProfileAccessTests
{
    [Fact]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task CREATE_OWN_succeeds_but_CREATE_FOR_OTHER_is_forbidden()
    {
        await using var factory = new ProfileApiFactory();
        using var client = factory.CreateClient();
        var me = Token(21, "me21@example.test", "Builder");

        using var own = await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me,
            "{\"userId\":21,\"name\":\"Me\",\"username\":\"me21\"}");
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);

        using var foreign = await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me,
            "{\"userId\":22,\"name\":\"Other\",\"username\":\"other22\"}");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task READ_is_scoped_to_self_without_oracle()
    {
        await using var factory = new ProfileApiFactory();
        using var client = factory.CreateClient();
        var me23 = Token(23, "me23@example.test", "Owner");
        var me24 = Token(24, "me24@example.test", "Owner");
        await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me23, "{\"userId\":23,\"name\":\"A\",\"username\":\"a23\"}");
        await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me24, "{\"userId\":24,\"name\":\"B\",\"username\":\"b24\"}");

        var list = await (await SendAsync(client, HttpMethod.Get, "/api/v1/profiles", me23)).Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotEmpty(list!);
        Assert.All(list!, p => Assert.Equal(23, p.GetProperty("userId").GetInt32()));

        var mine = list!.First();
        var mineId = mine.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, $"/api/v1/profiles/{mineId}", me23)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"/api/v1/profiles/{mineId}", me24)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, "/api/v1/profiles?userId=24", me23)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/profiles")).StatusCode);
    }

    [Fact]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task UPDATE_OWN_applies_partial_semantics_but_OTHER_is_not_found()
    {
        await using var factory = new ProfileApiFactory();
        using var client = factory.CreateClient();
        var me25 = Token(25, "me25@example.test", "Builder");
        var other = Token(26, "other26@example.test", "Builder");
        await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me25,
            "{\"userId\":25,\"name\":\"Original\",\"username\":\"orig25\",\"phoneNumber\":\"+51000000001\"}");
        var mineId = (await (await SendAsync(client, HttpMethod.Get, "/api/v1/profiles", me25)).Content.ReadFromJsonAsync<List<JsonElement>>())!
            .First().GetProperty("id").GetInt32();

        using var updated = await SendAsync(client, HttpMethod.Put, $"/api/v1/profiles/{mineId}", me25,
            "{\"userId\":25,\"name\":\"\",\"username\":\"new25\",\"phoneNumber\":null,\"address\":\"Av New 1\",\"age\":40}");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Original", body.GetProperty("name").GetString());
        Assert.Equal("new25", body.GetProperty("username").GetString());
        Assert.Equal("Av New 1", body.GetProperty("address").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Put, $"/api/v1/profiles/{mineId}", other,
            "{\"userId\":25,\"name\":\"Hacked\",\"username\":\"hacked\"}")).StatusCode);
    }

    [Fact]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task PHOTO_replace_needs_ownership_current_reference_and_working_upload()
    {
        await using var factory = new ProfileApiFactory(upload: _ => Task.FromResult<string?>("https://cloud.test/photo1"));
        using var client = factory.CreateClient();
        var me27 = Token(27, "me27@example.test", "Owner");
        var intruder = Token(28, "intruder28@example.test", "Owner");
        await SendAsync(client, HttpMethod.Post, "/api/v1/profiles", me27, "{\"userId\":27,\"name\":\"P\",\"username\":\"p27\"}");

        // Foreign user cannot touch the photo at all.
        using var forbidden = await SendAsync(client, new HttpMethod("PATCH"), "/api/v1/profiles/27/photo", intruder,
            "{\"expectedReference\":\"\",\"content\":\"bytes\"}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Fresh profile bootstraps with an empty expected reference.
        using var first = await SendAsync(client, new HttpMethod("PATCH"), "/api/v1/profiles/27/photo", me27,
            "{\"expectedReference\":\"\",\"content\":\"bytes\"}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // A stale reference now conflicts instead of overwriting.
        using var stale = await SendAsync(client, new HttpMethod("PATCH"), "/api/v1/profiles/27/photo", me27,
            "{\"expectedReference\":\"stale-ref\",\"content\":\"bytes\"}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var after = await (await SendAsync(client, HttpMethod.Get, "/api/v1/profiles", me27)).Content.ReadFromJsonAsync<List<JsonElement>>();
        var photo = after!.First().GetProperty("photoUrl").GetString();
        Assert.Equal("https://cloud.test/photo1", photo);
    }

    [Fact]
    [Trait("Flow", "PROFILES.MANAGE")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task PHOTO_failed_upload_aborts_without_touching_the_stored_photo()
    {
        await using var db = CreateDb();
        db.Profiles.Add(new IoBuild.Api.Profiles.Domain.Model.Aggregates.Profile { UserId = 29, Name = "Q", Username = "q29", PhotoReference = "ref-keep", PhotoUrl = "https://cloud.test/keep" });
        await db.SaveChangesAsync();

        var workflow = new IoBuild.Api.Profiles.Application.Internal.CommandServices.ProfilePhotoWorkflow(db, new BrokenUploader());
        Assert.False(await workflow.ReplaceAsync(29, "ref-keep", "new-bytes"));

        var untouched = await db.Profiles.SingleAsync(p => p.UserId == 29);
        Assert.Equal("ref-keep", untouched.PhotoReference);
        Assert.Equal("https://cloud.test/keep", untouched.PhotoUrl);
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

    private static IoBuildDbContext CreateDb() => new(
        new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FakeUploader(Func<string, Task<string?>> upload) : ICloudinaryUploader
    {
        public Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default) => upload(content);
    }

    private sealed class BrokenUploader : ICloudinaryUploader
    {
        public Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class ProfileApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        private readonly Func<string, Task<string?>>? upload;
        public ProfileApiFactory(Func<string, Task<string?>>? upload = null) => this.upload = upload;
        private readonly string databaseName = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IoBuildDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IoBuildDbContext>>();
            services.AddDbContext<IoBuildDbContext>(options => options.UseInMemoryDatabase(databaseName));
            var readiness = new IoBuild.Api.Readiness.MigrationReadiness();
            readiness.RecordMigrationSuccess();
            services.AddSingleton(readiness);
            if (upload is not null)
            {
                services.RemoveAll<ICloudinaryUploader>();
                services.AddSingleton<ICloudinaryUploader>(new FakeUploader(upload));
            }
            services.RemoveAll<IHostedService>();
        }).ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mqtt:Enabled"] = "false"
        }));
    }
}
