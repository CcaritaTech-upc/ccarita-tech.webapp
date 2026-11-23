using System.Runtime.CompilerServices;
using IoBuild.TestKit;

namespace IoBuild.Modules.Tests.TestSupport;

// Prepares the shared MySQL test database once, when the test assembly loads
// and before any test host starts.
//
// Several tests start a full API host against the MySQL database. Each host
// seeds the subscription plans on startup with a "check, then insert" that is
// not atomic, so hosts starting together on an empty database race: the loser
// fails on the unique index and still consumes auto-increment ids, leaving
// plan ids such as 1, 3, 4. Tests that expect plans 1 and 2 then fail
// depending only on timing. Creating the schema and seeding here, first,
// makes every host find the rows already present.
//
// Does nothing unless IOBUILD_TEST_MYSQL_CONNECTION is set.
internal static class MySqlSchemaBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var connectionString = MySqlFixture.ConnectionString;
        if (connectionString is null) return;

        try
        {
            MySqlFixture.EnsureSchema(connectionString);
        }
        catch
        {
            // Database not reachable yet: leave it to the tests, which call
            // EnsureSchema again and report the real error on their own.
        }
    }
}
