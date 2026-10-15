namespace UniversalSearch.Core;

/// <summary>Allows only one pending OS handoff; persistence is outside this gate.</summary>
public sealed class LaunchGate
{
    int pending;
    public async Task<bool> RunAsync(Func<Task> launch, Action handedOff)
    {
        if (Interlocked.CompareExchange(ref pending, 1, 0) != 0) return false;
        try
        {
            await launch();
            handedOff();
            return true;
        }
        finally { Volatile.Write(ref pending, 0); }
    }
}
