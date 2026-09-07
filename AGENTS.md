# AGENTS.md

> **Quick reference:** See [MEMORY.md](MEMORY.md) for a detailed codebase map including dependency graphs, subsystem overviews, controller patterns, and key file locations.

## Project summary

`blue-nile-pds` is a .NET 10 preview fork of `atompds`, a proof-of-concept ATProto PDS implementation in C#. Treat it as experimental and learning-focused, not production-ready. Preserve behavior unless the task explicitly requires protocol or storage changes.

## Solution layout

- `src/Host/`: ASP.NET Core host (`Host.csproj` → `BlueNilePds.Host.dll`), Minimal API endpoints, middleware, startup, config binding.
- `src/Pds/`: PDS-specific services such as `AccountManager`, `ActorStore`, `BlobStore`, `Sequencer`, `Mailer`, and `Xrpc`.
- `src/Core/`: lower-level libraries such as `Cid`, `Common`, `Crypto`, `Did`, `Handle`, `Identity`, and `Repo`.
- `src/Tools/PdsAdmin.Cli/`: admin CLI.
- `src/Web/`: single React SPA serving the public site at `/` and the admin UI at `/admin/*`.
- `src/Tools/Migration/`: batch migration utility for actor stores.
- `test/`: TUnit test projects (`Host.Tests`, `Cid.Tests`, …). `SubscribeTester` lives in `src/Tools/SubscribeTester`.
- `BlueNilePds.slnx`: root solution file. Use this for solution-wide build and test commands.

## Environment and prerequisites

- SDK is pinned in `global.json` to `10.0.100` with `rollForward: latestMinor`.
- Projects target `net10.0`.
- `src/Host/Host.csproj` enables `LangVersion=preview`, nullable reference types, and invariant globalization.
- Do not downgrade language or framework features unless the task explicitly requires it.

## Build, run, and test

Run commands from the repository root unless a task clearly needs a project directory.

```bash
dotnet build BlueNilePds.slnx
dotnet test --solution BlueNilePds.slnx
dotnet run --project src/Host/Host.csproj
dotnet run --project src/Tools/PdsAdmin.Cli/PdsAdmin.Cli.csproj
```

`dotnet test BlueNilePds.slnx` (positional) does NOT work — must use `--solution` flag.

Useful focused test commands:

```bash
dotnet test test/Cid.Tests/CID.Tests.csproj
dotnet test test/Common.Tests/Common.Tests.csproj
dotnet test test/ActorStore.Tests/ActorStore.Tests.csproj
dotnet test test/Host.Tests/Host.Tests.csproj
```

Build may hit MSB4166 node crashes on resource-constrained machines; add `-m:1` or `-m:2` if that occurs.

For dependency hygiene:

```bash
dotnet list BlueNilePds.slnx package --vulnerable --include-transitive
```

## Configuration

- Runtime config is bound from the `Config` section into `src/Host/Configuration/ServerEnvironment.cs`.
- Start from `src/Host/appsettings.Development.json.example`.
- `**/Host/appsettings.*.json` is gitignored; do not commit local secrets.
- `PDS_JWT_SECRET` and `PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX` are required.
- `ServerConfig` expands `~/` paths and creates missing data directories.
- `PdsAdmin.Cli` uses `src/Tools/PdsAdmin.Cli/pdsenv.json`; avoid committing real credentials there.

## Architecture notes

- Dependency registration lives in `src/Host/Configuration/ServerConfig.cs`.
- Startup pipeline lives in `src/Host/Program.cs`.
- All HTTP endpoints use ASP.NET Core Minimal APIs (no MVC controllers). Endpoints are in `src/Host/Endpoints/`, organized by ATProto namespace:
  - `Endpoints/RootEndpoints.cs`, `ErrorEndpoints.cs`, `WellKnownEndpoints.cs`
  - `Endpoints/OAuth/` — OAuth token, authorize, and client-metadata endpoints
  - `Endpoints/Xrpc/` — all `xrpc/` routes grouped by ATProto namespace (`Admin`, `Identity`, `Moderation`, `Repo`, `Server`, `Sync`, `Temp`)
  - `Endpoints/Xrpc/AppViewProxyEndpoints.cs` — catchall `{nsid}` proxy routes (registered last)
  - `Endpoints/EndpointRegistration.cs` — `MapEndpoints()` extension method that wires everything together
- Auth is enforced via `AuthMiddleware` reading endpoint metadata set with `.WithMetadata(new AccessStandardAttribute())` etc.
- Rate limiting is applied with `.RequireRateLimiting("policy-name")` on individual endpoints.
- JSON uses `System.Text.Json` with `ConfigureHttpJsonOptions` (same options as former `AddControllers().AddJsonOptions()`). CarpaNet-generated serializer contexts are used for ATProto models.
- `AccountManagerDb` and `SequencerDb` migrations run automatically on app startup.
- Actor repos are stored per DID via `ActorRepositoryProvider` in `src/Pds/ActorStore/`.
- Firehose event pipeline: HTTP write handler → `SequencerRepository.SequenceEventAsync` → saves to `RepoSeqs` table → calls `ISequencerNotifier.NotifyNewEvent()` (wake signal) → `SequencerPollingService` polls DB, fires `OnEvents` → `Outbox` receives via `Channel<ISeqEvt>` → `SubscribeReposEndpoints` encodes as CBOR and sends via WebSocket.

## Coding conventions to follow

