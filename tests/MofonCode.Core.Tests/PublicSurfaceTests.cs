using System.Reflection;

namespace MofonCode.Core.Tests;

/// <summary>
/// The public surface, listed, so a change to it is a decision rather than a side effect.
/// </summary>
/// <remarks>
/// <para><strong>Why a test rather than an analyzer.</strong> Microsoft's
/// <c>PublicApiAnalyzers</c> does this properly and seeds its baseline through an IDE code fix.
/// Typing that baseline by hand means getting every nullability annotation right first time, and a
/// wrong baseline is worse than none — it reports a surface the package does not have. This asks the
/// same question with the list generated rather than transcribed: run it, and a failure prints the
/// real surface in paste-ready form.</para>
///
/// <para><strong>Why it matters here and not in a tool.</strong> Every consumer of this package
/// inherits its breaking changes, which is why it is pinned pre-1.0 wherever it is used. A member
/// that becomes public by accident is a member this package then owns forever.</para>
/// </remarks>
[TestClass]
public sealed class PublicSurfaceTests
{
    /// <summary>
    /// Every type and member this package promises.
    /// </summary>
    /// <remarks>
    /// Add a line in the same change that makes something public, and say in the commit why the
    /// surface grew. Removing a line is a breaking change and wants a version bump with it.
    /// </remarks>
    private static readonly string[] Approved =
    [
        "MofonCode.Core.CallerSource",
        "MofonCode.Core.CallerSource.Api",
        "MofonCode.Core.CallerSource.Function",
        "MofonCode.Core.Concurrency",
        "MofonCode.Core.Concurrency.ChunkSize",
        "MofonCode.Core.Concurrency.MaxConcurrentOperations",
        "MofonCode.Core.Concurrency.MaxDop",
        "MofonCode.Core.Concurrency.RunThrottledAsync",
        "MofonCode.Core.Debug",
        "MofonCode.Core.Debug.#ctor",
        "MofonCode.Core.Debug.Message",
        "MofonCode.Core.Debug.Metadata",
        "MofonCode.Core.Debug.WithMetadata",
        "MofonCode.Core.EndpointDirection",
        "MofonCode.Core.EndpointDirection.Destination",
        "MofonCode.Core.EndpointDirection.Source",
        "MofonCode.Core.EndpointOptions",
        "MofonCode.Core.EndpointOptions.#ctor",
        "MofonCode.Core.EndpointOptions.Description",
        "MofonCode.Core.EndpointOptions.Type",
        "MofonCode.Core.EndpointOptions.Uri",
        "MofonCode.Core.EndpointRecord",
        "MofonCode.Core.EndpointRecord.#ctor",
        "MofonCode.Core.EndpointRecord.Description",
        "MofonCode.Core.EndpointRecord.Direction",
        "MofonCode.Core.EndpointRecord.Error",
        "MofonCode.Core.EndpointRecord.Status",
        "MofonCode.Core.EndpointRecord.Type",
        "MofonCode.Core.EndpointRecord.Uri",
        "MofonCode.Core.EndpointType",
        "MofonCode.Core.EndpointType.Cache",
        "MofonCode.Core.EndpointType.CosmosDb",
        "MofonCode.Core.EndpointType.EventHub",
        "MofonCode.Core.EndpointType.Http",
        "MofonCode.Core.EndpointType.S3",
        "MofonCode.Core.EndpointType.SqlServer",
        "MofonCode.Core.FluentResultLogging",
        "MofonCode.Core.FluentResultLogging.Initialize",
        "MofonCode.Core.HandlerLockRegistry",
        "MofonCode.Core.HandlerLockRegistry+LockToken",
        "MofonCode.Core.HandlerLockRegistry+LockToken.#ctor",
        "MofonCode.Core.HandlerLockRegistry+LockToken.Dispose",
        "MofonCode.Core.HandlerLockRegistry.#ctor",
        "MofonCode.Core.HandlerLockRegistry.LockToken",
        "MofonCode.Core.HandlerLockRegistry.TryAcquire",
        "MofonCode.Core.HttpEndpointOptions",
        "MofonCode.Core.HttpEndpointOptions.#ctor",
        "MofonCode.Core.ICommandRequest",
        "MofonCode.Core.ICommandRequestBehavior`1",
        "MofonCode.Core.ICommandRequestBehavior`1.Handle",
        "MofonCode.Core.ICommandRequestHandler`1",
        "MofonCode.Core.ICommandRequestHandler`1.Handle",
        "MofonCode.Core.IDebug",
        "MofonCode.Core.IEndpointTracker",
        "MofonCode.Core.IEndpointTracker.WithEndpointFailure",
        "MofonCode.Core.IEndpointTracker.WithEndpointSuccess",
        "MofonCode.Core.IRequestBehavior`2",
        "MofonCode.Core.IRequestBehavior`2.Handle",
        "MofonCode.Core.IRequestHandler`2",
        "MofonCode.Core.IRequestHandler`2.Handle",
        "MofonCode.Core.IRequest`1",
        "MofonCode.Core.ITrace",
        "MofonCode.Core.IWarning",
        "MofonCode.Core.Mediator",
        "MofonCode.Core.Mediator.#ctor",
        "MofonCode.Core.Mediator.Send",
        "MofonCode.Core.Notice",
        "MofonCode.Core.Notice.#ctor",
        "MofonCode.Core.Notice.Deconstruct",
        "MofonCode.Core.Notice.Equals",
        "MofonCode.Core.Notice.GetHashCode",
        "MofonCode.Core.Notice.Message",
        "MofonCode.Core.Notice.Severity",
        "MofonCode.Core.Notice.ToString",
        "MofonCode.Core.NoticeSeverity",
        "MofonCode.Core.NoticeSeverity.Error",
        "MofonCode.Core.NoticeSeverity.Information",
        "MofonCode.Core.NoticeSeverity.Warning",
        "MofonCode.Core.OperationType",
        "MofonCode.Core.OperationType.Command",
        "MofonCode.Core.OperationType.Query",
        "MofonCode.Core.ResultEnvelope",
        "MofonCode.Core.ResultEnvelope.#ctor",
        "MofonCode.Core.ResultEnvelope.Deconstruct",
        "MofonCode.Core.ResultEnvelope.Equals",
        "MofonCode.Core.ResultEnvelope.Errors",
        "MofonCode.Core.ResultEnvelope.GetHashCode",
        "MofonCode.Core.ResultEnvelope.Notices",
        "MofonCode.Core.ResultEnvelope.SomethingWentWrong",
        "MofonCode.Core.ResultEnvelope.Succeeded",
        "MofonCode.Core.ResultEnvelope.ToString",
        "MofonCode.Core.ResultEnvelope.TraceId",
        "MofonCode.Core.ResultEnvelopeExtensions",
        "MofonCode.Core.ResultEnvelopeExtensions.ToEnvelope",
        "MofonCode.Core.ResultEnvelopeExtensions.ToUserMessage",
        "MofonCode.Core.ResultExtensions",
        "MofonCode.Core.ResultExtensions.ThrowIfFailed",
        "MofonCode.Core.ResultExtensions.ToLogString",
        "MofonCode.Core.ResultFailedException",
        "MofonCode.Core.ResultFailedException.#ctor",
        "MofonCode.Core.ResultFailedException.Errors",
        "MofonCode.Core.S3EndpointOptions",
        "MofonCode.Core.S3EndpointOptions.#ctor",
        "MofonCode.Core.SensitiveMetaData",
        "MofonCode.Core.SensitiveMetaData.#ctor",
        "MofonCode.Core.SensitiveMetaData.ToString",
        "MofonCode.Core.SensitiveMetaData.Value",
        "MofonCode.Core.SpecificationsHelper",
        "MofonCode.Core.SpecificationsHelper.All",
        "MofonCode.Core.SpecificationsHelper.And",
        "MofonCode.Core.SpecificationsHelper.Not",
        "MofonCode.Core.SpecificationsHelper.Or",
        "MofonCode.Core.SqlEndpointOptions",
        "MofonCode.Core.SqlEndpointOptions.#ctor",
        "MofonCode.Core.SqlEndpointOptions.Database",
        "MofonCode.Core.SqlEndpointOptions.Server",
        "MofonCode.Core.Trace",
        "MofonCode.Core.Trace.#ctor",
        "MofonCode.Core.Trace.Message",
        "MofonCode.Core.Trace.Metadata",
        "MofonCode.Core.Trace.WithMetadata",
        "MofonCode.Core.TraceReasonBuilder",
        "MofonCode.Core.TraceReasonBuilder.#ctor",
        "MofonCode.Core.TraceReasonBuilder.WithValue",
        "MofonCode.Core.TraceResult",
        "MofonCode.Core.TraceResult.AddReason",
        "MofonCode.Core.TraceResult.Begin",
        "MofonCode.Core.TraceResult.BeginEntryPoint",
        "MofonCode.Core.TraceResult.BeginHandler",
        "MofonCode.Core.TraceResult.DisposeAsync",
        "MofonCode.Core.TraceResult.Fail",
        "MofonCode.Core.TraceResult.Ok",
        "MofonCode.Core.TraceResult.SetGlobalTag",
        "MofonCode.Core.TraceResult.WithConsumerGroup",
        "MofonCode.Core.TraceResult.WithContainerName",
        "MofonCode.Core.TraceResult.WithCorrelationId",
        "MofonCode.Core.TraceResult.WithDatabase",
        "MofonCode.Core.TraceResult.WithDestinationUrl",
        "MofonCode.Core.TraceResult.WithEndpointFailure",
        "MofonCode.Core.TraceResult.WithEndpointSuccess",
        "MofonCode.Core.TraceResult.WithErrorCode",
        "MofonCode.Core.TraceResult.WithEventHub",
        "MofonCode.Core.TraceResult.WithPartition",
        "MofonCode.Core.TraceResult.WithRecordCount",
        "MofonCode.Core.TraceResult.WithRetryCount",
        "MofonCode.Core.TraceResult.WithSourceUrl",
        "MofonCode.Core.TraceResult.WithTag",
        "MofonCode.Core.TraceResult.WithUserId",
        "MofonCode.Core.TraceResultAttributes",
        "MofonCode.Core.TraceResultAttributes.ConsumerGroup",
        "MofonCode.Core.TraceResultAttributes.ContainerName",
        "MofonCode.Core.TraceResultAttributes.CorrelationId",
        "MofonCode.Core.TraceResultAttributes.Database",
        "MofonCode.Core.TraceResultAttributes.DestinationUrl",
        "MofonCode.Core.TraceResultAttributes.DurationMs",
        "MofonCode.Core.TraceResultAttributes.ErrorCode",
        "MofonCode.Core.TraceResultAttributes.EventHub",
        "MofonCode.Core.TraceResultAttributes.IsBusinessTrace",
        "MofonCode.Core.TraceResultAttributes.OperationName",
        "MofonCode.Core.TraceResultAttributes.Partition",
        "MofonCode.Core.TraceResultAttributes.RecordCount",
        "MofonCode.Core.TraceResultAttributes.RetryCount",
        "MofonCode.Core.TraceResultAttributes.SourceUrl",
        "MofonCode.Core.TraceResultAttributes.UserId",
        "MofonCode.Core.TraceResultFailureContext",
        "MofonCode.Core.TraceResultFailureContext.#ctor",
        "MofonCode.Core.TraceResultFailureContext.Deconstruct",
        "MofonCode.Core.TraceResultFailureContext.Equals",
        "MofonCode.Core.TraceResultFailureContext.Errors",
        "MofonCode.Core.TraceResultFailureContext.GetHashCode",
        "MofonCode.Core.TraceResultFailureContext.OperationName",
        "MofonCode.Core.TraceResultFailureContext.ToString",
        "MofonCode.Core.TraceResultFailureRegistry",
        "MofonCode.Core.TraceResultFailureRegistry.Register",
        "MofonCode.Core.TraceResultFailureRegistry.Report",
        "MofonCode.Core.TraceResultLoggingExtensions",
        "MofonCode.Core.TraceResultLoggingExtensions.AddTraceResultLogging",
        "MofonCode.Core.TraceResultLoggingExtensions.UseTraceResultLogging",
        "MofonCode.Core.TraceResultOptions",
        "MofonCode.Core.TraceResultOptions.#ctor",
        "MofonCode.Core.TraceResultOptions.ActivitySourceName",
        "MofonCode.Core.TraceResultOptions.MeterName",
        "MofonCode.Core.TraceResult`1",
        "MofonCode.Core.TraceResult`1.AddReason",
        "MofonCode.Core.TraceResult`1.Begin",
        "MofonCode.Core.TraceResult`1.BeginHandler",
        "MofonCode.Core.TraceResult`1.DisposeAsync",
        "MofonCode.Core.TraceResult`1.Fail",
        "MofonCode.Core.TraceResult`1.Ok",
        "MofonCode.Core.TraceResult`1.Result",
        "MofonCode.Core.TraceResult`1.SetGlobalTag",
        "MofonCode.Core.TraceResult`1.SetResult",
        "MofonCode.Core.TraceResult`1.WithConsumerGroup",
        "MofonCode.Core.TraceResult`1.WithContainerName",
        "MofonCode.Core.TraceResult`1.WithCorrelationId",
        "MofonCode.Core.TraceResult`1.WithDatabase",
        "MofonCode.Core.TraceResult`1.WithDestinationUrl",
        "MofonCode.Core.TraceResult`1.WithEndpointFailure",
        "MofonCode.Core.TraceResult`1.WithEndpointSuccess",
        "MofonCode.Core.TraceResult`1.WithErrorCode",
        "MofonCode.Core.TraceResult`1.WithEventHub",
        "MofonCode.Core.TraceResult`1.WithPartition",
        "MofonCode.Core.TraceResult`1.WithRecordCount",
        "MofonCode.Core.TraceResult`1.WithRetryCount",
        "MofonCode.Core.TraceResult`1.WithSourceUrl",
        "MofonCode.Core.TraceResult`1.WithTag",
        "MofonCode.Core.TraceResult`1.WithUserId",
        "MofonCode.Core.TraceScope",
        "MofonCode.Core.TraceScope.#ctor",
        "MofonCode.Core.TraceScope.CallerSource",
        "MofonCode.Core.TraceScope.Equals",
        "MofonCode.Core.TraceScope.GetHashCode",
        "MofonCode.Core.TraceScope.IsBusinessTrace",
        "MofonCode.Core.TraceScope.OperationName",
        "MofonCode.Core.TraceScope.OperationType",
        "MofonCode.Core.TraceScope.RequestName",
        "MofonCode.Core.TraceScope.ToString",
        "MofonCode.Core.TracerFlushRegistry",
        "MofonCode.Core.TracerFlushRegistry.Flush",
        "MofonCode.Core.TracerFlushRegistry.Register",
        "MofonCode.Core.Warning",
        "MofonCode.Core.Warning.#ctor",
        "MofonCode.Core.Warning.Message",
        "MofonCode.Core.Warning.Metadata",
        "MofonCode.Core.Warning.WithMetadata",
    ];

