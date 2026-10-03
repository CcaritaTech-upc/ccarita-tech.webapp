using IoBuild.Api.Persistence;
using IoBuild.Api.Workflows;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Modules.Tests;

public sealed partial class IamWorkflowTests
{
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
}
