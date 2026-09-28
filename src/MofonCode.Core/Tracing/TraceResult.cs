using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentResults;

namespace MofonCode.Core;

/// <summary>Records I/O boundary crossings onto a trace.</summary>
public interface IEndpointTracker
{
    /// <summary>Records a successful call to an endpoint.</summary>
    /// <param name="endpoint">The endpoint that was called.</param>
    /// <param name="direction">Which way data flowed.</param>
    void WithEndpointSuccess(EndpointOptions endpoint, EndpointDirection direction);
    /// <summary>Records a failed call to an endpoint.</summary>
    /// <param name="endpoint">The endpoint that was called.</param>
    /// <param name="direction">Which way data flowed.</param>
    /// <param name="exception">The failure.</param>
    void WithEndpointFailure(EndpointOptions endpoint, EndpointDirection direction, Exception exception);
}

/// <summary>
/// A unit of traced work producing a <see cref="Result{T}"/>. Starts an
/// <see cref="Activity"/> on Begin, collects reasons, tags and endpoint crossings while the
/// work runs, and emits logs plus metrics on a terminal call or on dispose.
/// </summary>
/// <typeparam name="T">The value the traced work produces.</typeparam>
public sealed class TraceResult<T> : IAsyncDisposable, IEndpointTracker
{
    private static ActivitySource DefaultActivitySource => FluentResultLogging.ActivitySource;

    private readonly Activity? _activity;
    private readonly TraceScope _context;
    private readonly string _logContext;
    private readonly Stopwatch _stopwatch;
    private readonly ConcurrentBag<IReason> _reasons = [];
    private readonly List<EndpointRecord> _endpoints = [];
    private readonly Dictionary<string, object> _globalTags = [];

    private Result<T>? _result;
    private bool _logged;
    private bool _disposed;
    private bool _reasonEventsEmitted;

    private TraceResult(TraceScope context, string logContext)
    {
        _context = context;
        _logContext = logContext;
        _stopwatch = Stopwatch.StartNew();

        _activity = DefaultActivitySource.StartActivity(context.OperationName, ActivityKind.Internal);

        // Automatic attributes — set on Begin.
        _activity?.SetTag("trace.operation", context.OperationName);
        _activity?.SetTag("trace.request", context.RequestName ?? context.OperationName);
        _activity?.SetTag("trace.business", context.IsBusinessTrace);
        _activity?.SetTag(TraceResultAttributes.OperationName, context.OperationName);
        _activity?.SetTag(TraceResultAttributes.IsBusinessTrace, context.IsBusinessTrace);

        if (context.OperationType is not null)
            _activity?.SetTag("trace.type", context.OperationType.ToString());

        if (context.CallerSource is not null)
            _activity?.SetTag("trace.caller", context.CallerSource.ToString());
    }

    /// <summary>Begins a traced operation from an explicit scope.</summary>
    /// <param name="context">Identifies the operation being traced.</param>
    /// <param name="callerFilePath">Compiler-supplied; names the log context when the scope does not.</param>
    /// <returns>The trace, to be completed by a terminal call or dispose.</returns>
    public static TraceResult<T> Begin(
        TraceScope context,
        [CallerFilePath] string callerFilePath = "")
        => new(context, context.RequestName ?? Path.GetFileNameWithoutExtension(callerFilePath));

    /// <summary>
    /// Begins a traced operation for a request handler, inferring the request name and whether
    /// it is a query or a command from <typeparamref name="TRequest"/>.
    /// </summary>
    /// <typeparam name="TRequest">The request type being handled.</typeparam>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="callerFilePath">Compiler-supplied; used as a fallback log context.</param>
    /// <returns>The trace, to be completed by a terminal call or dispose.</returns>
    public static TraceResult<T> BeginHandler<TRequest>(
        string operationName,
        [CallerFilePath] string callerFilePath = "")
    {
        OperationType operationType = detectOperationType<TRequest>();
        string requestName = typeof(TRequest).DeclaringType?.Name ?? typeof(TRequest).Name;

        TraceScope scope = new()
        {
            OperationName = operationName,
            RequestName = requestName,
            OperationType = operationType,
            IsBusinessTrace = true
        };

        return new TraceResult<T>(scope, requestName);
    }