    [TestMethod]
    public void The_public_surface_is_exactly_what_was_approved()
    {
        string[] actual = Surface();

        Assert.IsTrue(actual.Length > 0, "Nothing was enumerated, so this rule would pass however the package looked.");

        string[] added = [.. actual.Except(Approved, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        string[] removed = [.. Approved.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal)];

        Assert.IsTrue(
            added.Length == 0 && removed.Length == 0,
            Describe(added, removed, actual));
    }

    private static string[] Surface()
    {
        List<string> lines = [];

        foreach (Type type in typeof(Mediator).Assembly.GetExportedTypes())
        {
            lines.Add(type.FullName ?? type.Name);

            foreach (MemberInfo member in type.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                // Property and event accessors, operators and the compiler's record members are
                // consequences of a declaration rather than declarations, so listing them would make
                // the file churn for changes nobody made.
                if (member is MethodInfo method && (method.IsSpecialName || method.DeclaringType == typeof(object)))
                {
                    continue;
                }

                if (member.Name.Contains('<', StringComparison.Ordinal))
                {
                    continue;
                }

                // An enum's storage and a record's synthesised equality members exist because of the
                // declaration, not beside it.
                if (member.Name == "value__")
                {
                    continue;
                }

                string name = member.MemberType == MemberTypes.Constructor ? "#ctor" : member.Name;
                lines.Add((type.FullName ?? type.Name) + "." + name);
            }
        }

        return [.. lines.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static string Describe(string[] added, string[] removed, string[] actual)
    {
        List<string> parts = [];

        if (added.Length > 0)
        {
            parts.Add($"{added.Length} member(s) became public and are not approved:\n  "
                + string.Join("\n  ", added));
        }

        if (removed.Length > 0)
        {
            parts.Add($"{removed.Length} approved member(s) no longer exist, which is a breaking change:\n  "
                + string.Join("\n  ", removed));
        }

        parts.Add("The surface as it stands, for the Approved list:\n\n"
            + string.Join("\n", actual.Select(line => "        \"" + line + "\",")));

        return string.Join("\n\n", parts);
    }
}
