using System.Collections.Concurrent;

namespace MofonCode.Core;

/// <summary>
/// Process-local "is this operation already running" guard. Wrap a write-path handler in
/// <see cref="TryAcquire"/> when concurrent fires would step on each other — e.g. a timer
/// trigger whose work can outlast its interval, or a user-triggered job that timer triggers
/// can race.
///
/// Single-process scope. If the same handler is hosted on multiple instances, this won't
/// coordinate across them — for that, layer a SQL <c>sp_getapplock</c> or similar on top.
///
/// Register as a singleton in DI; one lock per operation name, allocated on first ask.
/// </summary>
public sealed class HandlerLockRegistry
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>
    /// Try to grab the lock for <paramref name="operationName"/> without blocking. Returns
    /// a disposable token (dispose to release) on success, or <c>null</c> if another caller
    /// is already holding it — callers should treat null as "skip this run; another one
    /// owns the work."
    /// </summary>
    public LockToken? TryAcquire(string operationName)
    {
        SemaphoreSlim sem = _locks.GetOrAdd(operationName, _ => new SemaphoreSlim(1, 1));
        return sem.Wait(0) ? new LockToken(sem) : null;
    }

    /// <summary>Ownership of one named lock. Dispose to release it.</summary>
    /// <param name="semaphore">The semaphore released on dispose.</param>
    public sealed class LockToken(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        /// <summary>Releases the lock. Safe to call more than once.</summary>
        public void Dispose()
        {
            // Guard against double-release on exception unwinding paths.
            if (Interlocked.Exchange(ref _released, 1) == 0)
                semaphore.Release();
        }
    }
}
