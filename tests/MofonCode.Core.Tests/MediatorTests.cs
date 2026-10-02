using FluentResults;
using Microsoft.Extensions.DependencyInjection;

namespace MofonCode.Core.Tests;

/// <summary>Covers dispatch and pipeline ordering for both the query and command paths.</summary>
[TestClass]
public sealed class MediatorTests
{
    private sealed record GetNumber(int Value) : IRequest<int>;

    private sealed class GetNumberHandler(List<string> log) : IRequestHandler<GetNumber, int>
    {
        public Task<Result<int>> Handle(GetNumber request, CancellationToken cancellationToken)
        {
            log.Add("handler");
            return Task.FromResult(Result.Ok(request.Value * 2));
        }
    }

    private sealed class FailingNumberHandler : IRequestHandler<GetNumber, int>
    {
        public Task<Result<int>> Handle(GetNumber request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Fail<int>("nope"));
    }

    private sealed class NumberBehavior(List<string> log, string name) : IRequestBehavior<GetNumber, int>
    {
        public async Task<Result<int>> Handle(GetNumber request, Func<Task<Result<int>>> next, CancellationToken cancellationToken)
        {
            log.Add($"{name}:before");
            Result<int> result = await next();
            log.Add($"{name}:after");
            return result;
        }
    }

    private sealed record DoThing : ICommandRequest;

    private sealed class DoThingHandler(List<string> log) : ICommandRequestHandler<DoThing>
    {
        public Task<Result> Handle(DoThing request, CancellationToken cancellationToken)
        {
            log.Add("handler");
            return Task.FromResult(Result.Ok());
        }
    }

    private sealed class DoThingBehavior(List<string> log, string name) : ICommandRequestBehavior<DoThing>
    {
        public async Task<Result> Handle(DoThing request, Func<Task<Result>> next, CancellationToken cancellationToken)
        {
            log.Add($"{name}:before");
            Result result = await next();
            log.Add($"{name}:after");
            return result;
        }
    }

    private static (Mediator mediator, List<string> log) BuildQueryPipeline(int behaviorCount)
    {
        List<string> log = [];
        ServiceCollection services = new();
        services.AddSingleton(log);
        services.AddSingleton<IRequestHandler<GetNumber, int>>(_ => new GetNumberHandler(log));
        for (int i = 1; i <= behaviorCount; i++)
        {
            string name = $"b{i}";
            services.AddSingleton<IRequestBehavior<GetNumber, int>>(_ => new NumberBehavior(log, name));
        }
        return (new Mediator(services.BuildServiceProvider()), log);
    }

    [TestMethod]
    public async Task Send_reachesTheHandlerAndReturnsItsValue()
    {
        (Mediator mediator, _) = BuildQueryPipeline(0);

        Result<int> result = await mediator.Send<GetNumber, int>(new GetNumber(21));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.Value);
    }

    [TestMethod]
    public async Task Send_runsBehaviorsAroundTheHandlerInRegistrationOrder()
    {
        (Mediator mediator, List<string> log) = BuildQueryPipeline(2);

        await mediator.Send<GetNumber, int>(new GetNumber(1));

        CollectionAssert.AreEqual(
            new[] { "b1:before", "b2:before", "handler", "b2:after", "b1:after" },
            log);
    }

    [TestMethod]
    public async Task Send_propagatesAHandlerFailure()
    {
        ServiceCollection services = new();
        services.AddSingleton<IRequestHandler<GetNumber, int>, FailingNumberHandler>();
        Mediator mediator = new(services.BuildServiceProvider());

        Result<int> result = await mediator.Send<GetNumber, int>(new GetNumber(1));

        Assert.IsTrue(result.IsFailed);
        Assert.AreEqual("nope", result.ToLogString());
    }

    [TestMethod]
    public async Task Send_rejectsANullRequest()
    {
        (Mediator mediator, _) = BuildQueryPipeline(0);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => mediator.Send<GetNumber, int>(null!));
    }

    [TestMethod]
    public async Task Send_throwsWhenNoHandlerIsRegistered()
    {
        ServiceCollection services = new();
        Mediator mediator = new(services.BuildServiceProvider());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => mediator.Send<GetNumber, int>(new GetNumber(1)));
    }

    [TestMethod]
    public async Task SendCommand_runsBehaviorsAroundTheHandlerInRegistrationOrder()
    {
        List<string> log = [];
        ServiceCollection services = new();
        services.AddSingleton<ICommandRequestHandler<DoThing>>(_ => new DoThingHandler(log));
        services.AddSingleton<ICommandRequestBehavior<DoThing>>(_ => new DoThingBehavior(log, "b1"));
        services.AddSingleton<ICommandRequestBehavior<DoThing>>(_ => new DoThingBehavior(log, "b2"));
        Mediator mediator = new(services.BuildServiceProvider());

        Result result = await mediator.Send(new DoThing());

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "b1:before", "b2:before", "handler", "b2:after", "b1:after" },
            log);
    }

    [TestMethod]
    public async Task SendCommand_rejectsANullRequest()
    {
        ServiceCollection services = new();
        Mediator mediator = new(services.BuildServiceProvider());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => mediator.Send<DoThing>(null!));
    }
}
