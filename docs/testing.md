# Testing

_Last reviewed: 2026-04-29_

## Framework

This repository uses **TUnit**.

Patterns used in tests:
- `[Test]`
- `await Assert.That(...).IsEqualTo(...)`
- `WebApplicationFactory<Program>` for integration tests

## Test projects

- `test/CID.Tests/`
- `test/Common.Tests/`
- `test/ActorStore.Tests/`
- `test/atompds.Tests/`
- `test/SubscribeTester/` (manual utility, not an automated test project)

## Current passing totals

Current passing automated tests:

- **469 total**
- **0 failed**
- **0 skipped**

Validated with:

```bash
dotnet test --solution atompds.slnx
```

## Important integration-test behavior

The integration test host now stubs external HTTP dependencies so tests remain deterministic and offline-friendly.

The test host fakes responses for:
- PLC operations and DID resolution
- hCaptcha verification
- disposable-email lookup
- crawler callback requests

This behavior lives in:
- `test/atompds.Tests/Infrastructure/TestWebAppFactory.cs`

## Useful commands

Run all automated tests:

```bash
dotnet test --solution atompds.slnx
```

Run only the server integration tests:

```bash
dotnet test --project test/atompds.Tests/atompds.Tests.csproj
```

Run a focused library test project:

```bash
dotnet test test/ActorStore.Tests/ActorStore.Tests.csproj
```

## Build hygiene

The solution currently builds cleanly with zero compiler warnings:

```bash
dotnet build atompds.slnx
```

## Notes for contributors

When changing:
- auth/session behavior
- handle validation
- sequencing behavior
- persistence logic
- repo write semantics

add or update tests in the nearest relevant test project.
