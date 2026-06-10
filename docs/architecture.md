# Architecture

_Last reviewed: 2026-04-29_

## Overview

`blue-nile-pds` is split into three layers:

1. **Host layer** — `src/atompds/`
2. **PDS service layer** — `src/pds_projects/`
3. **Core/shared libraries** — `src/projects/`

## Host layer

The host is an ASP.NET Core Minimal API application.

Key files:
- `src/atompds/Program.cs`
- `src/atompds/Config/ServerConfig.cs`
- `src/atompds/Config/ServerEnvironment.cs`
- `src/atompds/Endpoints/EndpointRegistration.cs`

Responsibilities:
- bind configuration
- register DI services
- apply middleware
- register endpoints
- run EF migrations for account and sequencer databases

## Service layer

### AccountManager

Global account database and auth/session logic.

### ActorStore

Per-actor SQLite repositories, record indexing, blob linkage, and repo write application.

### Sequencer

Stores sequenced events and fans them out to subscribers.

### BlobStore

Abstracts disk or S3 blob storage.

### Xrpc

Defines reusable XRPC error types and response conventions.

## Core/shared libraries

### CID / Common / Repo

Low-level content addressing, CBOR helpers, and repo/MST logic.

### Crypto / DidLib / Identity / Handle

Key management, DID operations, DID resolution, and handle validation.

## HTTP surface

The server exposes Minimal API endpoints under:

- root endpoints
- `.well-known` endpoints
- OAuth endpoints
- `xrpc/` ATProto endpoints
- AppView proxy endpoints

Endpoint registration is centralized in:
- `src/atompds/Endpoints/EndpointRegistration.cs`

## Middleware order

Current pipeline shape in `Program.cs`:

1. exception handler
2. CORS
3. WebSockets
4. routing
5. optional rate limiting
6. auth middleware
7. not-found logging middleware
8. endpoint execution

This order is intentional so auth and endpoint exceptions flow through the exception handler, WebSocket support is enabled before endpoint execution, and 404 logging happens after the request pipeline completes.

## Storage model

### Global SQLite databases

- account database
- sequencer database

### Per-actor SQLite databases

Each actor repo is stored in a per-DID SQLite database under the actor-store directory.

## Background work

Background jobs use a bounded in-memory queue:

- capacity: 1000
- mode: `DropOldest`
- queue pressure now emits warnings when capacity is reached

This queue is best-effort and currently suited for non-durable background work such as email dispatch and crawler notifications.

## Testing strategy summary

- unit tests for low-level libraries
- integration tests through `WebApplicationFactory<Program>`
- test host now uses fake external HTTP responses for PLC, hCaptcha, disposable-email lookup, and crawler callbacks

For commands and details, see `docs/testing.md`.