- Match the existing namespace-to-folder structure.
- Prefer small constructor-injected classes over static helpers when working in the web host and service layers.
- Use existing config records and DI wiring instead of ad hoc environment reads.
- Reuse existing XRPC error types from `src/Pds/Pds.Xrpc/` for API-facing validation and protocol errors.
- Follow the Minimal API endpoint pattern: `static class {Feature}Endpoints` with a `Map{Feature}Endpoints(this RouteGroupBuilder group)` extension method and `static async Task<IResult> Handle(...)` action methods. Use `.WithMetadata(new SomeAuthAttribute())` for auth and `.RequireRateLimiting(...)` for rate limiting.
- Keep serialization compatible with the CarpaNet-generated lexicon models used by the server and tooling.
- When touching persistence, inspect related EF models, migrations, and repository code together.

## Front-end stack

- React 19 + TypeScript + Vite 8. Single SPA served from `src/Web/` via ASP.NET Core static files middleware: public site at `/`, admin UI at `/admin/*`.
- **TanStack Query v5** for all server state. `queryClient.ts` configures global defaults (30s stale, 1 retry), global `queryCache.onError` for 401 redirects, and `XrpcError` class. Mutations auto-invalidate related query keys on success.
- **No direct `xrpcGet`/`xrpcPost` calls from components.** All API interaction goes through domain hooks (`useAccountInfo`, `useSearchAccounts`, `useSubjectStatus`, `useDashboardStats`, `useInviteCodes`, etc.) in `hooks/useAccounts.ts`, `hooks/useInvites.ts`, `hooks/useDashboard.ts`.
- Add a new hook for any new endpoint. Follow the pattern: `useQuery`/`useInfiniteQuery` for reads, `useMutation` + `queryClient.invalidateQueries` for writes.
- The `client.ts` `request()` function throws `XrpcError` with `{ status, nsid, error, message }`. Catch errors from hooks via the `error` property (not try/catch around the hook call).
- `DidLink` component uses `useAccountInfo` hook (no module-level cache). TanStack Query deduplicates fetches by query key.

## Testing expectations

- Add or update TUnit tests when changing protocol logic, storage logic, parsing, or concurrency-sensitive behavior.
- Existing coverage is strongest in utility libraries and lighter in endpoint and sequencer code, so be proactive about adding tests in risky areas.
- If you change sequencing, backfill, or channel behavior, add tests around ordering, buffering, and slow-consumer paths.
- Use `[Test]` attribute (not `[Fact]`). Use `await Assert.That(...).IsEqualTo(...)` (not `Assert.Equal`).

## Known hotspots and gotchas

- **SQLite does not preserve `DateTimeKind`.** Values written as `DateTime.UtcNow` are read back as `DateTimeKind.Unspecified`. When compared against `DateTime.UtcNow` (Kind=Utc), .NET converts the Unspecified value from local time, shifting it by the timezone offset. This breaks cursor validation in `SubscribeReposEndpoints.cs` and will affect any `DateTime` comparison across a SQLite read boundary. Fix: add a value converter in `OnModelCreating` — `HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))`. Already applied to `SequencerDb.RepoSeqs.SequencedAt`.
- `src/Pds/Sequencer/Outbox.cs` is concurrency-sensitive. Careful with cutover, buffering, and channel completion behavior. The live event loop uses `Channel<ISeqEvt>` with backfill/cutover race handled via `ConcurrentQueue` + lock.
- `SequencerPollingService` lives in `src/Host/Services/` (not in the `Sequencer` project) because it's a `BackgroundService` hosted in the web host. It's registered as singleton + `ISequencerEventSource` + hosted service in `ServerConfig.cs`. Do NOT look for it under `src/Pds/Sequencer/`.
- `BackgroundJobQueue` and `BackgroundJobWorker` are registered in `Program.cs:RegisterPdsServices()`, NOT in `ServerConfig.RegisterServices()`. Look in both places when tracing DI.
- `src/Pds/AccountManager/Db/AccountStore.cs` contains user-input date parsing in account deactivation flows; treat that path carefully.
- This repo currently emits NuGet vulnerability warnings for some transitive packages; do not ignore new warnings without documenting why.
- The README lists several intentional limitations and TODOs. Review it before making protocol or data-model changes.
- The project is explicitly experimental; avoid broad refactors unless the task requires them.

## Files worth reading before major changes

- `readme.md`
- `src/Host/Program.cs`
- `src/Host/Configuration/ServerConfig.cs`
- `src/Host/Configuration/ServerEnvironment.cs`
- `src/Pds/AccountManager/`
- `src/Pds/ActorStore/`
- `src/Pds/Sequencer/`
- `src/Core/Repo/`

## Agent workflow guidance

- For most tasks, validate with `dotnet build BlueNilePds.slnx` and relevant `dotnet test` commands before finishing.
- Prefer surgical fixes over repo-wide rewrites.
- If you touch public XRPC behavior, review adjacent endpoints for consistency.
- If you add configuration, wire it through `ServerEnvironment` and `ServerConfig`.

## .NET context rules

Before making any .NET-specific claims (framework behavior, API availability, best practices):

1. **Check TFM first** — read `global.json`, `Directory.Build.props`, and the relevant `.csproj` to confirm the target framework. This project targets `net10.0` on ASP.NET Core.
2. **Frame advice around the detected TFM** — behavior differs between .NET Framework, .NET Core/.NET 5+, and .NET 10 preview. Do not apply legacy assumptions.
3. **ASP.NET Core has no `SynchronizationContext`** — `ConfigureAwait(false)` is a no-op in ASP.NET Core and should never be flagged as missing. This is the single most common .NET analysis mistake in ASP.NET Core codebases.
4. **Load the `dotnet-advisor` skill** before offering .NET analysis. It routes to domain skills that cover nuances like this.

When in doubt about whether a .NET claim is version-specific, state the TFM version alongside the claim.
