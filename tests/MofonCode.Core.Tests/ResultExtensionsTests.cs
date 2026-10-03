using FluentResults;

namespace MofonCode.Core.Tests;

/// <summary>Covers error flattening for a log, and the result-to-exception boundary helper.</summary>
[TestClass]
public sealed class ResultExtensionsTests
{
    [TestMethod]
    public void ToLogString_joinsTopLevelErrors()
    {
        Result result = Result.Fail("first").WithError("second");

        Assert.AreEqual("first; second", result.ToLogString());
    }

    [TestMethod]
    public void ToLogString_unwrapsNestedReasons()
    {
        Error inner = new("inner");
        Error outer = new("outer");
        outer.CausedBy(inner);

        Result result = Result.Fail(outer);

        Assert.AreEqual("outer; inner", result.ToLogString());
    }

    [TestMethod]
    public void ToLogString_prefersTheExceptionMessageForExceptionalErrors()
    {
        Result result = Result.Fail(new ExceptionalError(new InvalidOperationException("blew up")));

        Assert.AreEqual("blew up", result.ToLogString());
    }

    [TestMethod]
    public void ToLogString_isEmptyForASuccess()
        => Assert.AreEqual(string.Empty, Result.Ok().ToLogString());

    [TestMethod]
    public void ThrowIfFailed_doesNothingForASuccess()
    {
        Result.Ok().ThrowIfFailed();
        Result.Ok(5).ThrowIfFailed();
    }

    [TestMethod]
    public void ThrowIfFailed_throwsCarryingTheOriginalErrors()
    {
        Result<int> result = Result.Fail<int>("first").WithError("second");

        ResultFailedException ex = Assert.ThrowsExactly<ResultFailedException>(() => result.ThrowIfFailed());

        Assert.AreEqual(2, ex.Errors.Count);
        Assert.AreEqual("first; second", ex.Message);
    }

    [TestMethod]
    public void ThrowIfFailed_nonGenericThrowsCarryingTheOriginalErrors()
    {
        Result result = Result.Fail("only");

        ResultFailedException ex = Assert.ThrowsExactly<ResultFailedException>(() => result.ThrowIfFailed());

        Assert.AreEqual(1, ex.Errors.Count);
        Assert.AreEqual("only", ex.Message);
    }

    [TestMethod]
    public void ResultFailedException_toleratesNoErrors()
    {
        ResultFailedException ex = new();
        Assert.AreEqual(0, ex.Errors.Count);
    }
}
