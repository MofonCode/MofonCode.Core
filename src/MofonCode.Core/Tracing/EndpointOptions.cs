namespace MofonCode.Core;

/// <summary>Describes an I/O endpoint to a <see cref="TraceResult{T}"/> as it records a boundary crossing.</summary>
public class EndpointOptions
{
    /// <summary>Address of the endpoint.</summary>
    public required string Uri { get; set; }

    /// <summary>Human-readable note about what the call is for.</summary>
    public string? Description { get; set; }

    /// <summary>The kind of system on the far side.</summary>
    public EndpointType Type { get; set; }
}

/// <summary>A SQL Server endpoint, carrying the server and database it addresses.</summary>
public class SqlEndpointOptions : EndpointOptions
{
    /// <summary>The server being addressed.</summary>
    public required string Server { get; set; }

    /// <summary>The database being addressed.</summary>
    public required string Database { get; set; }

    /// <summary>Creates options typed as <see cref="EndpointType.SqlServer"/>.</summary>
    public SqlEndpointOptions() => Type = EndpointType.SqlServer;
}

/// <summary>An HTTP endpoint.</summary>
public class HttpEndpointOptions : EndpointOptions
{
    /// <summary>Creates options typed as <see cref="EndpointType.Http"/>.</summary>
    public HttpEndpointOptions() => Type = EndpointType.Http;
}

/// <summary>An S3-compatible object-store endpoint.</summary>
public class S3EndpointOptions : EndpointOptions
{
    /// <summary>Creates options typed as <see cref="EndpointType.S3"/>.</summary>
    public S3EndpointOptions() => Type = EndpointType.S3;
}
