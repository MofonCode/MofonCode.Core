using FluentResults;

namespace MofonCode.Core.Tests;

/// <summary>
/// The boundary: what a reader outside the process is allowed to be told.
/// </summary>
/// <remarks>
/// These are the tests the type exists for. The leak they guard against is not a stack trace — it is
/// an exception's own message, which FluentResults copies onto an <c>ExceptionalError</c>, so the
/// obvious boundary code puts a connection string or a column name in front of whoever asked.
/// </remarks>
[TestClass]
public sealed class ResultEnvelopeTests
{
    private const string Secret =
        "Server=sql-prod-07.internal;Database=marketsense;User Id=sa;Password=hunter2";

    [TestMethod]
    public void A_domain_failure_passes_through_because_it_was_written_for_this_reader()
    {
        Result result = Result.Fail("Bundle slug 'starter' is already in use.");

        ResultEnvelope envelope = result.ToEnvelope();

        Assert.IsFalse(envelope.Succeeded);
        Assert.AreEqual(1, envelope.Errors.Count);
        Assert.AreEqual("Bundle slug 'starter' is already in use.", envelope.Errors[0].Message);
    }

    [TestMethod]
    public void An_exception_message_never_reaches_the_reader()
    {
        // The whole reason this type exists. The naive boundary - joining e.Message over Errors -
        // emits the connection string verbatim, because FluentResults builds an ExceptionalError
        // with Message = exception.Message.
        Result result = Result.Fail(new ExceptionalError(new InvalidOperationException(Secret)));

        ResultEnvelope envelope = result.ToEnvelope(traceId: "abc123");

        Assert.IsFalse(envelope.Succeeded);
        Assert.AreEqual(1, envelope.Errors.Count);
        Assert.AreEqual(ResultEnvelope.SomethingWentWrong, envelope.Errors[0].Message);

        string everything = string.Join(" ", envelope.Notices.Select(n => n.Message));
        Assert.IsFalse(everything.Contains("hunter2", StringComparison.Ordinal), everything);
        Assert.IsFalse(everything.Contains("sql-prod-07", StringComparison.Ordinal), everything);
        Assert.IsFalse(
            everything.Contains(nameof(InvalidOperationException), StringComparison.Ordinal),
            "Not even the exception's type name, which names an implementation.");
        Assert.AreEqual("abc123", envelope.TraceId, "The trace id is what connects this to the log.");
    }

    [TestMethod]
    public void A_reason_nested_under_an_exception_is_no_safer_than_the_exception()
    {
        ExceptionalError thrown = new(new InvalidOperationException(Secret));
        thrown.CausedBy(new Error("the retry also failed against " + Secret));

        ResultEnvelope envelope = Result.Fail(thrown).ToEnvelope();

        Assert.AreEqual(1, envelope.Errors.Count);
        Assert.AreEqual(ResultEnvelope.SomethingWentWrong, envelope.Errors[0].Message);
    }

    [TestMethod]
    public void A_domain_failure_beside_an_exception_keeps_both_halves()
    {
        // Showing only the domain half would tell the reader their input was the problem when
        // something else also broke, which sends them to fix the wrong thing.
        Result result = Result
            .Fail("Slug is required.")
            .WithError(new ExceptionalError(new InvalidOperationException(Secret)));

        ResultEnvelope envelope = result.ToEnvelope();

        Assert.AreEqual(2, envelope.Errors.Count);
        Assert.AreEqual("Slug is required.", envelope.Errors[0].Message);
        Assert.AreEqual(ResultEnvelope.SomethingWentWrong, envelope.Errors[1].Message);
    }

    [TestMethod]
    public void A_domain_failures_own_causes_are_kept()
    {
        Error outer = new("The import stored nothing.");
        outer.CausedBy(new Error("Every day in the range was a non-trading day."));

        ResultEnvelope envelope = Result.Fail(outer).ToEnvelope();

        CollectionAssert.AreEqual(
            new[] { "The import stored nothing.", "Every day in the range was a non-trading day." },
            envelope.Errors.Select(notice => notice.Message).ToArray());
    }

    [TestMethod]
    public void A_product_may_word_the_neutral_sentence_itself()
    {
        ResultEnvelope envelope = Result
            .Fail(new ExceptionalError(new InvalidOperationException(Secret)))
            .ToEnvelope(unexpected: "We could not reach the market data service.");

        Assert.AreEqual("We could not reach the market data service.", envelope.Errors[0].Message);
    }

    [TestMethod]
    public void A_success_carries_no_errors_and_says_so()
    {
        ResultEnvelope envelope = Result.Ok().ToEnvelope();

        Assert.IsTrue(envelope.Succeeded);
        Assert.AreEqual(0, envelope.Errors.Count);
    }

    [TestMethod]
    public void A_warning_survives_a_success_because_it_is_worth_saying()
    {
        // An ingest that stored nothing because the market was shut succeeded and the reader still
        // needs to know. A boundary that only carried errors would drop it.
        Result result = Result.Ok().WithSuccess(new Success("stored 0 rows"))
            .WithReason(new Warning("The market was closed for the whole range."));

        ResultEnvelope envelope = result.ToEnvelope();

        Assert.IsTrue(envelope.Succeeded);
        Assert.AreEqual(0, envelope.Errors.Count);
        Assert.AreEqual(1, envelope.Notices.Count);
        Assert.AreEqual(NoticeSeverity.Warning, envelope.Notices[0].Severity);
    }

    [TestMethod]
    public void The_single_line_form_filters_the_same_way()
    {
        // One rule, one place that knows it. A second filter would eventually disagree with this one.
        Result result = Result
            .Fail("Slug is required.")
            .WithError(new ExceptionalError(new InvalidOperationException(Secret)));

        string line = result.ToUserMessage();

        StringAssert.Contains(line, "Slug is required.");
        StringAssert.Contains(line, ResultEnvelope.SomethingWentWrong);
        Assert.IsFalse(line.Contains("hunter2", StringComparison.Ordinal), line);
    }

    [TestMethod]
    public void The_log_form_still_tells_the_log_everything()
    {
        // The pair is the point: the log needs the exception's message and the reader must not have
        // it, so the two names say which side the call site is on.
        Result result = Result.Fail(new ExceptionalError(new InvalidOperationException(Secret)));

        StringAssert.Contains(result.ToLogString(), "hunter2");
        Assert.IsFalse(result.ToUserMessage().Contains("hunter2", StringComparison.Ordinal));
    }
}
