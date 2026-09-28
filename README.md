# MofonCode.Core

Shared kernel for MofonCode .NET solutions: a minimal mediator, result and tracing primitives.

Targets `net10.0`. Published to [nuget.org](https://www.nuget.org/packages/MofonCode.Core) as `MofonCode.Core`.

## What is in it

| Area | Types |
|---|---|
| Mediator | `Mediator`, `IRequest<T>`, `IRequestHandler<,>`, `IRequestBehavior<,>`, and the `ICommandRequest*` equivalents for commands that return no value |
| Results | `ResultExtensions.ToErrorString`, `ThrowIfFailed`, `ResultFailedException` |
| Tracing | `TraceResult` / `TraceResult<T>`, `TraceScope`, `TraceResultAttributes`, endpoint records, `TracerFlushRegistry`, `TraceResultFailureRegistry`, and the FluentResults-to-`ILogger` bridge |
| Concurrency | `Concurrency` (parallelism knobs and a throttled runner), `HandlerLockRegistry` |
| Specifications | `SpecificationsHelper` — `And`, `Or`, `Not`, `All` over predicate expressions |

It depends only on `FluentResults` and the `Microsoft.Extensions.*.Abstractions` packages. No EF Core, no SQL client.

## Where it came from

The code was lifted from `MarketSense/src/SharedKernel` on 2026-09-28, after MarketSense was
sunsetted on 2026-09-20. That project held 40 files; most of them were the product's own domain
— trading clock, supported intervals, entitlements, batch ledger, chart ranges, the news and
market model — and stayed behind. What moved is the part that had no opinion about tickers.

Three things changed on the way across, each documented at its definition:

1. **`SpecificationsHelper.And` / `.Or`** were built with `Expression.Invoke`, which evaluates
   fine in memory but cannot be translated by a relational LINQ provider — a composed
   specification threw at query time. They now rebind parameters and merge the bodies directly.
2. **`Concurrency.MaxDop`** was derived from `ThreadPool.GetAvailableThreads`, a live load
   reading rather than a capacity one, so it moved between reads and collapsed toward 1 under
   load. It now derives from `Environment.ProcessorCount` and is resolved once per process.
3. **`JobResultFailedException`** became **`ResultFailedException`**. The original was named and
   documented for MarketSense's job dispatcher; a shared kernel with no notion of a job should
   not ship a type whose name asserts one.

The market-data tags on `TraceResult` (`WithSymbol`, `WithMarketType`, `WithScorerVersion`) were
dropped, along with the `ErrorLogging` folder, which was shaped by that product's admin portal.
`TraceResult.WithTag(key, value)` is the escape hatch for a consumer's own domain tags.

## Building and testing

```
dotnet build
dotnet run --project tests/MofonCode.Core.Tests
```

`dotnet run --project` is deliberate. The test project is a Microsoft.Testing.Platform
application, and on SDK 10.0.301 `dotnet test` reports `Zero tests ran` (exit 5) against the very
same assembly that reports 40 passing when it runs itself. Running the test app directly is the
platform's native execution model and is what CI uses.

## Analyzer settings

The build runs at `latest-recommended` with `TreatWarningsAsErrors`, and every suppression is
listed with its reason in `Directory.Build.props` (and, for the test project, in its `.csproj`)
rather than as a pragma in code.
