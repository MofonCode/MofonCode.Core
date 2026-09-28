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

## Releasing

Publishing is tag-driven and gated. Cut a tag and approve the deployment:

```
git tag v0.1.0
git push github v0.1.0
```

`release.yml` derives the version from the tag, builds, tests and packs, then waits on the
`nuget` environment's required reviewer before anything leaves the runner. The csproj `<Version>`
is only the default for a local `dotnet pack`; the tag is what ships, so the two cannot disagree.

### Trusted publishing, not an API key

There is no long-lived nuget.org key stored anywhere. The publish job asks GitHub for an OIDC
token and trades it at nuget.org for an API key that expires in an hour, against a policy
registered on nuget.org:

| Field | Value |
|---|---|
| Repository Owner | `MofonCode` |
| Repository | `MofonCode.Core` |
| Workflow File | `release.yml` (file name only, no path) |
| Environment | `nuget` |
| Scopes | Push new packages **and** new versions, glob `MofonCode.*` |

Naming the environment in the policy is what ties the two gates together: a token minted by any
other job in this repository — or by the same workflow outside the approved environment — does
not satisfy the policy. The one repository secret is `NUGET_USER`, the nuget.org **profile name**
(not an email address), which the token exchange needs to identify the account.

The token is exchanged inside the gated job on purpose. The key lives one hour, and minting it
only after approval means a review that sits overnight delays the key rather than expiring it.

The first publish also permanently activates the policy: nuget.org locks it to this repository's
and owner's numeric ids, which it learns from that first token, so the policy cannot be
resurrected by deleting and recreating a repository of the same name.