    /// <summary>The result set so far, or a default success when none has been set.</summary>
    public Result<T> Result => _result ?? FluentResults.Result.Ok<T>(default!);

    /// <summary>Sets a tag on the activity and carries it onto every log scope for this trace.</summary>
    /// <param name="key">Tag key.</param>
    /// <param name="value">Tag value; redacted on the span when sensitive.</param>
    public void SetGlobalTag(string key, object value)
    {
        _globalTags[key] = value;
        _activity?.SetTag(key, value is SensitiveMetaData ? "[REDACTED]" : value);
    }

    /// <summary>Attaches a reason to the trace.</summary>
    /// <param name="reason">The reason to attach.</param>
    /// <returns>A builder for adding metadata to the reason.</returns>
    public TraceReasonBuilder AddReason(IReason reason)
    {
        _reasons.Add(reason);
        return new TraceReasonBuilder(reason, _activity);
    }

    /// <summary>Attaches a success reason carrying the given message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>A builder for adding metadata to the reason.</returns>
    public TraceReasonBuilder AddReason(string message)
        => AddReason(new Success(message));

    /// <summary>Records a successful call to an endpoint.</summary>
    /// <param name="endpoint">The endpoint that was called.</param>
    /// <param name="direction">Which way data flowed.</param>
    public void WithEndpointSuccess(EndpointOptions endpoint, EndpointDirection direction)
    {
        EndpointRecord record = new()
        {
            Direction = direction,
            Type = endpoint.Type,
            Uri = endpoint.Uri,
            Description = endpoint.Description,
            Status = "ok"
        };
        _endpoints.Add(record);

        AddReason(new Debug($"Endpoint [{direction}] [{endpoint.Type}] {endpoint.Uri}"))
            .WithValue("endpoint.direction", direction.ToString())
            .WithValue("endpoint.type", endpoint.Type.ToString())
            .WithValue("endpoint.uri", endpoint.Uri)
            .WithValue("endpoint.status", "ok");
    }

    /// <summary>Records a failed call to an endpoint.</summary>
    /// <param name="endpoint">The endpoint that was called.</param>
    /// <param name="direction">Which way data flowed.</param>
    /// <param name="exception">The failure.</param>
    public void WithEndpointFailure(EndpointOptions endpoint, EndpointDirection direction, Exception exception)
    {
        EndpointRecord record = new()
        {
            Direction = direction,
            Type = endpoint.Type,
            Uri = endpoint.Uri,
            Description = endpoint.Description,
            Status = "failed",
            Error = exception.Message
        };
        _endpoints.Add(record);

        AddReason(new Warning($"Endpoint [{direction}] [{endpoint.Type}] {endpoint.Uri} failed: {exception.Message}"))
            .WithValue("endpoint.direction", direction.ToString())
            .WithValue("endpoint.type", endpoint.Type.ToString())
            .WithValue("endpoint.uri", endpoint.Uri)
            .WithValue("endpoint.status", "failed")
            .WithValue("endpoint.error", exception.Message);
    }

    /// <summary>Completes the trace successfully and emits its logs and metrics.</summary>
    /// <param name="value">The value produced.</param>
    /// <returns>A successful result carrying the value.</returns>
    public Result<T> Ok(T value)
    {
        _result = FluentResults.Result.Ok(value);
        completeTrace();
        TracerFlushRegistry.Flush();
        return _result;
    }

    /// <summary>Completes the trace as failed and emits its logs and metrics.</summary>
    /// <param name="errorMessage">Why the work failed.</param>
    /// <returns>A failed result carrying the message.</returns>
    public Result<T> Fail(string errorMessage)
    {
        _result = FluentResults.Result.Fail<T>(errorMessage);
        completeTrace();
        TracerFlushRegistry.Flush();
        reportFailureIfExceptional();
        return _result;
    }

