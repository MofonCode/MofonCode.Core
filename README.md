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
    return Problem(result.ToErrorString());   // "No user 42."

return Ok(result.Value);
```

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
public sealed class ValidateUser(IValidator<GetUser> validator)
    : IRequestBehavior<GetUser, User>
{
    public async Task<Result<User>> Handle(
        GetUser request, Func<Task<Result<User>>> next, CancellationToken cancellationToken)
    {
        ValidationResult validation = await validator.ValidateAsync(request, cancellationToken);

        // Short-circuit: the handler never runs.
        return validation.IsValid
            ? await next()
            : Result.Fail<User>(validation.ToString());
    }
}

services.AddScoped<IRequestBehavior<GetUser, User>, ValidateUser>();
```

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

## What is in it

| Area | Types |
|---|---|
| Mediator | `Mediator`, `IRequest<T>`, `IRequestHandler<,>`, `IRequestBehavior<,>`, and `ICommandRequest*` for commands returning no value |
| Results | `ToErrorString`, `ThrowIfFailed`, `ResultFailedException` |
| Tracing | `TraceResult` / `TraceResult<T>`, `TraceScope`, `TraceResultAttributes`, endpoint records, `TracerFlushRegistry`, `TraceResultFailureRegistry`, and a FluentResults-to-`ILogger` bridge |
| Concurrency | `Concurrency`, `HandlerLockRegistry` |
| Specifications | `SpecificationsHelper` — `And`, `Or`, `Not`, `All` |

## Links

- [Source](https://github.com/MofonCode/MofonCode.Core)
- [Contributing, building and releasing](https://github.com/MofonCode/MofonCode.Core/blob/main/CONTRIBUTING.md)
- MIT licensed

This code was lifted from a sunsetted internal codebase and two latent defects were fixed on the
way across; `CONTRIBUTING.md` records which, and why.
