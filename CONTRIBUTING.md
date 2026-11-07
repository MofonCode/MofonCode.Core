# Contributing

Maintainer documentation. Nothing here is packaged — `README.md` is what ships to nuget.org and
is written for people consuming the library.

## Building and testing

```
dotnet build
dotnet run --project tests/MofonCode.Core.Tests
```

`dotnet run --project` is deliberate, and CI uses it too. The test project is a
Microsoft.Testing.Platform application, and on the .NET 10 SDK `dotnet test` reports
`Zero tests ran` (exit 5) against the very same assembly that reports 40 passing when it runs
itself. A workflow using `dotnet test` would go green having run nothing, which is worse than
failing.

## Analyzer settings

The build runs at `latest-recommended` with `TreatWarningsAsErrors`, and `WarningsNotAsErrors` is
deliberately empty. Every suppression is one commented line in `Directory.Build.props` — or, for
the test project, in its `.csproj` — rather than a pragma buried in code, so the full list of
exceptions is readable in one place.

## Where this code came from

Lifted from `MarketSense/src/SharedKernel` on 2026-09-28, after MarketSense was sunsetted on
2026-09-20. That sunsetting is what made the lift tractable: the extraction had been parked for
fear of two copies drifting — three fixes had already reached one copy and not the other — and
with nothing live to stay in step with it became a one-way move.

Of that project's 40 files, 13 had no opinion about its domain and are here. The rest was the
product's own — market and news model, validation rules over it, trading clock, supported
intervals, entitlements, batch ledger, chart ranges — along with migration services, a SQL
circuit breaker, and an `ErrorLogging` folder shaped by its admin portal. Leaving those behind
is what keeps EF Core, the SQL client and the configuration packages out of this package's
dependency set.

### The two defects fixed on the way across

Both were recorded before the lift as defects that would ride along unless fixed here, because
no maintained codebase would fix them at the source. Both now have a regression test that fails
against the original implementation.

**`SpecificationsHelper.And` / `.Or`** built their result with `Expression.Invoke`. That
evaluates fine in memory, so the defect was invisible to any test using LINQ-to-Objects, but
EF Core's relational translator rejects an `InvocationExpression` — a composed specification
threw *at query time* rather than at compile time. They now rebind the right lambda's parameter
onto the left and merge the bodies, producing one lambda with one parameter. The tests assert
the tree contains no invocation node.

**`Concurrency.MaxDop`** was `Min(ProcessorCount * 2, availableThreadPoolThreads / 2)`. The
second term is a live load reading, not a capacity one: idle, it sat near the pool maximum and
contributed nothing; under load it collapsed toward zero and clamped `MaxDop` to 1, throttling
exactly when throughput mattered. Being a computed property, two reads moments apart could also
disagree. The thread-pool term is gone and each knob resolves once per process.

### Two judgement calls

`JobResultFailedException` became `ResultFailedException` — a shared kernel with no notion of a
job should not ship a type whose name asserts one. And `TraceResult`'s `WithSymbol`,
`WithMarketType` and `WithScorerVersion` were dropped in favour of a generic `WithTag`, so a
consumer names its own domain tags.

## Branches and pull requests

Both branches take pull requests only, and nothing is pushed straight to either. Work happens on
a branch of your own, **squashed into `development`** by pull request; `development` is **merged
into `main` with a merge commit**, so the two keep sharing history and the next promotion carries
only what is new. Only `development` or a `release/*` branch may open a pull request into `main`,
which the `promotion` job checks.

The rulesets are in **Settings > Rules**: `build, test and pack` required on both, `promotion` on
`main` too, and one approval from a code owner, named in [`.github/CODEOWNERS`](.github/CODEOWNERS).
GitHub never counts an author's own approval, so the one bypass is the `owners` team, on pull
requests only: a member merges their own pull request by ticking the bypass box, **and only once
the checks are green**, because the same box would merge past them. Nobody bypasses a push.

## Releasing

**A release is a merge.** `ci.yml` is the one pipeline:

| What | Does |
|---|---|
| a pull request | builds, tests and packs, and keeps the package as a run artifact for seven days |
| a merge into `development` that ships | publishes `<Version>-preview` through the `nuget-preview` environment, once its reviewer approves, and tags the commit `v<that version>` |
| a merge into `main` | publishes `<Version>` through the `nuget` environment, once its reviewer approves, and tags the commit |

The version is the `<Version>` in `src/MofonCode.Core/MofonCode.Core.csproj`. **There is one
preview per version**: the next preview is the next patch, so a pull request into `development`
that changes what ships moves the patch, and a merge that forgets is refused rather than skipped.
What ships is `src/`, the `README.md` the package embeds, and the build settings
(`Directory.Build.props`, `Directory.Packages.props`, `global.json`); a merge touching none of
them publishes nothing, because a nuget.org version can be unlisted and never deleted. A merge into
`main` whose version is already published and that ships nothing publishes nothing; one that does
ship is refused.

Because `README.md` is embedded in the package, a change to it is a change that ships.

### Trusted publishing, not an API key

No long-lived nuget.org key is stored anywhere. The publish job asks GitHub for an OIDC token and
trades it at nuget.org for an API key that expires in an hour, against a policy registered there:

| Field | Value |
|---|---|
| Policy owner | `MofonCode` (nuget.org organization) |
| Repository Owner | `MofonCode` |
| Repository | `MofonCode.Core` |
| Workflow File | `ci.yml` (file name only, no path) |
| Environment | `nuget` on one policy, `nuget-preview` on a second, otherwise identical |
| Scopes | Push new packages **and** new versions, glob `MofonCode.*` |

Naming the environment in the policy is what ties the two gates together: a token minted by any
other job in this repository — or by the same workflow outside the approved environment — does
not satisfy the policy. The one secret, `NUGET_USER`, is the nuget.org profile name that the
token exchange needs to identify the account.

The exchange happens inside the gated job on purpose. The key lives one hour, and minting it only
after approval means a review left until the morning delays the key rather than expiring it.