    /// <summary>Completes the trace as failed and emits its logs and metrics.</summary>
    /// <param name="error">The error to fail with.</param>
    /// <returns>A failed result carrying the error.</returns>
    public Result<T> Fail(IError error)
    {
        _result = FluentResults.Result.Fail<T>(error);
        completeTrace();
        TracerFlushRegistry.Flush();
        reportFailureIfExceptional();
        return _result;
    }

    /// <summary>Completes the trace with an already-built result and emits its logs and metrics.</summary>
    /// <param name="result">The result to record.</param>
    public void SetResult(Result<T> result)
    {
        _result = result;
        completeTrace();
        TracerFlushRegistry.Flush();
        if (result.IsFailed) reportFailureIfExceptional();
    }

    // Only failures carrying an ExceptionalError are reported: domain fails — validation,
    // not-found, permission — carry no exception and are deliberately excluded.
    // Best-effort: swallows any exception from the registry / callback path.
    private void reportFailureIfExceptional()
    {
        if (_result is null || !_result.IsFailed) return;
        try
        {
            for (int i = 0; i < _result.Errors.Count; i++)
                if (_result.Errors[i] is IExceptionalError)
                {
                    TraceResultFailureRegistry.Report(new TraceResultFailureContext(
                        OperationName: _context.OperationName,
                        Errors: _result.Errors));
                    return;
                }
        }
        catch
        {
            // Never let failure reporting fail the handler that already failed.
        }
    }

    #region Fluent attribute API — set both traceResult_* and OTel semantic-convention tag names

    /// <summary>Sets the destination URL for I/O boundary operations (e.g., outbound HTTP call target).</summary>
    public TraceResult<T> WithDestinationUrl(string url)
    {
        _activity?.SetTag(TraceResultAttributes.DestinationUrl, url);
        _activity?.SetTag("url.full", url);
        return this;
    }

    /// <summary>Sets the source URL (e.g., inbound HTTP request URL).</summary>
    public TraceResult<T> WithSourceUrl(string url)
    {
        _activity?.SetTag(TraceResultAttributes.SourceUrl, url);
        _activity?.SetTag("client.address", url);
        return this;
    }

    /// <summary>Sets the SQL / logical database name touched by this operation.</summary>
    public TraceResult<T> WithDatabase(string database)
    {
        _activity?.SetTag(TraceResultAttributes.Database, database);
        _activity?.SetTag("db.name", database);
        return this;
    }

    /// <summary>Sets the storage / cache / Cosmos container this operation targets.</summary>
    public TraceResult<T> WithContainerName(string containerName)
    {
        _activity?.SetTag(TraceResultAttributes.ContainerName, containerName);
        _activity?.SetTag("db.collection.name", containerName);
        return this;
    }

    /// <summary>Sets the Event Hubs hub name.</summary>
    public TraceResult<T> WithEventHub(string hubName)
    {
        _activity?.SetTag(TraceResultAttributes.EventHub, hubName);
        _activity?.SetTag("messaging.destination.name", hubName);
        return this;
    }

    /// <summary>Sets the Event Hubs consumer group.</summary>
    public TraceResult<T> WithConsumerGroup(string consumerGroup)
    {
        _activity?.SetTag(TraceResultAttributes.ConsumerGroup, consumerGroup);
        _activity?.SetTag("messaging.event_hub.consumer_group", consumerGroup);
        return this;
    }

    /// <summary>Sets a partition id/key (Event Hubs, Cosmos, Kafka).</summary>
    public TraceResult<T> WithPartition(string partition)
    {
        _activity?.SetTag(TraceResultAttributes.Partition, partition);
        _activity?.SetTag("messaging.event_hub.partition_id", partition);
        return this;
    }

