namespace MofonCode.Core;

/// <summary>
/// Well-known attribute names emitted by <see cref="TraceResult{T}"/> onto its underlying
/// <see cref="System.Diagnostics.Activity"/>. All domain-shaped attributes are prefixed with
/// <c>traceResult_</c> to keep this namespace distinct from OpenTelemetry semantic conventions.
/// Where a natural OTel counterpart exists, TraceResult sets both — so Aspire/Jaeger/Tempo can
/// auto-visualize and queries stay portable.
/// </summary>
/// <remarks>
/// Carried across from MarketSense minus its market-data block — <c>Symbol</c>,
/// <c>MarketType</c> and <c>ScorerVersion</c>, and the three <c>With…</c> methods on
/// <see cref="TraceResult{T}"/> that set them. A shared kernel has no opinion about tickers.
/// Consumers that want such tags set them with
/// <see cref="TraceResult{T}.WithTag(string, object?)"/> and their own constants.
/// </remarks>
public static class TraceResultAttributes
{
    // Automatic — set on Begin / Dispose without an explicit call.
    /// <summary>Name of the traced operation.</summary>
    public const string OperationName = "traceResult_operationName";
    /// <summary>Wall-clock duration of the traced operation, in milliseconds.</summary>
    public const string DurationMs = "traceResult_duration_ms";
    /// <summary>Whether the span represents business work rather than infrastructure.</summary>
    public const string IsBusinessTrace = "isBusinessTrace"; // intentionally unprefixed per convention.

    // I/O boundary.
    /// <summary>URL the operation wrote to.</summary>
    public const string DestinationUrl = "traceResult_destinationUrl";
    /// <summary>URL the operation read from.</summary>
    public const string SourceUrl = "traceResult_sourceUrl";

    // Infrastructure.
    /// <summary>Database the operation touched.</summary>
    public const string Database = "traceResult_database";
    /// <summary>Document-store container the operation touched.</summary>
    public const string ContainerName = "traceResult_containerName";
    /// <summary>Event hub the operation published to or consumed from.</summary>
    public const string EventHub = "traceResult_eventHub";
    /// <summary>Consumer group the operation read under.</summary>
    public const string ConsumerGroup = "traceResult_consumerGroup";
    /// <summary>Partition the operation read from or wrote to.</summary>
    public const string Partition = "traceResult_partition";

    // Workload shape.
    /// <summary>Number of records the operation handled.</summary>
    public const string RecordCount = "traceResult_recordCount";

    // Cross-cutting observability.
    /// <summary>Correlation id threading this operation to its caller.</summary>
    public const string CorrelationId = "traceResult_correlationId";
    /// <summary>User the operation ran on behalf of.</summary>
    public const string UserId = "traceResult_userId";
    /// <summary>Machine-readable error code for a failed operation.</summary>
    public const string ErrorCode = "traceResult_errorCode";
    /// <summary>How many times the operation was retried.</summary>
    public const string RetryCount = "traceResult_retryCount";
}
