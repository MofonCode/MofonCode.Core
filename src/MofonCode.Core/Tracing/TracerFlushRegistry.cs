namespace MofonCode.Core;

/// <summary>
/// Static hook that lets <see cref="TraceResult{T}"/> force-flush the OpenTelemetry
/// TracerProvider without holding a DI reference. Host startup registers the
/// TracerProvider's <c>ForceFlush</c> callback via <see cref="Register"/>; TraceResult
/// invokes it via <see cref="Flush"/> on terminal outcomes (<c>Ok</c>/<c>Fail</c>/Dispose).
/// </summary>
/// <remarks>
/// The flush is fire-and-forget: <see cref="Flush"/> starts the drain on a
/// background task so handler completion is not blocked on the exporter batch
/// round-trip. A 2-second timeout applies to the flush itself.
/// </remarks>
public static class TracerFlushRegistry
{
    private static Func<int, bool>? _flushCallback;

    /// <summary>
    /// Registers a <c>ForceFlush(millisecondsTimeout)</c> callback. Typically called
    /// once at host startup with <c>TracerProvider.ForceFlush</c>.
    /// </summary>
    public static void Register(Func<int, bool> callback) => _flushCallback = callback;

    /// <summary>
    /// Fires the registered flush callback on a background task. No-op if no
    /// callback has been registered (e.g., during unit tests or before host start).
    /// </summary>
    public static void Flush()
    {
        Func<int, bool>? callback = _flushCallback;
        if (callback is null) return;

        _ = Task.Run(() =>
        {
            try
            {
                callback(2_000);
            }
            catch
            {
                // Swallow — a failed flush is not worth crashing the caller over.
            }
        });
    }
}
