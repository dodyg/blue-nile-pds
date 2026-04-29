# MEMORY.md -- Codebase Reference

_Last reviewed: 2026-04-29_

This file is a lightweight orientation map for `blue-nile-pds`.

## Project summary

`blue-nile-pds` is a .NET 10 ATProto PDS implementation in C#. It is experimental, learning-focused, and organized as a multi-project solution.

## Solution layout

- `src/atompds/` — ASP.NET Core host
- `src/pds_projects/` — PDS service libraries
- `src/projects/` — shared/core libraries
- `src/pdsadmin/` — admin CLI
- `src/migration/` — migration utility
- `test/` — automated tests and support tooling

## Key host entry points

- `src/atompds/Program.cs`
- `src/atompds/Config/ServerEnvironment.cs`
- `src/atompds/Config/ServerConfig.cs`
- `src/atompds/Endpoints/EndpointRegistration.cs`

## Middleware summary

Current order:
1. exception handler
2. CORS
3. WebSockets
4. routing
5. optional rate limiter
6. auth middleware
7. not-found logging middleware
8. endpoints

## Important subsystems

### Account management
- global SQLite database
- session creation / refresh / revoke
- invite handling
- email tokens and app passwords

### Actor store
- per-actor SQLite storage
- repo and record indexing
- blob linkage

### Sequencer
- stores events in SQLite
- publishes to subscribers
- crawler notifications

### Identity / DID / Handle
- DID resolution
- PLC operations
- handle validation
- service-domain checks

## Configuration notes

- config binds from the `Config` section
- required local secrets include JWT secret and PLC rotation key
- DID cache TTL semantics are now consistent and validated
- some fields remain documented as currently unused; see `docs/configuration.md`

## Testing notes

- framework: **TUnit**
- automated test total: **469 passing, 0 skipped**
- integration tests use fake external HTTP responses for PLC and related services

## Useful commands

```bash
dotnet build atompds.slnx
dotnet test --solution atompds.slnx
dotnet run --project src/atompds/atompds.csproj
dotnet run --project src/pdsadmin/pdsadmin.csproj
```

## Additional docs

- `docs/architecture.md`
- `docs/configuration.md`
- `docs/testing.md`
- `docs/known-issues.md`
- `AUDIT-CHECKLIST.md`
