using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests.IAM.AccountLifecycle.Api;

[Trait("Context", "IAM")]
public sealed class IamApiContractTests
{
    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task Registration_sign_in_and_durable_logout_preserve_the_characterized_contract()
    {
        await using var factory = new IamApiFactory();
        using var client = factory.CreateClient();
        var registration = await client.PostAsync("/api/v1/users", Json("{\"email\":\"api@example.test\",\"password\":\"secret123\",\"role\":\"Builder\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Created, registration.StatusCode);
        var session = await client.PostAsync("/api/v1/sessions", Json("{\"email\":\"api@example.test\",\"password\":\"secret123\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Created, session.StatusCode);
        var token = (await session.Content.ReadFromJsonAsync<AuthenticatedUser>())!.Token;
        using var logout = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/sessions/current");
        logout.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users")).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/authentication/sign-up")]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task Owner_registration_routes_reject_an_email_without_an_assigned_unit(string route)
    {
        await using var factory = new IamApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(route, Json("{\"email\":\"unassigned-api@example.test\",\"password\":\"secret123\",\"role\":\"Owner\"}"));

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("owner_unit_assignment_required", body.GetProperty("code").GetString());
        var login = await client.PostAsync("/api/v1/sessions", Json("{\"email\":\"unassigned-api@example.test\",\"password\":\"secret123\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.AUTHORIZED_ACCESS")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task User_directory_is_admin_only()
    {
        await using var factory = new IamApiFactory();
        using var client = factory.CreateClient();
        const string adminEmail = "iam.admin@example.test";
        const string adminPassword = "admin-secret-123";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            db.IamUsers.Add(new IamUser
            {
                Email = adminEmail,
                PasswordHash = new PasswordHasher().Hash(adminPassword),
                Role = "Admin"
            });
            await db.SaveChangesAsync();
        }

        const string builderEmail = "ordinary-builder@example.test";
        using var registration = await client.PostAsync("/api/v1/users", Json(
            "{\"email\":\"ordinary-builder@example.test\",\"password\":\"builder-secret-123\",\"role\":\"Builder\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Created, registration.StatusCode);

        using var builderSession = await client.PostAsync("/api/v1/sessions", Json(
            "{\"email\":\"ordinary-builder@example.test\",\"password\":\"builder-secret-123\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Created, builderSession.StatusCode);
        var builderToken = (await builderSession.Content.ReadFromJsonAsync<AuthenticatedUser>())!.Token;
        using var forbiddenRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        forbiddenRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", builderToken);
        using var forbidden = await client.SendAsync(forbiddenRequest);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var adminSession = await client.PostAsync("/api/v1/sessions", Json(
            "{\"email\":\"iam.admin@example.test\",\"password\":\"admin-secret-123\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Created, adminSession.StatusCode);
        var adminToken = (await adminSession.Content.ReadFromJsonAsync<AuthenticatedUser>())!.Token;
        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        adminRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        using var allowed = await client.SendAsync(adminRequest);

        Assert.Equal(System.Net.HttpStatusCode.OK, allowed.StatusCode);
        var directory = await allowed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, directory.GetArrayLength());
        Assert.Contains(adminEmail, directory.GetRawText(), StringComparison.Ordinal);
        Assert.Contains(builderEmail, directory.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", directory.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task Registration_rejects_malformed_email_and_password_under_eight_without_creating_accounts()
    {
        await using var factory = new IamApiFactory();
        using var client = factory.CreateClient();

        using var invalidEmail = await client.PostAsync("/api/v1/users", Json(
            "{\"email\":\"invalid-email-format\",\"password\":\"builder-secret-123\",\"role\":\"Builder\"}"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalidEmail.StatusCode);

        using var shortPassword = await client.PostAsync("/api/v1/users", Json(
            "{\"email\":\"valid-format@example.test\",\"password\":\"1234567\",\"role\":\"Builder\"}"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, shortPassword.StatusCode);

        using var invalidEmailLogin = await client.PostAsync("/api/v1/sessions", Json(
            "{\"email\":\"invalid-email-format\",\"password\":\"builder-secret-123\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, invalidEmailLogin.StatusCode);

        using var shortPasswordLogin = await client.PostAsync("/api/v1/sessions", Json(
            "{\"email\":\"valid-format@example.test\",\"password\":\"1234567\"}"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, shortPasswordLogin.StatusCode);
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "A")]
    public async Task Public_invitation_lookup_returns_assignment_only_not_owner_PII()
    {
        await using var factory = new IamApiFactory();
        using var client = factory.CreateClient();
        const string ownerEmail = "private-owner@example.test";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            db.Units.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Unit(1, "801", null, 8, "801") { Id = 801 });
            db.Clients.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Client(
                "Private Owner Name", "Private Tower", "Pending", 10, 1, ownerEmail, "+51999999999", "Private Address", 801, "801"));
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/authentication/invitation?email={Uri.EscapeDataString(ownerEmail)}");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var invitation = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(invitation.GetProperty("assigned").GetBoolean());
        Assert.False(invitation.GetProperty("alreadyRegistered").GetBoolean());
        foreach (var privateProperty in new[] { "fullName", "phoneNumber", "address", "unitNumber", "projectName", "unitId" })
            Assert.False(invitation.TryGetProperty(privateProperty, out _), $"Invitation must not expose {privateProperty}.");
        Assert.DoesNotContain("Private Owner Name", invitation.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Private Address", invitation.GetRawText(), StringComparison.Ordinal);
    }

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");

    private sealed class IamApiFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        private readonly string databaseName = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<Microsoft.EntityFrameworkCore.DbContextOptions<IoBuildDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<IoBuildDbContext>>();
            services.AddDbContext<IoBuildDbContext>(options => options.UseInMemoryDatabase(databaseName));
            var readiness = new IoBuild.Api.Readiness.MigrationReadiness(); readiness.RecordMigrationSuccess();
            services.AddSingleton(readiness);
            services.RemoveAll<IHostedService>();
        }).ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mqtt:Enabled"] = "false"
        }));
    }
}
