using Microsoft.EntityFrameworkCore;
using IoBuild.Api.Persistence;

namespace IoBuild.Api.Subscriptions.Interfaces.REST;

// Cross-instance mutual exclusion per builder using a MySQL named lock. The
// lock lives on the context's own connection, which stays open until disposal,
// so every query issued while it is held runs inside the critical section.
// Non-relational providers (in-memory unit tests) have no concurrent writers
// and take no lock.
internal sealed class BuilderConfirmLock : IAsyncDisposable
{
    private readonly IoBuildDbContext? _db;
    private readonly string _name;

    private BuilderConfirmLock(IoBuildDbContext? db, string name) { _db = db; _name = name; }

    public static async Task<BuilderConfirmLock?> AcquireAsync(IoBuildDbContext db, int builderId, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return new BuilderConfirmLock(null, "");

        var name = $"iobuild:sub-confirm:{builderId}";
        await db.Database.OpenConnectionAsync(ct);
        var granted = await db.Database.SqlQuery<int>($"SELECT GET_LOCK({name}, 10) AS `Value`").SingleAsync(ct);
        if (granted != 1)
        {
            await db.Database.CloseConnectionAsync();
            return null;
        }
        return new BuilderConfirmLock(db, name);
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is null) return;
        try { await _db.Database.ExecuteSqlAsync($"DO RELEASE_LOCK({_name})"); }
        finally { await _db.Database.CloseConnectionAsync(); }
    }
}
