# Big functional gaps vs canonical Bluesky PDS

This review compares this repository with the canonical Bluesky PDS distribution (`bluesky-social/pds`) and the implementation it packages from `bluesky-social/atproto/packages/pds`. It intentionally ignores small endpoint, formatting, and operational differences that do not materially affect hosting a PDS.

## 1. Repo sync verification and verifiable firehose data are incomplete

The canonical implementation verifies imported repo CARs and sync diffs, verifies commit signatures, and computes `relevantBlocks`/covering proofs for writes. Those pieces matter because other ATProto services and relays need to trust repository history, imported repos, and firehose commit slices.

Local evidence:

- `src/projects/Repo/Types.cs` defines `CommitData` with `NewBlocks` and `RemovedCids`, but no `RelevantBlocks` equivalent.
- `src/projects/Repo/Repo.cs` formats commits from MST diffs and added leaves, but does not call an MST covering-proof API or populate relevant proof blocks.
- `src/projects/Repo/Util.cs` serializes whatever blocks are in a `BlockMap`; it does not distinguish full new blocks from a verifiable minimal proof set.
- `src/atompds/Endpoints/Xrpc/Com/Atproto/Repo/ImportRepoEndpoints.cs` parses a CAR and writes every block into storage, but does not verify the repo root, DID, signing key, MST reachability, or update account repo root/sequencing from the imported commit.
- There are no local matches for `verifyRepo`, `verifyDiff`, `verifyProof`, `getCoveringProof`, or `relevantBlocks` under `src/`.

Canonical reference:

- `packages/repo/src/sync/consumer.ts` implements `verifyRepoCar`, `verifyRepo`, `verifyDiffCar`, `verifyDiff`, `verifyProofs`, and `verifyRecords`.
- `packages/repo/src/repo.ts` computes covering proofs with `data.getCoveringProof(...)` and returns `relevantBlocks` in `CommitData`.

Impact: this code can produce and accept repository data that is not equivalent to canonical PDS sync semantics. Repo import/migration and downstream firehose verification are the biggest risks.

## 2. Record validation and blob constraints are not equivalent

Canonical PDS validates records against known lexicons, validates collection/rkey rules, computes CIDs with canonical lex-CBOR, enumerates blobs with legacy-blob handling, and returns `valid`/`unknown` validation status based on real validation.

Local evidence:

- `src/pds_projects/ActorStore/Repo/Prepare.cs` has explicit TODOs for schema validation and currently sets `ValidationStatus.Unknown`.
- `Prepare.CidForSafeRecord()` says "This is probably not in any way correct" and hashes a generic JSON-to-CBOR conversion instead of the canonical lex-CBOR path.
- Blob extraction in `Prepare.ExtractBlobReferences()` has a TODO for constraints and does not enforce blob accept/max-size constraints from schemas.
- `src/atompds/Endpoints/Xrpc/Com/Atproto/Repo/ApplyWritesEndpoints.cs` returns validation status from those prepared writes, so write APIs can report `Unknown` for records that canonical PDS would validate or reject.

Canonical reference:

- `packages/pds/src/repo/prepare.ts` validates records against known schemas, rejects invalid record keys and legacy blobs, enumerates blob refs with `enumBlobRefs`, and computes CIDs with `cidForCbor(encode(record))`.

Impact: clients can write invalid records or records with non-canonical CIDs. This is a protocol-level gap, not just missing polish.

## 3. OAuth provider behavior is only a lightweight in-memory approximation

Canonical PDS has a database-backed OAuth provider store for authorization requests, devices, authorized clients, lexicons, access/refresh tokens, token rotation, and refresh-token reuse detection. Local OAuth is substantially simpler.

Local evidence:

