# MofonCode.Core

A small shared kernel for .NET solutions built as vertical slices: a mediator with a behavior
pipeline, `Result`-shaped handlers, and tracing that turns each handler into one span with its
reasons attached.

It is deliberately thin. It depends on FluentResults and two `Microsoft.Extensions.*.Abstractions`
packages — no EF Core, no database client, no opinion about your domain.

```
dotnet add package MofonCode.Core
```

Requires **.NET 10**.

## Quick start

A request, its handler, and the registration that connects them:

```csharp
using FluentResults;
using MofonCode.Core;
using Microsoft.Extensions.DependencyInjection;

// 1. A request names what it returns.
public sealed record GetUser(int Id) : IRequest<User>;

// 2. A handler returns a Result rather than throwing for expected failures.
public sealed class GetUserHandler(IUserStore store) : IRequestHandler<GetUser, User>
{
    public async Task<Result<User>> Handle(GetUser request, CancellationToken cancellationToken)
    {
        User? user = await store.Find(request.Id, cancellationToken);

        return user is null
            ? Result.Fail<User>($"No user {request.Id}.")
            : Result.Ok(user);
    }
}

// 3. Register the mediator and the handler.
services.AddSingleton<Mediator>();
services.AddScoped<IRequestHandler<GetUser, User>, GetUserHandler>();
```

Then dispatch. Both type arguments are explicit — the request type and what it returns:

```csharp
Result<User> result = await mediator.Send<GetUser, User>(new GetUser(42), cancellationToken);

if (result.IsFailed)
{
    logger.LogWarning("GetUser failed: {Reason}", result.ToLogString());
    return BadRequest(result.ToEnvelope(traceId: HttpContext.TraceIdentifier));
}

return Ok(result.Value);
```

**The two names are the point.** `ToLogString` prefers an exception's own message, which is what a log
wants and what a caller must never see. `ToEnvelope` is what crosses the boundary: it drops every
`IExceptionalError` and says one neutral sentence instead, so a database exception's text cannot reach
whoever asked. A domain failure — `"No user 42."` — passes through verbatim, because a handler wrote
that for this reader. See **The error boundary** below.

Commands that produce no value use `ICommandRequest` and a one-argument `Send`:

```csharp
public sealed record DeactivateUser(int Id) : ICommandRequest;

public sealed class DeactivateUserHandler : ICommandRequestHandler<DeactivateUser>
{
    public Task<Result> Handle(DeactivateUser request, CancellationToken cancellationToken) => ...;
}

Result result = await mediator.Send(new DeactivateUser(42), cancellationToken);
```

## Pipeline behaviors

A behavior wraps every dispatch of the request it is registered for, in registration order.
Validation, logging and retry belong here rather than in each handler:

```csharp
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IRequestBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> Handle(
        TRequest request, Func<Task<Result<TResponse>>> next, CancellationToken cancellationToken)
    {
        List<IError> failures = [];

        foreach (IValidator<TRequest> validator in validators)
        {
            ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);

            // An Error, never an ExceptionalError. A validation message was written for the person
            // who typed the input, so the envelope has to pass it through rather than replace it.
            failures.AddRange(validation.Errors.Select(failure => new Error(failure.ErrorMessage)));
        }

        // Short-circuit: the handler never runs.
        return failures.Count > 0 ? Result.Fail<TResponse>(failures) : await next();
    }
}

// An open generic, so an operation gets validation by declaring a validator and nothing else.
services.AddTransient(typeof(IRequestBehavior<,>), typeof(ValidationBehavior<,>));
```

Note it accumulates: a form with four empty fields reports four problems rather than the first. That is
what a result carrying several errors is for, and the reason this package is built on FluentResults
rather than on a single-error result type.

## Tracing

`TraceResult` wraps a unit of work in an `Activity`, records the reasons it collected, and emits
logs and metrics once — on a terminal call, or on dispose if the work returned early or threw:

```csharp
services.AddTraceResultLogging();          // at registration
serviceProvider.UseTraceResultLogging();   // once, at startup

await using TraceResult<Report> trace = TraceResult<Report>.BeginHandler<GetReport>("build-report");

trace.AddReason("cache miss").WithValue("key", cacheKey);
trace.WithRecordCount(rows.Count)
     .WithTag("tenant", tenantId);         // your own domain tags

return trace.Ok(report);                   // or trace.Fail("...")
```

Failures carrying an exception are forwarded to whatever you register with
`TraceResultFailureRegistry.Register`; domain failures — validation, not-found, permission —
carry no exception and are deliberately not.

## Specifications

Composable predicates that a relational provider can translate, because they compose into a
single lambda rather than an invocation tree:

