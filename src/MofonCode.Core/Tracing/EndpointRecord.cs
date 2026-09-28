using System.Text.Json.Serialization;

namespace MofonCode.Core;

/// <summary>One I/O boundary crossed during a traced operation, as attached to the span summary.</summary>
public sealed class EndpointRecord
{
    /// <summary>Which way data flowed.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EndpointDirection Direction { get; set; }

    /// <summary>The kind of system on the far side.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EndpointType Type { get; set; }

    /// <summary>Address of the endpoint.</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Human-readable note about what the call was for.</summary>
    public string? Description { get; set; }

    /// <summary>Outcome of the call. <c>"ok"</c> unless the call failed.</summary>
    public string Status { get; set; } = "ok";

    /// <summary>Failure message when the call failed; otherwise <see langword="null"/>.</summary>
    public string? Error { get; set; }
}
