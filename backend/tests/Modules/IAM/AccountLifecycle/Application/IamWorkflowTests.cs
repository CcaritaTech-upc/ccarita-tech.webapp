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

[Trait("Context", "IAM")]
public sealed partial class IamWorkflowTests
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