```csharp
Expression<Func<Order, bool>> open = o => o.ClosedOn == null;
Expression<Func<Order, bool>> large = o => o.Total > 1000m;

var urgent = open.And(large);
var stale  = open.And(large.Not());

IQueryable<Order> matches = db.Orders.Where(urgent);
```

`All<T>()` matches everything, which gives a filter chain somewhere to start.

## Concurrency

```csharp
// Run with a throttle the caller chooses, or the cross-cutting default.
IEnumerable<Func<Task>> work = imports.Select(i => new Func<Task>(() => Import(i)));

await Concurrency.RunThrottledAsync(work, maxConcurrent: 4);

int batch = Concurrency.ChunkSize;   // scales with MaxDop, clamped to [100, 2000]
```

`HandlerLockRegistry` guards a handler that must not run twice at once in a process — a timer
whose work can outlast its interval, say. `TryAcquire` returns `null` when someone else holds it,
which the caller treats as "skip this run":

```csharp
using HandlerLockRegistry.LockToken? token = locks.TryAcquire("nightly-import");
if (token is null) return Result.Ok();   // another run owns the work
```

It is single-process. Across instances, layer a distributed lock on top.

## The error boundary

A `Result` carries everything a handler knew, including an `IExceptionalError` whose `Message`
FluentResults copies from the exception. So the obvious boundary code —
`string.Join("; ", result.Errors.Select(e => e.Message))` — puts the text of a database or
null-reference exception in front of whoever asked. Not a stack trace, and still a connection string,
a column name, or a sentence nobody can act on.

`ResultEnvelope` is the one shape that crosses. Building it is what filters, so a caller cannot leak
by forgetting:

```csharp
ResultEnvelope envelope = result.ToEnvelope(traceId: HttpContext.TraceIdentifier);

// envelope.Succeeded   false
// envelope.Errors      the notices a reader should show as problems
// envelope.TraceId     what connects a neutral sentence to the real exception in the log
```

| | |
|---|---|
| a domain failure | passes through verbatim — a handler wrote it for this reader |
| an exceptional error | dropped, itself and everything it caused, replaced by one neutral sentence |
| both at once | both, because showing only the domain half sends the reader to fix the wrong thing |
| a warning on a success | kept — an ingest that stored nothing on a closed market succeeded and the reader still needs to know |

`ToUserMessage()` is the same filter joined into one line, for where a list will not fit.
`ToLogString()` is the other side: every message including the exception's, for a log. The two names
exist so a call site says which side of the boundary it is on — the single `ToErrorString` they
replaced said neither, which is why the thing to reach for at an HTTP boundary was the one that leaks.

Nothing here depends on a web framework. The envelope is a record and a list; turning one into an HTTP
response, a SignalR payload or a view model belongs wherever the host already is.

## Upgrading from 0.1.x

**`ToErrorString` is gone.** It is replaced by two methods, because its name said neither side of the
boundary and the thing to reach for at an HTTP boundary was the one that leaks an exception's own
message.

| Was | Use | When |
|---|---|---|
| `result.ToErrorString()` | `result.ToLogString()` | writing to a log — identical behaviour, including the exception's message |
| `result.ToErrorString()` | `result.ToEnvelope()` or `result.ToUserMessage()` | anything a caller outside the process will read |

Removed rather than deprecated: an `[Obsolete]` attribute under warnings-as-errors is a trap rather
than a kindness, and the compiler naming every call site is the migration.

## What is in it

| Area | Types |
|---|---|
| Mediator | `Mediator`, `IRequest<T>`, `IRequestHandler<,>`, `IRequestBehavior<,>`, and `ICommandRequest*` for commands returning no value |
| Results | `ResultEnvelope`, `Notice`, `ToEnvelope`, `ToUserMessage`, `ToLogString`, `ThrowIfFailed`, `ResultFailedException` |
| Tracing | `TraceResult` / `TraceResult<T>`, `TraceScope`, `TraceResultAttributes`, endpoint records, `TracerFlushRegistry`, `TraceResultFailureRegistry`, and a FluentResults-to-`ILogger` bridge |
| Concurrency | `Concurrency`, `HandlerLockRegistry` |
| Specifications | `SpecificationsHelper` — `And`, `Or`, `Not`, `All` |

## Links

- [Source](https://github.com/MofonCode/MofonCode.Core)
- [Contributing, building and releasing](https://github.com/MofonCode/MofonCode.Core/blob/main/CONTRIBUTING.md)
- MIT licensed

This code was lifted from a sunsetted internal codebase and two latent defects were fixed on the
way across; `CONTRIBUTING.md` records which, and why.
