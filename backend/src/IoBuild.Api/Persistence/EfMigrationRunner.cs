using System.Data;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Persistence;

public interface IMigrationRunner
{
    Task ApplyAsync(CancellationToken cancellationToken);
}

public sealed class EfMigrationRunner(IoBuildDbContext dbContext) : IMigrationRunner
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (dbContext.Database.IsRelational())
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync(cancellationToken);

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'profiles';";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingColumns.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }

            var columnsToAdd = new (string Name, string Type)[]
            {
                ("PhoneNumber", "VARCHAR(50) NULL"),
                ("Address", "VARCHAR(255) NULL"),
                ("SecondEmail", "VARCHAR(150) NULL"),
                ("PhotoUrl", "LONGTEXT NULL")
            };

            foreach (var (colName, colType) in columnsToAdd)
            {
                if (!existingColumns.Contains(colName))
                {
                    await dbContext.Database.ExecuteSqlRawAsync($"ALTER TABLE profiles ADD COLUMN {colName} {colType};", cancellationToken);
                }
            }

            if (existingColumns.Contains("PhotoUrl"))
            {
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE profiles MODIFY COLUMN PhotoUrl LONGTEXT NULL;", cancellationToken);
            }
            if (existingColumns.Contains("CloudinaryReference"))
            {
                await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE profiles MODIFY COLUMN CloudinaryReference LONGTEXT NULL;", cancellationToken);
            }

            var existingClientColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'clients';";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingClientColumns.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }

            var clientColumnsToAdd = new (string Name, string Type)[]
            {
                ("Email", "VARCHAR(150) NULL"),
                ("PhoneNumber", "VARCHAR(50) NULL"),
                ("Address", "VARCHAR(255) NULL"),
                ("UnitId", "INT NULL"),
                ("UnitNumber", "VARCHAR(50) NULL")
            };

            foreach (var (colName, colType) in clientColumnsToAdd)
            {
                if (!existingClientColumns.Contains(colName))
                {
                    await dbContext.Database.ExecuteSqlRawAsync($"ALTER TABLE clients ADD COLUMN {colName} {colType};", cancellationToken);
                }
            }

            // Single-active-subscription arbiter (migration 202609170006): existing
            // databases created before it need the generated column plus index.
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'subscriptions' AND COLUMN_NAME = 'ActiveBuilderId';";
                var hasArbiter = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
                if (!hasArbiter)
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE subscriptions ADD COLUMN ActiveBuilderId INT GENERATED ALWAYS AS (CASE WHEN Status = 'active' THEN BuilderId ELSE NULL END) STORED;", cancellationToken);
                    await dbContext.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IX_subscriptions_ActiveBuilderId ON subscriptions (ActiveBuilderId);", cancellationToken);
                }
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }
        }
    }
}
