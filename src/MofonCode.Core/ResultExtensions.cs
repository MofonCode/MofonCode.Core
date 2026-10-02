using FluentResults;

namespace MofonCode.Core;

/// <summary>Extension methods for extracting error messages from FluentResults.</summary>
public static class ResultExtensions
{
    /// <summary>
    /// Everything a failure knew, as one line, for a log.
    /// </summary>
    /// <remarks>
    /// <para>Recursively unwraps <c>CausedBy</c> reasons and prefers an exception's own message,
    /// which is what makes it useful in a log and wrong at a boundary: a database exception's
    /// message names a server and a column, and a null-reference exception's names nothing a reader
    /// can act on.</para>
    ///
    /// <para><strong>Named for the side of the boundary it belongs on.</strong> It was
    /// <c>ToErrorString</c>, which said neither side — so the obvious thing to reach for at an HTTP
    /// boundary was the one that leaks, and that is exactly what the code doing it looked like.
    /// <see cref="ResultEnvelopeExtensions.ToUserMessage"/> is the other side, and
    /// <see cref="ResultEnvelope"/> is what crosses.</para>
    /// </remarks>
    /// <param name="result">The result to describe.</param>
    /// <returns>The joined messages, or an empty string on success.</returns>
    public static string ToLogString(this ResultBase result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return string.Join("; ", result.Errors.SelectMany(FlattenError));
    }

    private static IEnumerable<string> FlattenError(IError error)
    {
        yield return error is IExceptionalError ee ? ee.Exception.Message : error.Message;
        foreach (IError reason in error.Reasons)
            foreach (string msg in FlattenError(reason))
                yield return msg;
    }

    /// <summary>
    /// Throws a <see cref="ResultFailedException"/> carrying the original <see cref="IError"/>
    /// list when the result failed. For boundaries that must surface a failed
    /// <see cref="Result{T}"/> as an exception — a background-work dispatcher, say — without
    /// flattening it to a string first and losing reason metadata and inner stack traces.
    /// </summary>
    public static void ThrowIfFailed<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsFailed)
            throw new ResultFailedException(result.Errors);
    }

    /// <summary>Non-generic overload for callers that <see cref="Result.Fail(string)"/> without a value.</summary>
    public static void ThrowIfFailed(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsFailed)
            throw new ResultFailedException(result.Errors);
    }
}
