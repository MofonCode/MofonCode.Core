namespace MofonCode.Core;

/// <summary>Process-wide parallelism knobs, each overridable by environment variable.</summary>
/// <remarks>
/// <para><b>Carried across from MarketSense with a fix.</b> The original derived
/// <see cref="MaxDop"/> from <c>ThreadPool.GetAvailableThreads() / 2</c>, combined with a
/// processor-based limit by <c>Math.Min</c>. That reads the thread pool's <i>currently
/// available</i> worker count, which is a live load measurement, not a capacity one — so the
/// value moved with whatever else the process happened to be doing. On an idle process it sits
/// near the pool maximum and the processor term wins, making the whole thread-pool branch
/// dead weight; under load it collapses toward zero and clamps <see cref="MaxDop"/> to 1,
/// throttling exactly when throughput matters most. Worse, it was a computed property, so two
/// reads moments apart could disagree and a caller sizing a partition from it could not rely
/// on the answer.</para>
/// <para>The thread-pool term is gone. The limits now derive from
/// <see cref="Environment.ProcessorCount"/>, which is a capacity, and each is resolved once on
/// first use and cached so every reader sees the same number for the life of the process.
/// Environment overrides are read during that one resolution.</para>
/// </remarks>
public static class Concurrency
{
    private static readonly Lazy<int> _maxDop = new(calculateMaxDop);
    private static readonly Lazy<int> _chunkSize = new(calculateChunkSize);
    private static readonly Lazy<int> _maxConcurrentOperations = new(calculateMaxConcurrentOperations);

    /// <summary>
    /// Effective max degree of parallelism. Defaults to <see cref="Environment.ProcessorCount"/>
    /// doubled; override with <c>Concurrency__MaxDop</c>. Resolved once per process.
    /// </summary>
    public static int MaxDop => _maxDop.Value;

    /// <summary>
    /// Chunk size for batching parallel work. Scales with <see cref="MaxDop"/>; override with
    /// <c>Concurrency__ChunkSize</c>. Clamped to [100, 2000]. Resolved once per process.
    /// </summary>
    public static int ChunkSize => _chunkSize.Value;

    /// <summary>
    /// Max concurrent high-level operations (e.g. command fan-out, bulk ingest loops). Each op
    /// may use internal parallelism, so keep this modest. Override with
    /// <c>Concurrency__MaxConcurrentOperations</c>. Default 8, clamped to [1, 16].
    /// </summary>
    public static int MaxConcurrentOperations => _maxConcurrentOperations.Value;

    private static int calculateMaxDop()
        => int.TryParse(Environment.GetEnvironmentVariable("Concurrency__MaxDop"), out int maxDop)
            ? Math.Max(1, maxDop)
            : Math.Max(1, Environment.ProcessorCount * 2);

    private static int calculateChunkSize()
        => int.TryParse(Environment.GetEnvironmentVariable("Concurrency__ChunkSize"), out int chunkSize)
            ? Math.Clamp(chunkSize, 100, 2000)
            : Math.Clamp(MaxDop * 50, 100, 2000);

    private static int calculateMaxConcurrentOperations()
        => int.TryParse(Environment.GetEnvironmentVariable("Concurrency__MaxConcurrentOperations"), out int max)
            ? Math.Clamp(max, 1, 16)
            : 8;

    /// <summary>Runs async ops concurrently with the default throttle (<see cref="MaxConcurrentOperations"/>).</summary>
    public static Task RunThrottledAsync(IEnumerable<Func<Task>> operations, CancellationToken cancellationToken = default)
        => RunThrottledAsync(operations, MaxConcurrentOperations, cancellationToken);

    /// <summary>
    /// Runs async ops with an explicit throttle. Useful when the caller knows per-op cost better
    /// than the cross-cutting default (e.g. bulk import where "one op" is a full day's copy and
    /// the right concurrency depends on downstream headroom).
    /// </summary>
    public static async Task RunThrottledAsync(
        IEnumerable<Func<Task>> operations, int maxConcurrent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (maxConcurrent < 1) maxConcurrent = 1;

        using SemaphoreSlim semaphore = new(maxConcurrent, maxConcurrent);
        List<Task> tasks = [];

        try
        {
            foreach (Func<Task> operation in operations)
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await operation().ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }
        }
        catch
        {
            // Enumeration or cancellation cut the loop short. Let the work already in flight
            // finish so no task outlives the semaphore it releases, but swallow its failures:
            // the exception that stopped the loop is the one worth reporting.
            try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { /* superseded */ }
            throw;
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
