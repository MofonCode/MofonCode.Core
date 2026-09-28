using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentResults;
using Microsoft.Extensions.Logging;

namespace MofonCode.Core;

/// <summary>
/// Configures FluentResults to log results through Microsoft.Extensions.Logging and emit
/// OpenTelemetry spans for each logged result.
/// </summary>
public static class FluentResultLogging
{
    internal static ActivitySource ActivitySource { get; private set; } = new("TraceResult");

    /// <summary>
    /// Initializes FluentResults logging, the <see cref="ActivitySource"/>, and the metrics
    /// <see cref="Meter"/> from the provided options. Prefer the DI extension
    /// <c>UseTraceResultLogging()</c> over calling this directly.
    /// </summary>
    public static void Initialize(ILoggerFactory loggerFactory, TraceResultOptions? options = null)
    {
        options ??= new TraceResultOptions();
        ActivitySource = new ActivitySource(options.ActivitySourceName);
        TraceResultMetrics.Initialize(options.MeterName);

        ResultLogger resultLogger = new(loggerFactory);
        Result.Setup(resultSettingsBuilder => resultSettingsBuilder.Logger = resultLogger);
    }

    private sealed class ResultLogger(ILoggerFactory loggerFactory) : IResultLogger
    {
        private readonly ConcurrentDictionary<string, ILogger> _loggers = new();
        private static ActivitySource Source => ActivitySource;

        private ILogger GetLogger(string context)
            => _loggers.GetOrAdd(context, loggerFactory.CreateLogger);

        public void Log(string context, string content, ResultBase result, LogLevel logLevel)
        {
            if (string.IsNullOrWhiteSpace(context))
                context = "FluentResults";
            ArgumentNullException.ThrowIfNull(result, nameof(result));

            ILogger logger = GetLogger(context);
            LogResult(logger, context, result);
            if (!string.IsNullOrWhiteSpace(content) && logger.IsEnabled(logLevel))
                logger.Log(logLevel, "{Content}", content);
        }

        public void Log<TContext>(string content, ResultBase result, LogLevel logLevel)
            => Log(typeof(TContext).FullName ?? typeof(TContext).Name, content, result, logLevel);

        private static void LogResult(ILogger logger, string context, ResultBase result)
        {
            using Activity? activity = Source.StartActivity(context, ActivityKind.Internal);

            foreach (IReason reason in result.Reasons)
                if (reason is Error error)
                    LogError(logger, context, error);
                else
                    LogReason(logger, reason);
        }

        private static void LogError(ILogger logger, string context, Error error)
        {
            Dictionary<string, object> metaData = error.GetMetaDataFromReason(logger);
            using IDisposable? logScope = logger.BeginScope(metaData);

            using Activity? activity = error.Reasons.Count != 0
                ? Source.StartActivity(context, ActivityKind.Internal)
                : null;

            // Guarded because ToLogMessage walks the error's reason graph to build its string,
            // which is wasted work when Error is not enabled. Matches LogReason below.
            if (logger.IsEnabled(LogLevel.Error))
            {
                if (error is IExceptionalError ee)
                    logger.Log(LogLevel.Error, ee.Exception, "{Message}", ToLogMessage(error));
                else
                    logger.Log(LogLevel.Error, "{Message}", ToLogMessage(error));
            }

            foreach (IError currentReason in error.Reasons)
                LogReason(logger, currentReason);
        }

        private static void LogReason(ILogger logger, IReason reason)
        {
            Dictionary<string, object> metaData = reason.GetMetaDataFromReason(logger);
            using IDisposable? logScope = logger.BeginScope(metaData);

            LogLevel logLevel = GetLogLevel(reason);
            if (logger.IsEnabled(logLevel))
                logger.Log(logLevel, "{Message}", ToLogMessage(reason));
        }

        private static LogLevel GetLogLevel(IReason reason) => reason switch
        {
            IExceptionalError => LogLevel.Error,
            IError => LogLevel.Error,
            IWarning => LogLevel.Warning,
            ISuccess => LogLevel.Information,
            IDebug => LogLevel.Debug,
            ITrace => LogLevel.Trace,
            _ => LogLevel.Debug
        };

        private static string ToLogMessage(IReason reason)
        {
            ReasonStringBuilder builder = new ReasonStringBuilder()
                .WithReasonType(reason.GetType())
                .WithInfo(nameof(reason.Message), reason.Message);

            foreach (KeyValuePair<string, object> metaData in reason.Metadata)
                builder.WithInfo(metaData.Key, metaData.Value.ToString() ?? string.Empty);

            return builder.Build();
        }
    }

    private static Dictionary<string, object> GetMetaDataFromReason(this IReason reason, ILogger logger)
    {
        return logger.IsEnabled(LogLevel.Trace)
            ? reason.ToRevealedMetaData()
            : reason.ToRedactedMetaData();
    }

    private static Dictionary<string, object> ToRevealedMetaData(this IReason reason)
    {
        return !reason.HasSensitiveMetaData()
            ? reason.Metadata
            : reason.Metadata.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value is SensitiveMetaData sensitive
                    ? sensitive.Value ?? string.Empty
                    : kvp.Value);
    }

    private static Dictionary<string, object> ToRedactedMetaData(this IReason reason)
    {
        return !reason.HasSensitiveMetaData()
            ? reason.Metadata
            : reason.Metadata.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value is SensitiveMetaData
                    ? (object)"[REDACTED]"
                    : kvp.Value);
    }

    private static bool HasSensitiveMetaData(this IReason reason)
        => reason.Metadata.Values.Any(v => v is SensitiveMetaData);
}

