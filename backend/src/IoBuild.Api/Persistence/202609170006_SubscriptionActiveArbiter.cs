using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoBuild.Api.Persistence;

[DbContext(typeof(IoBuildDbContext))]
[Migration("202609170006_SubscriptionActiveArbiter")]
public sealed class SubscriptionActiveArbiter : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Partial uniqueness the hard way: MySQL has no filtered indexes, so a
        // stored generated column carries BuilderId only while active (NULL
        // otherwise, and NULLs never collide in a unique index). Concurrent
        // confirms then arbitrate on this index instead of doubling rows.
        migrationBuilder.Sql("ALTER TABLE subscriptions ADD COLUMN ActiveBuilderId INT GENERATED ALWAYS AS (CASE WHEN Status = 'active' THEN BuilderId ELSE NULL END) STORED;");
        migrationBuilder.Sql("CREATE UNIQUE INDEX IX_subscriptions_ActiveBuilderId ON subscriptions (ActiveBuilderId);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IX_subscriptions_ActiveBuilderId ON subscriptions;");
        migrationBuilder.Sql("ALTER TABLE subscriptions DROP COLUMN ActiveBuilderId;");
    }
}