    /// <summary>
    /// Sets an arbitrary tag on the underlying activity. The escape hatch for tags this kernel
    /// has no opinion about — a consumer's own domain attributes, named by its own constants.
    /// </summary>
    public TraceResult<T> WithTag(string key, object? value)
    {
        _activity?.SetTag(key, value);
        return this;
    }

    /// <summary>Sets the number of records processed.</summary>
    public TraceResult<T> WithRecordCount(long count)
    {
        _activity?.SetTag(TraceResultAttributes.RecordCount, count);
        return this;
    }

    /// <summary>Sets the correlation id (inbound request → downstream call chain).</summary>
    public TraceResult<T> WithCorrelationId(string correlationId)
    {
        _activity?.SetTag(TraceResultAttributes.CorrelationId, correlationId);
        return this;
    }

    /// <summary>Sets the user id for user-scoped operations (auth). Consider hashing for compliance.</summary>
    public TraceResult<T> WithUserId(string userId)
    {
        _activity?.SetTag(TraceResultAttributes.UserId, userId);
        _activity?.SetTag("user.id", userId);
        return this;
    }

    /// <summary>Sets a machine-readable error code on failure.</summary>
    public TraceResult<T> WithErrorCode(string errorCode)
    {
        _activity?.SetTag(TraceResultAttributes.ErrorCode, errorCode);
        _activity?.SetTag("error.type", errorCode);
        return this;
    }

    /// <summary>Sets the retry count observed for this operation.</summary>
    public TraceResult<T> WithRetryCount(int retryCount)
    {
        _activity?.SetTag(TraceResultAttributes.RetryCount, retryCount);
        return this;
    }

    #endregion

    /// <summary>
    /// Completes the trace if no terminal call was made, so work that returned early or threw
    /// still emits its logs and metrics exactly once.
    /// </summary>
    /// <returns>A task that completes when the trace has been emitted.</returns>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        _disposed = true;
        _stopwatch.Stop();

        emitPendingReasonEvents();

        if (!_logged && _result is not null)
            log();

        if (_result is not null)
        {
            if (_result.IsFailed)
                _activity?.SetStatus(ActivityStatusCode.Error,
                    _result.Errors.Count > 0 ? _result.Errors[0].Message : null);
            else
                _activity?.SetStatus(ActivityStatusCode.Ok);
        }

        attachEndpointSummary();

        TraceResultMetrics.Record(
            _context.OperationName,
            _logContext,
            classifyOutcome(_result),
            _stopwatch.Elapsed.TotalMilliseconds,
            (_result?.Reasons ?? [.. _reasons]).ToList());

        _activity?.SetTag("duration_ms", _stopwatch.ElapsedMilliseconds);
        _activity?.SetTag(TraceResultAttributes.DurationMs, _stopwatch.ElapsedMilliseconds);
        _activity?.Dispose();

        // Fallback flush — covers the case where the caller never invoked
        // Ok/Fail (rare — typically indicates an exception path).
        TracerFlushRegistry.Flush();

