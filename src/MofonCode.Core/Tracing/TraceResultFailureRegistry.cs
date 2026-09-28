using FluentResults;

namespace MofonCode.Core;

/// <summary>
/// Static hook that lets <see cref="TraceResult{T}"/> report handler failures to a host-supplied
/// sink without holding a DI reference. Mirrors <see cref="TracerFlushRegistry"/>.
/// </summary>
/// <remarks>
/// <para>Host startup registers a callback via <see cref="Register"/>. <see cref="TraceResult{T}"/>
/// invokes it on <c>Fail</c> and on terminal <c>Dispose</c>, but only for failures carrying an
/// <see cref="IExceptionalError"/> — domain-level fails (validation, not-found, permission) carry
/// no exception and are deliberately excluded.</para>
/// <para>Carried across from MarketSense's <c>ErrorLogging.ErrorLogFailRegistry</c>. The rest of
/// that folder — the async-local job attribution, the dedupe hash, the sink interface and its
/// entry record — stayed behind: all of it was shaped by that product's AdminPortal errors page
/// and its job/recorded-operation model. This hook is the only part <see cref="TraceResult{T}"/>
/// actually depended on, so it moves here and the folder does not.</para>
/// <para>Best-effort by contract: the callback runs synchronously and must never throw. Any
/// exception it raises is swallowed, because a broken hook must not fail the handler that
/// already failed.</para>
/// </remarks>
public static class TraceResultFailureRegistry
{
    private static Action<TraceResultFailureContext>? _callback;

    /// <summary>Registers the failure hook. Typically called once at host startup.</summary>
    public static void Register(Action<TraceResultFailureContext> callback) => _callback = callback;

    /// <summary>
    /// Invokes the registered hook for a specific failure. No-op when nothing is registered
    /// (unit tests, minimal hosts). Never throws.
    /// </summary>
    public static void Report(TraceResultFailureContext context)
    {
        Action<TraceResultFailureContext>? callback = _callback;
        if (callback is null) return;
        try { callback(context); } catch { /* best-effort */ }
    }
}

/// <summary>
/// Snapshot of a failed <see cref="TraceResult{T}"/> handed to the registered callback. Carries
/// the operation name and the errors; the callback picks service name, correlation and any
/// deduplication details out of its own ambient context.
/// </summary>
public sealed record TraceResultFailureContext(
    string OperationName,
    IReadOnlyList<IError> Errors);
