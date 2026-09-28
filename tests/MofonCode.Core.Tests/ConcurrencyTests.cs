namespace MofonCode.Core.Tests;

/// <summary>
/// Covers the throttle helper and the defect carried over from MarketSense: MaxDop was derived
/// from ThreadPool.GetAvailableThreads, a live load reading rather than a capacity one, so the
/// value moved with whatever else the process was doing and collapsed toward 1 under load.
/// The stability assertions below are the regression test.
/// </summary>
[TestClass]
public sealed class ConcurrencyTests
{
    [TestMethod]
    public void MaxDop_derivesFromProcessorCount()
        => Assert.AreEqual(Environment.ProcessorCount * 2, Concurrency.MaxDop);

    [TestMethod]
    public void MaxDop_isStableAcrossReads()
    {
        int first = Concurrency.MaxDop;
        for (int i = 0; i < 50; i++)
            Assert.AreEqual(first, Concurrency.MaxDop, "MaxDop must not move between reads");
    }

    [TestMethod]
    public async Task MaxDop_doesNotCollapseWhileTheThreadPoolIsBusy()
    {
        int idle = Concurrency.MaxDop;

        TaskCompletionSource gate = new();
        // Occupy a slab of pool threads so GetAvailableThreads would report a much lower number.
        Task[] occupiers = [.. Enumerable.Range(0, Environment.ProcessorCount * 4)
            .Select(_ => Task.Run(() => gate.Task.Wait(TimeSpan.FromSeconds(30))))];

        try
        {
            int busy = Concurrency.MaxDop;
            Assert.AreEqual(idle, busy, "MaxDop must not shrink because the pool is busy");
        }
        finally
        {
            gate.SetResult();
            await Task.WhenAll(occupiers);
        }
    }

    [TestMethod]
    public void ChunkSize_staysWithinItsClamp()
    {
        int chunk = Concurrency.ChunkSize;
        Assert.IsTrue(chunk is >= 100 and <= 2000, $"ChunkSize {chunk} outside [100, 2000]");
    }

    [TestMethod]
    public void MaxConcurrentOperations_staysWithinItsClamp()
    {
        int max = Concurrency.MaxConcurrentOperations;
        Assert.IsTrue(max is >= 1 and <= 16, $"MaxConcurrentOperations {max} outside [1, 16]");
    }

    [TestMethod]
    public async Task RunThrottledAsync_runsEveryOperation()
    {
        int completed = 0;
        Func<Task>[] operations = [.. Enumerable.Range(0, 50).Select(_ => new Func<Task>(async () =>
        {
            await Task.Yield();
            Interlocked.Increment(ref completed);
        }))];

        await Concurrency.RunThrottledAsync(operations, 4);

        Assert.AreEqual(50, completed);
    }

    [TestMethod]
    public async Task RunThrottledAsync_neverExceedsTheThrottle()
    {
        int running = 0;
        int peak = 0;
        object sync = new();

        Func<Task>[] operations = [.. Enumerable.Range(0, 40).Select(_ => new Func<Task>(async () =>
        {
            int now = Interlocked.Increment(ref running);
            lock (sync) peak = Math.Max(peak, now);
            await Task.Delay(10);
            Interlocked.Decrement(ref running);
        }))];

        await Concurrency.RunThrottledAsync(operations, 3);

        Assert.IsTrue(peak <= 3, $"peak concurrency {peak} exceeded the throttle of 3");
    }

    [TestMethod]
    public async Task RunThrottledAsync_surfacesAnOperationFailure()
    {
        Func<Task>[] operations =
        [
            () => Task.CompletedTask,
            () => throw new InvalidOperationException("boom"),
            () => Task.CompletedTask,
        ];

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => Concurrency.RunThrottledAsync(operations, 2));
    }

    [TestMethod]
    public async Task RunThrottledAsync_treatsAThrottleBelowOneAsOne()
    {
        int completed = 0;
        Func<Task>[] operations = [.. Enumerable.Range(0, 5).Select(_ => new Func<Task>(() =>
        {
            Interlocked.Increment(ref completed);
            return Task.CompletedTask;
        }))];

        await Concurrency.RunThrottledAsync(operations, 0);

        Assert.AreEqual(5, completed);
    }

    [TestMethod]
    public async Task RunThrottledAsync_rejectsNullOperations()
        => await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => Concurrency.RunThrottledAsync(null!, 2));
}