        return ValueTask.CompletedTask;
    }

    private void completeTrace()
    {
        copyReasons();
        emitPendingReasonEvents();
        log();
    }

    private void copyReasons()
    {
        foreach (IReason reason in _reasons)
        {
            if (reason is IError error)
                _result!.Reasons.Add(error);
            else
                _result!.Reasons.Add(reason);
        }
    }

    private void log()
    {
        if (_logged) return;
        _logged = true;
        _result?.Log(_logContext);
    }

    private void emitPendingReasonEvents()
    {
        if (_reasonEventsEmitted) return;
        _reasonEventsEmitted = true;

        foreach (IReason reason in _reasons)
            emitSpanEvent(reason);
    }

    private static readonly JsonSerializerOptions _endpointJson =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    private void attachEndpointSummary()
    {
        if (_endpoints.Count == 0) return;

        string summary = JsonSerializer.Serialize(new { endpoints = _endpoints }, _endpointJson);

        _activity?.SetTag("trace.endpoints", summary);
    }

    private static string classifyOutcome(ResultBase? result)
    {
        if (result is null) return "untracked";
        if (result.IsFailed)
            return result.Errors.Any(e => e is IExceptionalError) ? "exception" : "failed";
        return result.Reasons.Any(r => r is IWarning) ? "warning" : "ok";
    }

    private void emitSpanEvent(IReason reason)
    {
        if (_activity is null) return;

        ActivityTagsCollection tags = new()
        {
            ["reason.type"] = reason.GetType().Name,
            ["reason.message"] = reason.Message
        };

        foreach (KeyValuePair<string, object> kvp in _globalTags)
            tags[kvp.Key] = kvp.Value is SensitiveMetaData ? "[REDACTED]" : kvp.Value?.ToString();

        foreach (KeyValuePair<string, object> kvp in reason.Metadata)
        {
            if (kvp.Value is not SensitiveMetaData)
                tags[kvp.Key] = kvp.Value?.ToString();
        }

        _activity.AddEvent(new ActivityEvent("reason", tags: tags));
    }

    private static OperationType detectOperationType<TRequest>()
    {
        Type requestType = typeof(TRequest);

        if (typeof(ICommandRequest).IsAssignableFrom(requestType))
            return OperationType.Command;

        if (requestType.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            return OperationType.Query;

        return OperationType.Command;
    }
}

/// <summary>
/// A unit of traced work producing a valueless <see cref="Result"/>. Wraps
/// <see cref="TraceResult{T}"/> so commands do not have to name a response type.
/// </summary>
public sealed class TraceResult : IAsyncDisposable, IEndpointTracker
{
    private readonly TraceResult<bool> _inner;

    private TraceResult(TraceResult<bool> inner) => _inner = inner;

    /// <summary>Begins a traced operation from an explicit scope.</summary>
    /// <param name="context">Identifies the operation being traced.</param>
    /// <param name="callerFilePath">Compiler-supplied; names the log context when the scope does not.</param>
    /// <returns>The trace, to be completed by a terminal call or dispose.</returns>
    public static TraceResult Begin(
        TraceScope context,
        [CallerFilePath] string callerFilePath = "")
        => new(TraceResult<bool>.Begin(context, callerFilePath));

    /// <summary>Begins a traced operation at a host entry point.</summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="callerSource">The kind of host handling the call.</param>
    /// <returns>The trace, to be completed by a terminal call or dispose.</returns>
    public static TraceResult BeginEntryPoint(
        string operationName,
        CallerSource callerSource)
    {
        TraceScope scope = new()
        {
            OperationName = operationName,
            RequestName = operationName,
            CallerSource = callerSource
        };

        return new TraceResult(TraceResult<bool>.Begin(scope));
    }

    /// <summary>Begins a traced operation for a command handler.</summary>
    /// <typeparam name="TRequest">The request type being handled.</typeparam>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="callerFilePath">Compiler-supplied; used as a fallback log context.</param>
    /// <returns>The trace, to be completed by a terminal call or dispose.</returns>
    public static TraceResult BeginHandler<TRequest>(
        string operationName,
        [CallerFilePath] string callerFilePath = "")
        => new(TraceResult<bool>.BeginHandler<TRequest>(operationName, callerFilePath));

    /// <summary>Sets a tag on the activity and carries it onto every log scope for this trace.</summary>
    /// <param name="key">Tag key.</param>
    /// <param name="value">Tag value; redacted on the span when sensitive.</param>
    public void SetGlobalTag(string key, object value) => _inner.SetGlobalTag(key, value);

    /// <summary>Attaches a reason to the trace.</summary>
    /// <param name="reason">The reason to attach.</param>
    /// <returns>A builder for adding metadata to the reason.</returns>
    public TraceReasonBuilder AddReason(IReason reason) => _inner.AddReason(reason);
    /// <summary>Attaches a success reason carrying the given message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>A builder for adding metadata to the reason.</returns>
    public TraceReasonBuilder AddReason(string message) => _inner.AddReason(message);

