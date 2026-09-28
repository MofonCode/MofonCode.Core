using FluentResults;

namespace MofonCode.Core;

/// <summary>Extension methods for extracting error messages from FluentResults.</summary>
public static class ResultExtensions
{
    /// <summary>
    /// Extracts a semicolon-delimited error message string from a failed result, recursively
    /// unwrapping CausedBy reasons and exception messages.
    /// </summary>
    public static string ToErrorString(this ResultBase result)
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
