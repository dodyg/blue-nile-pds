# AGENTS.md

## Project summary

`blue-nile-pds` is a .NET 10 preview fork of `atompds`, a proof-of-concept ATProto PDS implementation in C#. Treat it as experimental and learning-focused, not production-ready. Preserve behavior unless the task explicitly requires protocol or storage changes.

## Solution layout

- `src/atompds/`: ASP.NET Core host, controllers, middleware, startup, config binding.
- `src/pds_projects/`: PDS-specific services such as `AccountManager`, `ActorStore`, `BlobStore`, `Sequencer`, `Mailer`, and `Xrpc`.
- `src/projects/`: lower-level libraries such as `CID`, `Common`, `Crypto`, `DidLib`, `Handle`, `Identity`, and `Repo`.
- `src/pdsadmin/`: admin CLI.
- `src/migration/`: batch migration utility for actor stores.
- `test/`: xUnit test projects plus `SubscribeTester`.
- `atompds.slnx`: root solution file. Use this for solution-wide build and test commands.

## Environment and prerequisites

- SDK is pinned in `global.json` to `.NET 10.0.100-rc.1`.
- Projects target `net10.0`.
- `src/atompds/atompds.csproj` enables `LangVersion=preview`, nullable reference types, and invariant globalization.
- Do not downgrade language or framework features unless the task explicitly requires it.

## Build, run, and test

Run commands from the repository root unless a task clearly needs a project directory.

```bash
dotnet build atompds.slnx
dotnet test atompds.slnx
dotnet run --project src/atompds/atompds.csproj
dotnet run --project src/pdsadmin/pdsadmin.csproj
```

Useful focused test commands:

```bash
dotnet test test/CID.Tests/CID.Tests.csproj
dotnet test test/Common.Tests/Common.Tests.csproj
dotnet test test/ActorStore.Tests/ActorStore.Tests.csproj
```

For dependency hygiene:

```bash
dotnet list atompds.slnx package --vulnerable --include-transitive
```

## Configuration

- Runtime config is bound from the `Config` section into `src/atompds/Config/ServerEnvironment.cs`.
- Start from `src/atompds/appsettings.Development.json.example`.
- `**/atompds/appsettings.*.json` is gitignored; do not commit local secrets.
- `PDS_JWT_SECRET` and `PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX` are required.
- `ServerConfig` expands `~/` paths and creates missing data directories.
- `pdsadmin` uses `src/pdsadmin/pdsenv.json`; avoid committing real credentials there.

## Architecture notes

- Dependency registration lives in `src/atompds/Config/ServerConfig.cs`.
- Startup pipeline lives in `src/atompds/Program.cs`.
- Controllers are organized by XRPC namespace under `src/atompds/Controllers/Xrpc/...`.
- JSON uses `System.Text.Json` with CarpaNet-generated serializer contexts where ATProto models are involved.
- `AccountManagerDb` and `SequencerDb` migrations run automatically on app startup.
- Actor repos are stored per DID via `ActorRepositoryProvider` in `src/pds_projects/ActorStore/`.

## Coding conventions to follow

- Match the existing namespace-to-folder structure.
- Prefer small constructor-injected classes over static helpers when working in the web host and service layers.
- Use existing config records and DI wiring instead of ad hoc environment reads.
- Reuse existing XRPC error types from `src/pds_projects/Xrpc/` for API-facing validation and protocol errors.
- Follow existing controller patterns: `[ApiController]`, `[Route("xrpc")]`, explicit action names matching ATProto endpoints.
- Keep serialization compatible with the CarpaNet-generated lexicon models used by the server and tooling.
- When touching persistence, inspect related EF models, migrations, and repository code together.

## Testing expectations

- Add or update xUnit tests when changing protocol logic, storage logic, parsing, or concurrency-sensitive behavior.
- Existing coverage is strongest in utility libraries and lighter in controllers and sequencer code, so be proactive about adding tests in risky areas.
- If you change sequencing, backfill, or channel behavior, add tests around ordering, buffering, and slow-consumer paths.

## Known hotspots and gotchas

- This repo currently emits NuGet vulnerability warnings for some transitive packages; do not ignore new warnings without documenting why.
- `src/pds_projects/Sequencer/Outbox.cs` is concurrency-sensitive. Be careful with cutover, buffering, and channel completion behavior.
- `src/pds_projects/AccountManager/Db/AccountStore.cs` contains user-input date parsing in account deactivation flows; treat that path carefully.
- The README lists several intentional limitations and TODOs. Review it before making protocol or data-model changes.
- The project is explicitly experimental; avoid broad refactors unless the task requires them.

## Files worth reading before major changes

- `readme.md`
- `src/atompds/Program.cs`
- `src/atompds/Config/ServerConfig.cs`
- `src/atompds/Config/ServerEnvironment.cs`
- `src/pds_projects/AccountManager/`
- `src/pds_projects/ActorStore/`
- `src/pds_projects/Sequencer/`
- `src/projects/Repo/`

## Agent workflow guidance

- For most tasks, validate with `dotnet build atompds.slnx` and relevant `dotnet test` commands before finishing.
- Prefer surgical fixes over repo-wide rewrites.
- If you touch public XRPC behavior, review adjacent controllers for consistency.
- If you add configuration, wire it through `ServerEnvironment` and `ServerConfig`.
