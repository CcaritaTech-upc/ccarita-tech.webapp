namespace IoBuild.Api.Analytics.Domain.Model;

/// <summary>
/// Serializes projection self-sync per user so concurrent dashboard loads for
/// the same tenant cannot double-insert projection rows. Same in-process idiom
/// as device state locks; a multi-instance deployment would need a database
/// arbiter instead.
/// </summary>
internal static class AnalyticsSyncGates
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.Threading.SemaphoreSlim> Gates = new();
    public static async Task<System.IDisposable> EnterAsync(int userId, CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(userId, static _ => new System.Threading.SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }
    private sealed class Releaser(System.Threading.SemaphoreSlim gate) : System.IDisposable { public void Dispose() => gate.Release(); }
}
