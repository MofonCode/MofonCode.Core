using FluentResults;

namespace MofonCode.Core;

/// <summary>How much a notice matters.</summary>
public enum NoticeSeverity
{
    /// <summary>Something worth saying that is not a problem.</summary>
    Information,

    /// <summary>Something that worked and that the reader should know about anyway.</summary>
    Warning,

    /// <summary>Something that did not work.</summary>
    Error,
}

/// <summary>One thing to tell whoever is reading.</summary>
/// <param name="Severity">How much it matters.</param>
/// <param name="Message">
/// What to say, written for the person reading it. Never an exception's message: see
/// <see cref="ResultEnvelope"/> for why that is the whole point of this type existing.
/// </param>
public sealed record Notice(NoticeSeverity Severity, string Message);

/// <summary>
/// A result, shaped for something outside the process to read.
/// </summary>
/// <remarks>
/// <para><strong>Why this exists.</strong> A <see cref="Result"/> carries everything a handler knew,
/// including an <see cref="IExceptionalError"/> whose message is the exception's own. FluentResults
/// builds that error with <c>Message = exception.Message</c>, so the obvious boundary code —
/// <c>string.Join("; ", result.Errors.Select(e =&gt; e.Message))</c> — puts the text of a
/// <c>SqlException</c> or a <c>NullReferenceException</c> in front of whoever asked. That is not a
/// stack trace, and it is still a connection string, a column name, or a sentence no reader can act
/// on.</para>
///
/// <para>So this is the one shape that crosses the boundary, and it is built by one function that
/// drops every exceptional error and says one neutral sentence instead. A caller cannot leak by
/// forgetting, because there is nothing to remember: the filtering is not a step, it is the only
/// path.</para>
///
/// <para><strong>It carries no exception, by construction.</strong> Not a message, not a type name,
/// not a stack. The exception is the log's business, and <see cref="ResultExtensions.ToLogString"/>
/// is what the log uses — the two names exist so a call site says which side of the boundary it is
/// on, which the single <c>ToErrorString</c> they replaced did not.</para>
///
/// <para><strong>No dependency on a web framework.</strong> This is a record and a list, so the
/// kernel stays what it is. Turning one into an HTTP response, a SignalR payload or a view model is
/// the host's business, and that adapter belongs wherever the host already is.</para>
/// </remarks>
/// <param name="Succeeded">Whether the operation did what was asked.</param>
/// <param name="Notices">
/// Everything worth telling the reader, in the order the handler recorded it. A reader shows the
/// <see cref="NoticeSeverity.Error"/> ones when <see cref="Succeeded"/> is false and needs no other
/// rule than that.
/// </param>
/// <param name="TraceId">
/// The correlation id, when the caller had one. This is what connects a neutral sentence to the real
/// exception in the log — which is why the neutral sentence is acceptable at all.
/// </param>
public sealed record ResultEnvelope(
    bool Succeeded,
    IReadOnlyList<Notice> Notices,
    string? TraceId = null)
{
    /// <summary>What is said when something failed for a reason the reader cannot be told.</summary>
    /// <remarks>
    /// Deliberately one sentence with no detail, and it names the trace id when there is one so the
    /// reader has something to quote. A product that wants its own wording passes it in.
    /// </remarks>
    public const string SomethingWentWrong =
        "Something went wrong at our end. Nothing you did caused it, and nothing was changed.";

    /// <summary>The notices a reader should show as problems.</summary>
    public IReadOnlyList<Notice> Errors =>
        [.. Notices.Where(notice => notice.Severity == NoticeSeverity.Error)];
}

/// <summary>Turns a result into the one shape that is allowed out of the process.</summary>
public static class ResultEnvelopeExtensions
{
    /// <summary>
    /// Shapes a result for a reader outside the process.
    /// </summary>
    /// <remarks>
    /// <para>Every <see cref="IExceptionalError"/> is dropped — itself and everything it caused —
    /// and replaced by a single neutral notice. A domain failure passes through verbatim, because a
    /// handler writing "Bundle slug 'x' is already in use" wrote that for this reader.</para>
    ///
    /// <para>The neutral notice is added whenever an exceptional error was present, even alongside
    /// domain failures. Showing only the domain half would tell the reader their input was the
    /// problem when something else also broke, and that sends them to fix the wrong thing.</para>
    /// </remarks>
    /// <param name="result">The result to shape.</param>
    /// <param name="traceId">The correlation id, when the caller has one.</param>
    /// <param name="unexpected">
    /// What to say when something failed for a reason the reader cannot be told. Defaults to
    /// <see cref="ResultEnvelope.SomethingWentWrong"/>.
    /// </param>
    /// <returns>The envelope.</returns>
    public static ResultEnvelope ToEnvelope(
        this ResultBase result,
        string? traceId = null,
        string? unexpected = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        List<Notice> notices = [];
        bool somethingBroke = false;

        foreach (IError error in result.Errors)
        {
            Collect(error, notices, ref somethingBroke);
        }

        foreach (IReason reason in result.Reasons)
        {
            // A warning is a thing that happened and is worth saying even on success - an ingest that
            // stored nothing because the market was shut, say. Errors are read from Errors above, so
            // this skips them rather than listing each one twice.
            if (reason is IError)
            {
                continue;
            }

            if (reason is IWarning)
            {
                notices.Add(new Notice(NoticeSeverity.Warning, reason.Message));
            }
        }

        if (somethingBroke)
        {
            notices.Add(new Notice(
                NoticeSeverity.Error,
                unexpected ?? ResultEnvelope.SomethingWentWrong));
        }

        return new ResultEnvelope(result.IsSuccess, notices, traceId);
    }

    /// <summary>
    /// Everything a reader outside the process may be told about a failure, as one line.
    /// </summary>
    /// <remarks>
    /// The same filtering as <see cref="ToEnvelope"/>, because there is one rule and one place that
    /// knows it. Use the envelope where the reader can render a list; use this where a single string
    /// is all there is room for.
    /// </remarks>
    /// <param name="result">The result.</param>
    /// <param name="unexpected">What to say about a failure the reader cannot be told. Optional.</param>
    /// <returns>The joined messages, or an empty string on success.</returns>
    public static string ToUserMessage(this ResultBase result, string? unexpected = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return string.Join("; ", result.ToEnvelope(null, unexpected).Errors.Select(notice => notice.Message));
    }

    /// <summary>
    /// Walks an error and its causes, keeping what is safe to say and noting whether anything broke.
    /// </summary>
    /// <remarks>
    /// An exceptional error ends the walk down that branch. Its causes are the exception's own
    /// business, and a reason nested under a thrown exception is no safer to repeat than the
    /// exception.
    /// </remarks>
    private static void Collect(IError error, List<Notice> notices, ref bool somethingBroke)
    {
        if (error is IExceptionalError)
        {
            somethingBroke = true;
            return;
        }

        notices.Add(new Notice(NoticeSeverity.Error, error.Message));

        foreach (IError cause in error.Reasons)
        {
            Collect(cause, notices, ref somethingBroke);
        }
    }
}
