using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.IAM.Infrastructure.Tokens;
using IoBuild.Api.Persistence;
using IoBuild.Api.Workflows;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests;

public sealed class IamWorkflowTests
{
    [Fact]
    [Trait("Category", "IAM")]
    public async Task Registration_workflow_implements_the_transactional_workflow_boundary()
    {
        await using var db = CreateDb();
        Assert.IsAssignableFrom<IWorkflow<RegisterUser, int>>(
            new RegisterUserWorkflow(db, new PasswordHasher(), new IntegrationDispatchQueue(db), new WorkflowExecutor(db)));
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task Registration_is_idempotent_and_creates_a_durable_dispatch_record()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);

        await service.RegisterAsync(new RegisterUser("ada@example.test", "secret123", "Builder"));
        await service.RegisterAsync(new RegisterUser("ada@example.test", "secret123", "Builder"));

        Assert.Single(await db.IamUsers.ToListAsync());
        Assert.Single(await db.IntegrationDispatches.ToListAsync());
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task Owner_registration_without_an_assigned_unit_is_rejected_without_side_effects()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);

        await Assert.ThrowsAsync<OwnerUnitAssignmentRequiredException>(() =>
            service.RegisterAsync(new RegisterUser("unassigned@example.test", "secret123", "Owner")));

        Assert.Empty(await db.IamUsers.ToListAsync());
        Assert.Empty(await db.IntegrationDispatches.ToListAsync());
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task Owner_registration_requires_a_unit_not_only_a_client_record()
    {
        await using var db = CreateDb();
        db.Clients.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Client(
            "Unassigned Client", "Project", "Pending", 1, 1, "client-only@example.test", "999888777", "Somewhere", null, null));
        await db.SaveChangesAsync();
        var service = CreateIamService(db);

        await Assert.ThrowsAsync<OwnerUnitAssignmentRequiredException>(() =>
            service.RegisterAsync(new RegisterUser("client-only@example.test", "secret123", "Owner")));

        Assert.Empty(await db.IamUsers.ToListAsync());
    }

    [Fact]
    [Trait("Category", "IAM")]
    public async Task Registration_auto_links_assigned_units_and_projections()
    {
        await using var db = CreateDb();
        db.Projects.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Project { Id = 1, BuilderId = 10, Name = "Sunset Heights" });
        var unit = new IoBuild.Api.Publishing.Domain.Model.Aggregates.Unit(1, "204", null, 2, "204") { Id = 15 };
        unit.AssignOwner("owner.auto@example.com", null);
        db.Units.Add(unit);

        var device = new IoBuild.Api.Devices.Domain.Model.Aggregates.Device
        {
            Id = 101,
            Name = "Living Room AC",
            Type = "AirConditioner",
            ProjectId = 1,
            UnitId = 15,
            OwnerId = 0,
            Status = "online"
        };
        db.Devices.Add(device);

        db.UnitProjections.Add(new IoBuild.Api.Analytics.Domain.Model.Aggregates.UnitProjection
        {
            UnitId = unit.Id,
            ProjectId = 1,
            BuilderUserId = 10,
            Status = "occupied",
            OwnerEmail = "owner.auto@example.com"
        });
        await db.SaveChangesAsync();

        var service = CreateIamService(db);
        await service.RegisterAsync(new RegisterUser("owner.auto@example.com", "secret123", "Owner"));

        var user = await db.IamUsers.SingleAsync(u => u.Email == "owner.auto@example.com");
        var updatedUnit = await db.Units.SingleAsync(u => u.Id == unit.Id);
        Assert.Equal(user.Id, updatedUnit.OwnerId);

        var ownerProj = await db.UnitOwnerProjections.SingleAsync(p => p.UnitId == unit.Id);
        Assert.Equal(user.Id, ownerProj.OwnerUserId);

        var unitProj = await db.UnitProjections.SingleAsync(p => p.UnitId == unit.Id);
        Assert.Equal(user.Id, unitProj.OwnerUserId);

        var updatedDevice = await db.Devices.SingleAsync(d => d.Id == 101);
        Assert.Equal(user.Id, updatedDevice.OwnerId);

        var devProj = await db.DeviceProjections.SingleAsync(dp => dp.DeviceId == 101);
        Assert.Equal(user.Id, devProj.OwnerUserId);

        var projProj = await db.ProjectProjections.SingleAsync(p => p.ProjectId == 1);
        Assert.Equal("Sunset Heights", projProj.Name);
    }

    [Fact]
    [Trait("Category", "IAM")]
    public async Task Registration_auto_links_when_assigned_via_client_and_creates_missing_unit_projection()
    {
        await using var db = CreateDb();
        db.Projects.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Project { Id = 2, BuilderId = 10, Name = "Ocean Tower" });
        var unit = new IoBuild.Api.Publishing.Domain.Model.Aggregates.Unit(2, "301", null, 3, "301") { Id = 20 };
        db.Units.Add(unit);

        db.Clients.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Client(
            "Carlos Lopez", "Ocean Tower", "Pending", 10, 2, "carlos@example.com", "999888777", "Av. Mar 123", 20, "301"));

        var light = new IoBuild.Api.Devices.Domain.Model.Aggregates.Device
        {
            Id = 202,
            Name = "Hallway Light",
            Type = "SmartLight",
            ProjectId = 2,
            UnitId = 20,
            OwnerId = 0,
            Status = "online"
        };
        db.Devices.Add(light);
        await db.SaveChangesAsync();

        var service = CreateIamService(db);
        await service.RegisterAsync(new RegisterUser("carlos@example.com", "secure456", "Owner"));

        var user = await db.IamUsers.SingleAsync(u => u.Email == "carlos@example.com");
        var updatedUnit = await db.Units.SingleAsync(u => u.Id == 20);
        Assert.Equal(user.Id, updatedUnit.OwnerId);

        var unitProj = await db.UnitProjections.SingleAsync(p => p.UnitId == 20);
        Assert.Equal(user.Id, unitProj.OwnerUserId);
        Assert.Equal("occupied", unitProj.Status);

        var updatedLight = await db.Devices.SingleAsync(d => d.Id == 202);
        Assert.Equal(user.Id, updatedLight.OwnerId);

        var devProj = await db.DeviceProjections.SingleAsync(dp => dp.DeviceId == 202);
        Assert.Equal(user.Id, devProj.OwnerUserId);
    }

    [Fact]
    [Trait("Category", "IAM")]
    public async Task Dispatch_leases_in_order_and_dead_letters_after_retry_limit()
    {
        await using var db = CreateDb();
        var queue = new IntegrationDispatchQueue(db);
        await queue.EnqueueAsync(new DispatchRequest("iam", "event", "key-1", 2, "two", "two"));
        await queue.EnqueueAsync(new DispatchRequest("iam", "event", "key-1", 1, "one", "one"));
        await db.SaveChangesAsync();

        var first = await queue.LeaseDueAsync("worker", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        Assert.Equal("one", first!.IdempotencyKey);
        await queue.FailAsync(first.Id, "worker", DateTimeOffset.UtcNow, retryable: true, maxAttempts: 1);

        var dead = await db.IntegrationDispatches.SingleAsync(row => row.IdempotencyKey == "one");
        Assert.Equal(DispatchStatus.DeadLetter, dead.Status);
    }

    [Fact]
    [Trait("Category", "IAM")]
    public async Task Expired_leases_are_recovered_and_dead_letters_are_audit_replayed()
    {
        await using var db = CreateDb();
        var queue = new IntegrationDispatchQueue(db);
        await queue.EnqueueAsync(new DispatchRequest("iam", "event", "recovery-key", 1, "payload", "recovery"));
        await db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        var leased = await queue.LeaseDueAsync("lost-worker", now, TimeSpan.FromSeconds(1));
        var recovered = await queue.LeaseDueAsync("new-worker", now.AddSeconds(2), TimeSpan.FromMinutes(1));
        Assert.Equal(leased!.Id, recovered!.Id);
        await queue.FailAsync(recovered.Id, "new-worker", now.AddSeconds(2), retryable: false, maxAttempts: 3);
        await queue.ReplayAsync(recovered.Id, now.AddSeconds(3));
        var replayed = await db.IntegrationDispatches.SingleAsync(row => row.Id == recovered.Id);
        Assert.Equal(DispatchStatus.Pending, replayed.Status);
        Assert.Equal("audited replay", replayed.LastError);
        var replayLease = await queue.LeaseDueAsync("finisher", now.AddSeconds(3), TimeSpan.FromMinutes(1));
        await queue.CompleteAsync(replayLease!.Id, "finisher", now.AddSeconds(3));
        Assert.Equal(DispatchStatus.Completed, (await db.IntegrationDispatches.SingleAsync(row => row.Id == replayLease.Id)).Status);
    }

    [Fact]
    [Trait("Category", "IAM")]
    public async Task Expired_lease_is_recovered_before_the_next_worker_selects_due_work()
    {
        await using var db = CreateDb();
        var queue = new IntegrationDispatchQueue(db);
        await queue.EnqueueAsync(new DispatchRequest("iam", "event", "recoverable-key", 1, "payload", "recoverable"));
        await db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;

        var firstLease = await queue.LeaseDueAsync("lost-worker", now, TimeSpan.FromSeconds(1));
        var recoveredLease = await queue.LeaseDueAsync("next-worker", now.AddSeconds(2), TimeSpan.FromMinutes(1));

        Assert.NotNull(firstLease);
        Assert.NotNull(recoveredLease);
        Assert.Equal(firstLease!.Id, recoveredLease!.Id);
        Assert.Equal("next-worker", recoveredLease.LeaseOwner);
    }

    [Fact]
    [Trait("Category", "IAM")]
    public async Task Replay_resets_attempts_so_a_dead_letter_can_receive_a_fresh_retry_budget()
    {
        await using var db = CreateDb();
        var queue = new IntegrationDispatchQueue(db);
        await queue.EnqueueAsync(new DispatchRequest("iam", "event", "replay-key", 1, "payload", "replay"));
        await db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        var lease = await queue.LeaseDueAsync("worker", now, TimeSpan.FromMinutes(1));
        await queue.FailAsync(lease!.Id, "worker", now, retryable: false, maxAttempts: 3);

        await queue.ReplayAsync(lease.Id, now.AddSeconds(1));

        var replayed = await db.IntegrationDispatches.SingleAsync(row => row.Id == lease.Id);
        Assert.Equal(0, replayed.Attempts);
        Assert.Equal(DispatchStatus.Pending, replayed.Status);
    }

    [Fact]
    [Trait("Category", "IAM")]
    public async Task MySql_dispatch_rows_survive_storage_and_recover_an_expired_lease()
    {
        var connectionString = Environment.GetEnvironmentVariable("IOBUILD_TEST_MYSQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var db = new IoBuildDbContext(new DbContextOptionsBuilder<IoBuildDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)).Options);
        // Lease selection is global over the shared outbox table: with foreign
        // rows present it would lease someone else's row (pilot rule: avoid
        // shared data). A fresh database (CI compose up) runs this proof.
        if (await db.IntegrationDispatches.AnyAsync()) return;

        var queue = new IntegrationDispatchQueue(db);
        var key = $"mysql-recovery-{Guid.NewGuid():N}";
        try
        {
            await queue.EnqueueAsync(new DispatchRequest("iam", "event", key, 1, "payload", key));
            await db.SaveChangesAsync();
            var now = DateTimeOffset.UtcNow;

            var firstLease = await queue.LeaseDueAsync("lost-mysql-worker", now, TimeSpan.FromSeconds(1));
            var recoveredLease = await queue.LeaseDueAsync("next-mysql-worker", now.AddSeconds(2), TimeSpan.FromMinutes(1));

            Assert.NotNull(firstLease);
            Assert.NotNull(recoveredLease);
            Assert.Equal(firstLease!.Id, recoveredLease!.Id);
            Assert.Equal("next-mysql-worker", recoveredLease.LeaseOwner);
        }
        finally
        {
            var leftovers = await db.IntegrationDispatches.Where(row => row.IdempotencyKey == key).ToListAsync();
            if (leftovers.Count > 0)
            {
                db.IntegrationDispatches.RemoveRange(leftovers);
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    [Trait("Category", "IAM")]
    [Trait("Flow", "IAM.LOGOUT")]
    [Trait("Layer", "Application")]
    [Trait("Risk", "A")]
    public async Task Revoked_tokens_are_rejected_from_the_durable_store()
    {
        await using var db = CreateDb();
        var service = CreateIamService(db);
        await service.RegisterAsync(new RegisterUser("lin@example.test", "secret123", "Builder"));
        var session = await service.SignInAsync(new SignIn("lin@example.test", "secret123"));

        await service.RevokeAsync(session.Token);

        Assert.True(await service.IsRevokedAsync(session.Token));
    }

    private static IoBuildDbContext CreateDb() => new(
        new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IamService CreateIamService(IoBuildDbContext db)
    {
        var passwordHasher = new PasswordHasher();
        var queue = new IntegrationDispatchQueue(db);
        var workflow = new RegisterUserWorkflow(db, passwordHasher, queue, new WorkflowExecutor(db));
        return new IamService(db, passwordHasher, new JwtTokenIssuer("a-test-secret-that-is-long-enough-for-hmac"), workflow);
    }
}

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
