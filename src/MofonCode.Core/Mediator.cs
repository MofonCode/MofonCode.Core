using FluentResults;
using Microsoft.Extensions.DependencyInjection;

namespace MofonCode.Core;

/// <summary>Dispatches requests through a pipeline of behaviors to their handlers.</summary>
/// <param name="serviceProvider">Container the handler and its behaviors are resolved from.</param>
public class Mediator(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Resolves the handler for <typeparamref name="TRequest"/> and invokes it through every
    /// registered <see cref="IRequestBehavior{TRequest, TResponse}"/>, in registration order.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response the request produces.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="cancellationToken">Token observed by the behaviors and the handler.</param>
    /// <returns>The handler result, as returned through the behavior pipeline.</returns>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the request.</exception>
    public async Task<Result<TResponse>> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        IRequestHandler<TRequest, TResponse> handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        List<IRequestBehavior<TRequest, TResponse>> behaviors = [.. serviceProvider.GetServices<IRequestBehavior<TRequest, TResponse>>()];

        // The pipeline function
        async Task<Result<TResponse>> Invoke(int index)
        {
            return index < behaviors.Count
                ? await behaviors[index].Handle(request, () => Invoke(index + 1), cancellationToken).ConfigureAwait(false)
                : await handler.Handle(request, cancellationToken).ConfigureAwait(false);
        }

        return await Invoke(0).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the handler for a command that returns no value and invokes it through every
    /// registered <see cref="ICommandRequestBehavior{TRequest}"/>, in registration order.
    /// </summary>
    /// <typeparam name="TRequest">The command type.</typeparam>
    /// <param name="request">The command to dispatch.</param>
    /// <param name="cancellationToken">Token observed by the behaviors and the handler.</param>
    /// <returns>The handler result, as returned through the behavior pipeline.</returns>
    /// <exception cref="ArgumentNullException">The request is null.</exception>
    /// <exception cref="InvalidOperationException">No handler is registered for the command.</exception>
    public async Task<Result> Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : ICommandRequest
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ICommandRequestHandler<TRequest> handler = serviceProvider.GetRequiredService<ICommandRequestHandler<TRequest>>();
        List<ICommandRequestBehavior<TRequest>> behaviors = [.. serviceProvider.GetServices<ICommandRequestBehavior<TRequest>>()];

        // The pipeline function
        async Task<Result> Invoke(int index)
        {
            return index < behaviors.Count
                ? await behaviors[index].Handle(request, () => Invoke(index + 1), cancellationToken).ConfigureAwait(false)
                : await handler.Handle(request, cancellationToken).ConfigureAwait(false);
        }

        return await Invoke(0).ConfigureAwait(false);
    }
}

/// <summary>Marker interface for requests that return a response of type <typeparamref name="TResponse"/>.</summary>
/// <typeparam name="TResponse">The response the request produces.</typeparam>
public interface IRequest<out TResponse>;

/// <summary>Handles a request and produces a result containing the response.</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response the request produces.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Handles the request.</summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Token observed while handling.</param>
    /// <returns>The result of handling the request.</returns>
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Pipeline behavior that wraps request handling (e.g., validation, logging).</summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response the request produces.</typeparam>
public interface IRequestBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Runs around the rest of the pipeline.</summary>
    /// <param name="request">The request being dispatched.</param>
    /// <param name="next">Invokes the next behavior, or the handler when this is the last one.</param>
    /// <param name="cancellationToken">Token observed while handling.</param>
    /// <returns>The result produced by this behavior, usually that of the next step.</returns>
    Task<Result<TResponse>> Handle(TRequest request, Func<Task<Result<TResponse>>> next, CancellationToken cancellationToken);
}

/// <summary>Marker interface for commands that do not return a value.</summary>
public interface ICommandRequest;

/// <summary>Handles a void command request and returns a result indicating success or failure.</summary>
/// <typeparam name="TRequest">The command type.</typeparam>
public interface ICommandRequestHandler<in TRequest>
    where TRequest : ICommandRequest
{
    /// <summary>Handles the command.</summary>
    /// <param name="request">The command to handle.</param>
    /// <param name="cancellationToken">Token observed while handling.</param>
    /// <returns>The result of handling the command.</returns>
    Task<Result> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Pipeline behavior that wraps void command handling (e.g., validation, logging).</summary>
/// <typeparam name="TRequest">The command type.</typeparam>
public interface ICommandRequestBehavior<TRequest>
    where TRequest : ICommandRequest
{
    /// <summary>Runs around the rest of the pipeline.</summary>
    /// <param name="request">The command being dispatched.</param>
    /// <param name="next">Invokes the next behavior, or the handler when this is the last one.</param>
    /// <param name="cancellationToken">Token observed while handling.</param>
    /// <returns>The result produced by this behavior, usually that of the next step.</returns>
    Task<Result> Handle(TRequest request, Func<Task<Result>> next, CancellationToken cancellationToken);
}