    /// <summary>Records a successful call to an endpoint.</summary>
    /// <param name="endpoint">The endpoint that was called.</param>
    /// <param name="direction">Which way data flowed.</param>
    public void WithEndpointSuccess(EndpointOptions endpoint, EndpointDirection direction)
        => _inner.WithEndpointSuccess(endpoint, direction);

    /// <summary>Records a failed call to an endpoint.</summary>
    /// <param name="endpoint">The endpoint that was called.</param>
    /// <param name="direction">Which way data flowed.</param>
    /// <param name="exception">The failure.</param>
    public void WithEndpointFailure(EndpointOptions endpoint, EndpointDirection direction, Exception exception)
        => _inner.WithEndpointFailure(endpoint, direction, exception);

    // Fluent attribute passthrough (returns this for chaining on the non-generic version).
    /// <summary>Sets the destination URL for an outbound call.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithDestinationUrl(string url) { _inner.WithDestinationUrl(url); return this; }
    /// <summary>Sets the source URL the operation read from.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithSourceUrl(string url) { _inner.WithSourceUrl(url); return this; }
    /// <summary>Sets the database the operation touched.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithDatabase(string database) { _inner.WithDatabase(database); return this; }
    /// <summary>Sets the document-store container the operation touched.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithContainerName(string containerName) { _inner.WithContainerName(containerName); return this; }
    /// <summary>Sets the event hub the operation used.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithEventHub(string hubName) { _inner.WithEventHub(hubName); return this; }
    /// <summary>Sets the consumer group the operation read under.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithConsumerGroup(string consumerGroup) { _inner.WithConsumerGroup(consumerGroup); return this; }
    /// <summary>Sets the partition the operation used.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithPartition(string partition) { _inner.WithPartition(partition); return this; }
    /// <summary>Sets the number of records the operation handled.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithRecordCount(long count) { _inner.WithRecordCount(count); return this; }
    /// <summary>Sets an arbitrary tag on the underlying activity.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithTag(string key, object? value) { _inner.WithTag(key, value); return this; }
    /// <summary>Sets the correlation id threading this operation to its caller.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithCorrelationId(string correlationId) { _inner.WithCorrelationId(correlationId); return this; }
    /// <summary>Sets the user the operation ran on behalf of.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithUserId(string userId) { _inner.WithUserId(userId); return this; }
    /// <summary>Sets a machine-readable error code.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithErrorCode(string errorCode) { _inner.WithErrorCode(errorCode); return this; }
    /// <summary>Sets how many times the operation was retried.</summary>
    /// <returns>This trace, for chaining.</returns>
    public TraceResult WithRetryCount(int retryCount) { _inner.WithRetryCount(retryCount); return this; }

    /// <summary>Completes the trace successfully and emits its logs and metrics.</summary>
    /// <returns>A successful result.</returns>
    public Result Ok()
    {
        Result<bool> typedResult = _inner.Ok(true);
        return typedResult.IsSuccess ? FluentResults.Result.Ok() : FluentResults.Result.Fail(typedResult.Errors);
    }

    /// <summary>Completes the trace as failed and emits its logs and metrics.</summary>
    /// <param name="errorMessage">Why the work failed.</param>
    /// <returns>A failed result carrying the message.</returns>
    public Result Fail(string errorMessage)
    {
        _inner.Fail(errorMessage);
        return FluentResults.Result.Fail(errorMessage);
    }

    /// <summary>Completes the trace as failed and emits its logs and metrics.</summary>
    /// <param name="error">The error to fail with.</param>
    /// <returns>A failed result carrying the error.</returns>
    public Result Fail(IError error)
    {
        _inner.Fail(error);
        return FluentResults.Result.Fail(error);
    }

    /// <summary>
    /// Completes the trace if no terminal call was made, so work that returned early or threw
    /// still emits its logs and metrics exactly once.
    /// </summary>
    /// <returns>A task that completes when the trace has been emitted.</returns>
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
