namespace MofonCode.Core;

/// <summary>Identifies the operation a <see cref="TraceResult{T}"/> is tracing.</summary>
public sealed record TraceScope
{
    /// <summary>Name of the operation, used as the activity and metric name.</summary>
    public required string OperationName { get; init; }

    /// <summary>Name of the request type being handled, when the operation handles one.</summary>
    public string? RequestName { get; init; }

    /// <summary>Whether the operation reads or writes.</summary>
    public OperationType? OperationType { get; init; }

    /// <summary>The kind of host the operation originated from.</summary>
    public CallerSource? CallerSource { get; init; }

    /// <summary>
    /// Whether this span represents business work rather than infrastructure plumbing.
    /// Emitted as <see cref="TraceResultAttributes.IsBusinessTrace"/> so dashboards can
    /// separate the two. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IsBusinessTrace { get; init; } = true;
}