- `src/atompds/Services/OAuth/OAuthSessionStore.cs` stores authorizations and codes in `ConcurrentDictionary`, so authorization state is lost on process restart.
- `src/atompds/Endpoints/OAuth/OAuthTokenEndpoints.cs` issues self-contained refresh JWTs and validates them statelessly; there is no persisted token record, revocation, token rotation state, used-refresh-token tracking, or device/client record.
- `src/atompds/Endpoints/OAuth/OAuthClientMetadataEndpoints.cs` redirects directly to any `http://` or `https://` `client_id` instead of resolving and validating client metadata with the canonical safety model.
- DPoP proof checks exist in `src/atompds/Middleware/AuthVerifier.cs`, but there is no durable nonce/replay/device/token store comparable to the canonical OAuth provider backing store.

Canonical reference:

- `packages/pds/src/account-manager/oauth-store.ts` implements `AccountStore`, `RequestStore`, `DeviceStore`, `LexiconStore`, and `TokenStore`, including device-account links, authorization requests, token creation/deletion/rotation, and used-refresh-token tracking.

Impact: ATProto OAuth clients may work in simple flows, but restart behavior, revocation, rotation, device/session management, and client metadata safety are not functionally equivalent.

## 4. App-private state and proxy behavior are not fully canonical

Canonical PDS includes a small set of `app.bsky.*` private-state endpoints and robust proxying to appview/moderation services. Local proxying covers many read routes and `com.atproto.moderation.createReport`, but some canonical user-facing state is stubbed.

Local evidence:

- `src/atompds/Endpoints/Xrpc/AppViewProxyEndpoints.cs` returns an empty preference list for `app.bsky.actor.getPreferences` and makes `app.bsky.actor.putPreferences` a no-op.
- The same file has a catchall proxy limited to `app.bsky.*`, `chat.bsky.*`, and `com.atproto.moderation.*`; it does not provide broad canonical routing for other service families such as `tools.ozone.*`.
- `src/atompds/Endpoints/Xrpc/Com/Atproto/Moderation/CreateReportEndpoints.cs` proxies report creation when report-service config is present, but moderation/admin-service proxy parity beyond that is limited.

Canonical reference:

- `packages/pds/src/api/app/bsky/actor/index.ts` wires real `getPreferences` and `putPreferences` handlers.
- `packages/pds/src/pipethrough.ts` is the canonical catchall/proxy layer for appview/moderation service routing, auth, headers, and service-DID targeting.

Impact: Bluesky clients backed by this PDS will lose preference state, and some moderation/appview service routes available through canonical PDS may fail or route differently. This is less core than repo/OAuth correctness, but still a major canonical-hosting gap.

## 5. Canonical deployment packaging is missing

Canonical `bluesky-social/pds` is not just application code; it ships a production-oriented deployment wrapper with Docker/Caddy, environment templates, expected ports, volumes, and operational defaults. This repository currently has run commands and appsettings examples, but no equivalent host package.

Local evidence:

- No local `Dockerfile`, `compose.yaml`, `docker-compose.yml`, `Caddyfile`, `sample.env`, or `.env.example` was found.
- Runtime configuration exists in `src/atompds/appsettings.Development.json.example`, but it is not equivalent to the canonical host bundle for public DNS/TLS/websocket deployment.

Canonical reference:

- `bluesky-social/pds/compose.yaml` runs `ghcr.io/bluesky-social/pds` with Caddy and persistent `/pds` volumes.
- `bluesky-social/pds/sample.env` documents the required public hostname, JWT/admin/PLC secrets, blobstore, appview/report service, crawler, SMTP, invite, and rate-limit settings.

Impact: even if the application runs locally, it is not packaged or documented as a drop-in canonical PDS host. Operators would need to design their own TLS, websocket, persistence, environment, and upgrade workflow.

## Important non-gap noted during review

The admin password is not currently a production hardcoded-secret gap: `src/atompds/Config/ServerConfig.cs` only defaults `PDS_ADMIN_PASSWORD` to `secret` in dev mode and throws when it is absent outside dev mode.
