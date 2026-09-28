using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MofonCode.Core;

/// <summary>
/// Configuration options for <see cref="TraceResult{T}"/> and <see cref="TraceResult"/> observability.
/// </summary>
/// <remarks>
/// <para>Override defaults via <c>AddTraceResultLogging(options =&gt; { ... })</c> at startup.
/// When no explicit values are provided, both names default to the current process name
/// (falling back to <c>"TraceResult"</c> if the process name is unavailable), with
/// <see cref="MeterName"/> additionally appending a <c>.Meter</c> suffix.</para>
/// </remarks>
public sealed class TraceResultOptions
{
    private static readonly string _defaultName = getDefaultName();

    /// <summary>Name of the <see cref="Meter"/> used for TraceResult metrics.</summary>
    public string MeterName { get; set; } = $"{_defaultName}.Meter";

    /// <summary>Name of the <see cref="ActivitySource"/> used for TraceResult spans.</summary>
    public string ActivitySourceName { get; set; } = _defaultName;

    private static string getDefaultName()
    {
        try
        {
            string? processName = Process.GetCurrentProcess().ProcessName;
            return string.IsNullOrWhiteSpace(processName) ? "TraceResult" : processName;
        }
        catch
        {
            return "TraceResult";
        }
    }
}