internal static class TraceResultMetrics
{
    private static Meter _meter = new("TraceResult.Meter", "1.0.0");
    private static Counter<long> _operations = _meter.CreateCounter<long>("trace_result_operations_total");
    private static Counter<long> _failures = _meter.CreateCounter<long>("trace_result_failures_total");
    private static Counter<long> _reasons = _meter.CreateCounter<long>("trace_result_reasons_total");
    private static Histogram<double> _durationMs = _meter.CreateHistogram<double>("trace_result_duration_ms", unit: "ms");

    internal static void Initialize(string meterName)
    {
        _meter = new Meter(meterName, "1.0.0");
        _operations = _meter.CreateCounter<long>("trace_result_operations_total");
        _failures = _meter.CreateCounter<long>("trace_result_failures_total");
        _reasons = _meter.CreateCounter<long>("trace_result_reasons_total");
        _durationMs = _meter.CreateHistogram<double>("trace_result_duration_ms", unit: "ms");
    }

    internal static void Record(
        string operation, string component, string outcome,
        double durationMs, IReadOnlyCollection<IReason> reasons)
    {
        TagList tags = new()
        {
            { "operation", operation },
            { "component", component },
            { "outcome", outcome }
        };

        _operations.Add(1, tags);
        _durationMs.Record(durationMs, tags);

        if (outcome is "failed" or "exception")
            _failures.Add(1, tags);

        foreach (IReason reason in reasons)
        {
            TagList reasonTags = tags;
            reasonTags.Add("reason_type", GetReasonType(reason));
            _reasons.Add(1, reasonTags);
        }
    }

    private static string GetReasonType(IReason reason) => reason switch
    {
        IExceptionalError => "exception",
        IError => "error",
        IWarning => "warning",
        ISuccess => "success",
        IDebug => "debug",
        ITrace => "trace",
        _ => "other"
    };
}

/// <summary>
/// Fluent builder for attaching metadata to a reason and mirroring it onto the current activity.
/// Values of type <see cref="SensitiveMetaData"/> are written to the reason intact but redacted
/// on the span tag.
/// </summary>
/// <param name="reason">The reason to attach metadata to.</param>
/// <param name="activity">The activity to mirror the metadata onto, if any.</param>
public sealed class TraceReasonBuilder(IReason reason, Activity? activity)
{
    /// <summary>Attaches one metadata value to the reason and mirrors it onto the activity.</summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value; redacted on the span tag when sensitive.</param>
    /// <returns>This builder, for chaining.</returns>
    public TraceReasonBuilder WithValue(string key, object value)
    {
        reason.Metadata[key] = value;
        activity?.SetTag(key, value is SensitiveMetaData ? "[REDACTED]" : value);
        return this;
    }
}

/// <summary>A non-fatal reason that something deserves attention.</summary>
public interface IWarning : IReason;

/// <summary>Default <see cref="IWarning"/> carrying a message and metadata.</summary>
/// <param name="message">The warning message.</param>
/// <param name="metadata">Initial metadata, if any.</param>
public class Warning(string message, Dictionary<string, object>? metadata = null) : IWarning
{
    /// <summary>The warning message.</summary>
    public string Message { get; set; } = message;

    /// <summary>Metadata attached to this warning.</summary>
    public Dictionary<string, object> Metadata { get; } = metadata ?? [];

    /// <summary>Attaches one metadata value.</summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value.</param>
    /// <returns>This warning, for chaining.</returns>
    public Warning WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }
}

/// <summary>A reason recorded at debug level.</summary>
public interface IDebug : IReason;

/// <summary>Default <see cref="IDebug"/> carrying a message and metadata.</summary>
/// <param name="message">The debug message.</param>
/// <param name="metadata">Initial metadata, if any.</param>
public class Debug(string message, Dictionary<string, object>? metadata = null) : IDebug
{
    /// <summary>The debug message.</summary>
    public string Message { get; set; } = message;

    /// <summary>Metadata attached to this reason.</summary>
    public Dictionary<string, object> Metadata { get; } = metadata ?? [];

    /// <summary>Attaches one metadata value.</summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value.</param>
    /// <returns>This reason, for chaining.</returns>
    public Debug WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }
}

/// <summary>A reason recorded at trace level.</summary>
public interface ITrace : IReason;

/// <summary>Default <see cref="ITrace"/> carrying a message and metadata.</summary>
/// <param name="message">The trace message.</param>
/// <param name="metadata">Initial metadata, if any.</param>
public class Trace(string message, Dictionary<string, object>? metadata = null) : ITrace
{
    /// <summary>The trace message.</summary>
    public string Message { get; set; } = message;

    /// <summary>Metadata attached to this reason.</summary>
    public Dictionary<string, object> Metadata { get; } = metadata ?? [];

    /// <summary>Attaches one metadata value.</summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value.</param>
    /// <returns>This reason, for chaining.</returns>
    public Trace WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }
}

/// <summary>
/// Wraps a metadata value that must not reach logs or spans in the clear. Metadata is revealed
/// only when the logger has Trace enabled; otherwise it is written as <c>[REDACTED]</c>, and
/// span tags are redacted unconditionally.
/// </summary>
/// <param name="value">The value to protect.</param>
public class SensitiveMetaData(object? value)
{
    /// <summary>The protected value.</summary>
    public object? Value { get; set; } = value;

    /// <summary>
    /// Returns the empty string, deliberately: an accidental interpolation of this object must
    /// not print the value it exists to protect. Read <see cref="Value"/> to get it.
    /// </summary>
    /// <returns>The empty string.</returns>
    public override string ToString() => "";
}
