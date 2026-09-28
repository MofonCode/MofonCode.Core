namespace MofonCode.Core;

/// <summary>Whether a traced operation reads or writes.</summary>
public enum OperationType
{
    /// <summary>A read that does not change state.</summary>
    Query,
    /// <summary>A write that changes state.</summary>
    Command
}

/// <summary>The kind of host that originated a traced operation.</summary>
public enum CallerSource
{
    /// <summary>An HTTP API surface.</summary>
    Api,
    /// <summary>A background or serverless function host.</summary>
    Function
}

/// <summary>Which way data flowed across a traced I/O boundary.</summary>
public enum EndpointDirection
{
    /// <summary>The operation read from the endpoint.</summary>
    Source,
    /// <summary>The operation wrote to the endpoint.</summary>
    Destination
}

/// <summary>The kind of system on the far side of a traced I/O boundary.</summary>
public enum EndpointType
{
    /// <summary>An HTTP service.</summary>
    Http,
    /// <summary>A SQL Server database.</summary>
    SqlServer,
    /// <summary>A Cosmos DB account.</summary>
    CosmosDb,
    /// <summary>An event hub or comparable message broker.</summary>
    EventHub,
    /// <summary>A cache such as Redis.</summary>
    Cache,
    /// <summary>An S3-compatible object store.</summary>
    S3
}
