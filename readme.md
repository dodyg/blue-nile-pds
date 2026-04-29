# blue-nile-pds

`blue-nile-pds` is an experimental .NET 10 ATProto PDS implementation in C#. It is a fork of `atompds`, updated to build and run on the modern .NET stack, with additional test coverage and maintenance work focused on correctness and learnability.

## Status

- Experimental and learning-focused
- Targets `.NET 10`
- Uses ASP.NET Core Minimal APIs
- Uses SQLite for account, sequencer, and per-actor repo storage
- Uses TUnit for tests

This repository is **not production-ready**. Treat it as a protocol-learning and implementation-study project.

## What is in the repo?

- `src/atompds/` — web host, middleware, endpoint registration, startup
- `src/pds_projects/` — PDS-specific services such as account management, actor storage, sequencing, blob storage, mail, and XRPC errors
- `src/projects/` — lower-level shared libraries such as CID, crypto, DID, identity, handle validation, and repo/MST logic
- `src/pdsadmin/` — admin CLI
- `src/migration/` — actor-store migration utility
- `test/` — unit and integration tests

See also:
- `docs/architecture.md`
- `docs/configuration.md`
- `docs/testing.md`
- `docs/known-issues.md`
- `MEMORY.md`

## Prerequisites

- .NET SDK `10.0.100` or compatible `10.0.x` SDK
- SQLite available via bundled packages
- Required secrets for local runtime config

The repo pins the SDK in `global.json`.

## Build

From the repository root:

```bash
dotnet build atompds.slnx
```

## Test

Run all tests:

```bash
dotnet test --solution atompds.slnx
```

Run the main integration suite only:

```bash
dotnet test --project test/atompds.Tests/atompds.Tests.csproj
```

## Run the server

```bash
dotnet run --project src/atompds/atompds.csproj
```

## Run the admin CLI

```bash
dotnet run --project src/pdsadmin/pdsadmin.csproj
```

## Local configuration

Start from:

- `src/atompds/appsettings.Development.json.example`

Create a local `appsettings.Development.json` and fill in at least:

- `PDS_JWT_SECRET`
- `PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX`
- blob storage paths

Do **not** commit local secrets. The repo ignores local `appsettings.*.json` files for the host.

## Highlights from the latest audit work

- Refresh token type handling is now consistent
- Service-domain handle validation now correctly supports domains like `.test`
- Integration tests no longer rely on live external PLC or disposable-email services
- Vulnerable package warnings were resolved
- DID cache TTL naming and mapping were corrected
- The solution builds cleanly and the test suite passes

## Known limitations

- The project is still experimental
- Some configuration fields remain documented as unused compatibility/planned fields
- Background jobs are best-effort and use a bounded in-memory queue
- The migration tool remains utility-grade rather than operator-grade

See `docs/known-issues.md` for current caveats.

## Inspiration / related projects

- Original upstream: `atompds`
- Other PDS implementations: <https://github.com/threddyrex/atproto-links?tab=readme-ov-file>
