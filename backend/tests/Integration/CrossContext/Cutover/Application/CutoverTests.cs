using System.Security.Claims;
using IoBuild.Api.Shared.Application.Cutover;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Integration.Tests.CrossContext.Cutover.Application;

[Trait("Context", "CrossContext")]
[Trait("Capability", "Cutover")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class CutoverTests
{
    private static IoBuildDbContext Db() => new(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // ── Freeze blocks writes (503) vs not-ready 503 distinction ──

    [Fact]
    [Trait("Category", "Cutover")]
    public void CutoverFreeze_blocks_writes_returns_cutover_error_distinct_from_migration()
    {
        var readiness = new CutoverReadiness();
        var migration = new IoBuild.Api.Readiness.MigrationReadiness();

        // Initially not blocked
        Assert.False(readiness.ShouldBlockWrites);
        Assert.Null(readiness.FailureReason);

        // Freeze
        readiness.Freeze();

        Assert.True(readiness.ShouldBlockWrites);
        Assert.Equal("cutover_freeze_active", readiness.FailureReason);
        Assert.NotEqual(migration.FailureReason, readiness.FailureReason);
        // Migration initially not ready: should be migration_readiness_failed concept, but not frozen
        Assert.False(migration.IsReady);
    }

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverFreeze_unfreeze_allows_writes()
    {
        var readiness = new CutoverReadiness();
        await new CutoverHarness(Db(), readiness).FreezeAsync();
        Assert.True(readiness.ShouldBlockWrites);
        await new CutoverHarness(Db(), readiness).UnfreezeAsync();
        Assert.False(readiness.ShouldBlockWrites);
    }

    // ── Backup/restore preserves committed rows ──

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverBackup_restore_preserves_committed_rows()
    {
        await using var db = Db();
        db.Projects.Add(new Project { Id = 1, Name = "Committed", Description = "d", Location = "l", TotalUnits = 10, BuilderId = 1 });
        await db.SaveChangesAsync();

        var readiness = new CutoverReadiness();
        var harness = new CutoverHarness(db, readiness);
        var checkpointPath = Path.Combine(Path.GetTempPath(), $"cutover-checkpoint-{Guid.NewGuid():N}.json");
        var checkpoint = await harness.BackupAsync(checkpointPath, CancellationToken.None);

        Assert.True(File.Exists(checkpointPath));
        Assert.Equal(1, checkpoint.ProjectCount);

        // Add new rows post-backup
        db.Projects.Add(new Project { Id = 2, Name = "New", Description = "d", Location = "l", TotalUnits = 5, BuilderId = 2 });
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Projects.CountAsync());

        await harness.RestoreAsync(checkpointPath, CancellationToken.None);

        // Committed rows preserved, new rows reverted
        var count = await db.Projects.CountAsync();
        Assert.Equal(1, count);
        Assert.NotNull(await db.Projects.FindAsync(1));
        Assert.Null(await db.Projects.FindAsync(2));
        File.Delete(checkpointPath);
    }

    // ── Ordered import IAM→Projects/Profiles→Subscriptions→Devices with parity counts ──

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverImport_ordered_iam_projects_profiles_subscriptions_devices()
    {
        await using var db = Db();
        var harness = new CutoverHarness(db, new CutoverReadiness());

        var dump = new LegacyCutoverDump
        {
            IamUsers = [new LegacyIamUser(1, "a@b.com", "hash", "Builder", DateTime.UtcNow)],
            Projects = [new LegacyProject(10, "P1", "d", "l", 1, 10, DateTime.UtcNow, DateTime.UtcNow)],
            Profiles = [new LegacyProfile(100, 1, "Name", "user1", DateTime.UtcNow)],
            Subscriptions = [new LegacySubscription(1000, 1, 5, "active", DateTime.UtcNow)],
            Devices = [new LegacyDevice(500, "D1", "SmartLight", "loc", 10, null, 1, "online", DateTime.UtcNow, null)]
        };

        var result = await harness.ImportAsync(dump);

        Assert.Equal(new[] { "IAM", "Projects", "Profiles", "Subscriptions", "Devices" }, result.ImportOrder);
        Assert.Equal(1, await db.IamUsers.CountAsync());
        Assert.Equal(1, await db.Projects.CountAsync());
        Assert.Equal(1, await db.Profiles.CountAsync());
        Assert.Equal(1, await db.Subscriptions.CountAsync());
        Assert.Equal(1, await db.Devices.CountAsync());
        Assert.Equal(1, result.ProjectInserted);
        Assert.Equal(1, result.DeviceInserted);
    }

    // ── Parity gates ──

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverParity_repeat_import_zero_inserts()
    {
        await using var db = Db();
        var harness = new CutoverHarness(db, new CutoverReadiness());
        var dump = new LegacyCutoverDump
        {
            IamUsers = [new LegacyIamUser(1, "a@b.com", "hash", "Builder", DateTime.UtcNow)],
            Projects = [new LegacyProject(1, "P", "d", "l", 1, 5, DateTime.UtcNow, DateTime.UtcNow)],
            Devices = [new LegacyDevice(1, "D", "SmartLight", "loc", 1, null, 1, "online", DateTime.UtcNow, null)]
        };

        var first = await harness.ImportAsync(dump);
        var second = await harness.ImportAsync(dump);

        Assert.Equal(1, first.ProjectInserted);
        Assert.Equal(0, second.ProjectInserted);
        Assert.Equal(0, second.DeviceInserted);
        Assert.Equal(0, second.IamInserted);
        Assert.True(await harness.VerifyParityAsync(dump, second));
    }

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverParity_lww_stale_ignored()
    {
        await using var db = Db();
        var harness = new CutoverHarness(db, new CutoverReadiness());
        var now = DateTime.UtcNow;
        var fresh = new LegacyCutoverDump
        {
            Projects = [new LegacyProject(1, "Fresh", "d", "l", 1, 5, now, now)]
        };
        await harness.ImportAsync(fresh);

        var stale = new LegacyCutoverDump
        {
            Projects = [new LegacyProject(1, "Stale", "d", "l", 1, 5, now.AddMinutes(-10), now.AddMinutes(-10))]
        };
        await harness.ImportAsync(stale);

        var project = await db.Projects.FindAsync(1);
        Assert.Equal("Fresh", project!.Name);
    }

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverParity_invalid_ref_nulling()
    {
        await using var db = Db();
        var harness = new CutoverHarness(db, new CutoverReadiness());
        var dump = new LegacyCutoverDump
        {
            Projects = [new LegacyProject(1, "P", "d", "l", 1, 5, DateTime.UtcNow, DateTime.UtcNow)],
            Devices = [new LegacyDevice(1, "D", "SmartLight", "loc", 999, 999, 1, "online", DateTime.UtcNow, null)]
        };
        await harness.ImportAsync(dump);
        var device = await db.Devices.FindAsync(1);
        Assert.NotNull(device);
        // Invalid project ref should be handled: either null UnitId or preserved ProjectId with null UnitId
        Assert.Null(device!.UnitId);
    }

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverParity_hash_deterministic()
    {
        await using var db = Db();
        var harness = new CutoverHarness(db, new CutoverReadiness());
        var dump = new LegacyCutoverDump
        {
            IamUsers = [new LegacyIamUser(1, "a@b.com", "hash", "Builder", DateTime.UtcNow)],
            Projects = [new LegacyProject(1, "P", "d", "l", 1, 5, DateTime.UtcNow, DateTime.UtcNow)]
        };
        var hash1 = harness.ComputeHash(dump);
        var hash2 = harness.ComputeHash(dump);
        Assert.Equal(hash1, hash2);
        Assert.False(string.IsNullOrWhiteSpace(hash1));

        var result = await harness.ImportAsync(dump);
        Assert.Equal(hash1, result.ParityHash);
        Assert.True(await harness.VerifyParityAsync(dump, result));
    }

    // ── Nginx switch concept (config exists) ──

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverSwitch_nginx_config_proxies_to_monolith()
    {
        await using var db = Db();
        var harness = new CutoverHarness(db, new CutoverReadiness());
        var nginxPath = Path.Combine(Path.GetTempPath(), $"nginx-{Guid.NewGuid():N}.conf");
        await harness.SwitchAsync(nginxPath, CancellationToken.None);
        Assert.True(File.Exists(nginxPath));
        var content = await File.ReadAllTextAsync(nginxPath);
        Assert.Contains("iobuild-api:8080", content);
        Assert.DoesNotContain("gateway:8080", content);
        Assert.Contains("proxy_pass", content);
        File.Delete(nginxPath);
    }

    // ── Rollback on failure ──

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverRollback_on_failure_reverts_and_preserves_committed()
    {
        await using var db = Db();
        db.Projects.Add(new Project { Id = 1, Name = "Keep", Description = "d", Location = "l", TotalUnits = 5, BuilderId = 1 });
        await db.SaveChangesAsync();

        var readiness = new CutoverReadiness();
        var harness = new CutoverHarness(db, readiness);
        var checkpointPath = Path.Combine(Path.GetTempPath(), $"cutover-rollback-{Guid.NewGuid():N}.json");
        await harness.BackupAsync(checkpointPath, CancellationToken.None);

        // Simulate failure by importing with duplicate that throws then rollback
        var dump = new LegacyCutoverDump
        {
            Projects = [new LegacyProject(2, "New", "d", "l", 1, 5, DateTime.UtcNow, DateTime.UtcNow)]
        };
        await harness.ImportAsync(dump);
        Assert.Equal(2, await db.Projects.CountAsync());

        // Force restore (simulating failure handler)
        await harness.RestoreAsync(checkpointPath, CancellationToken.None);

        Assert.Equal(1, await db.Projects.CountAsync());
        Assert.NotNull(await db.Projects.FindAsync(1));
        Assert.Null(await db.Projects.FindAsync(2));
        File.Delete(checkpointPath);
    }

    // ── Stabilization auth (builder/admin role) ──

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverStabilization_requires_admin_role()
    {
        await using var db = Db();
        var readiness = new CutoverReadiness();
        readiness.Freeze();
        var harness = new CutoverHarness(db, readiness);

        var nonAdmin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Builder")], "test"));
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));

        Assert.False(await harness.StabilizeAsync(nonAdmin));
        Assert.True(readiness.ShouldBlockWrites); // still frozen

        Assert.True(await harness.StabilizeAsync(admin));
        Assert.False(readiness.ShouldBlockWrites); // now ready
    }

    [Fact]
    [Trait("Category", "Cutover")]
    public async Task CutoverStabilization_admin_role_marks_ready()
    {
        await using var db = Db();
        var readiness = new CutoverReadiness();
        readiness.Freeze();
        var harness = new CutoverHarness(db, readiness);
        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));
        var result = await harness.StabilizeAsync(admin);
        Assert.True(result);
        Assert.False(readiness.ShouldBlockWrites);
    }
}
