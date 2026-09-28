using FluentResults;

namespace MofonCode.Core;

/// <summary>
/// Thrown by <see cref="ResultExtensions.ThrowIfFailed{T}"/> to surface a failed
/// <see cref="Result{T}"/> across a boundary that speaks exceptions rather than results.
/// Carries the original <see cref="IError"/> list so the catching side can rebuild a rich
/// failure payload — reason messages, metadata, and any inner exception's stack trace all
/// survive.
/// </summary>
/// <remarks>
/// Carried across from MarketSense, where this was <c>JobResultFailedException</c> and its
/// documentation named that product's <c>JobDispatcher</c>, <c>JobStatus</c> and
/// <c>JobFailureDetails</c>. None of those concepts exist here, and a shared kernel with no
/// notion of a job should not ship a type whose name asserts one, so the type was renamed and
/// its contract restated in terms of the result it carries.
/// </remarks>
public sealed class ResultFailedException : Exception
{
    /// <summary>The errors carried by the result that failed.</summary>
    public IReadOnlyList<IError> Errors { get; }

    /// <summary>Creates an exception carrying the failed result's errors.</summary>
    public ResultFailedException(IReadOnlyList<IError> errors)
        : base(string.Join("; ", (errors ?? []).Select(e => e.Message)))
        => Errors = errors ?? [];

    /// <summary>Creates an exception with no carried errors.</summary>
    public ResultFailedException() : base() => Errors = [];

    /// <summary>Creates an exception with a message and no carried errors.</summary>
    public ResultFailedException(string message) : base(message) => Errors = [];

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public ResultFailedException(string message, Exception innerException)
        : base(message, innerException) => Errors = [];
}
