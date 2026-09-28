namespace MofonCode.Core.Tests;

/// <summary>Covers the process-local "is this already running" guard.</summary>
[TestClass]
public sealed class HandlerLockRegistryTests
{
    [TestMethod]
    public void TryAcquire_grantsAnUncontendedLock()
    {
        HandlerLockRegistry registry = new();

        using HandlerLockRegistry.LockToken? token = registry.TryAcquire("op");

        Assert.IsNotNull(token);
    }

    [TestMethod]
    public void TryAcquire_refusesWhileTheLockIsHeld()
    {
        HandlerLockRegistry registry = new();

        using HandlerLockRegistry.LockToken? first = registry.TryAcquire("op");
        HandlerLockRegistry.LockToken? second = registry.TryAcquire("op");

        Assert.IsNotNull(first);
        Assert.IsNull(second, "a second caller must be told to skip its run");
    }

    [TestMethod]
    public void TryAcquire_grantsAgainOnceTheTokenIsDisposed()
    {
        HandlerLockRegistry registry = new();

        registry.TryAcquire("op")!.Dispose();
        using HandlerLockRegistry.LockToken? again = registry.TryAcquire("op");

        Assert.IsNotNull(again);
    }

    [TestMethod]
    public void TryAcquire_keepsOperationsIndependent()
    {
        HandlerLockRegistry registry = new();

        using HandlerLockRegistry.LockToken? a = registry.TryAcquire("a");
        using HandlerLockRegistry.LockToken? b = registry.TryAcquire("b");

        Assert.IsNotNull(a);
        Assert.IsNotNull(b);
    }

    [TestMethod]
    public void TryAcquire_isCaseSensitiveOnTheOperationName()
    {
        HandlerLockRegistry registry = new();

        using HandlerLockRegistry.LockToken? lower = registry.TryAcquire("op");
        using HandlerLockRegistry.LockToken? upper = registry.TryAcquire("OP");

        Assert.IsNotNull(lower);
        Assert.IsNotNull(upper);
    }

    [TestMethod]
    public void Dispose_isIdempotent()
    {
        HandlerLockRegistry registry = new();

        HandlerLockRegistry.LockToken token = registry.TryAcquire("op")!;
        token.Dispose();
        token.Dispose();

        // A double release would have raised the semaphore count above one, letting two
        // callers hold the lock at once.
        using HandlerLockRegistry.LockToken? first = registry.TryAcquire("op");
        HandlerLockRegistry.LockToken? second = registry.TryAcquire("op");

        Assert.IsNotNull(first);
        Assert.IsNull(second, "double dispose must not inflate the lock count");
    }
}
