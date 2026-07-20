# TODO.md — Remaining Work Items

_Generated 2026-07-07 by cross-referencing GAPS.md, GAPS-TODO.md, FIX.md, BIG-GAP.md, AUDIT-CHECKLIST.md against the current codebase._

Only items that are **still open, partially done, or newly discovered** are listed. Items confirmed done are omitted.

---

## P0 — Security & Correctness

### [NEW] NuGet vulnerability (NU1903)
**File:** `test/ActorStore.Tests/ActorStore.Tests.csproj`
- `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 has known high-severity vulnerability (GHSA-2m69-gcr7-jv3q).
- Update the package or suppress with documented justification.

---

## P1 — Missing Sync Endpoints

### [GAP-01] `com.atproto.sync.getCheckout`
Not implemented. No endpoint, no handler. The canonical PDS returns a CAR file containing the repo at a specific version.

### [GAP-02] `com.atproto.sync.getHead`
Not implemented. `com.atproto.sync.getLatestCommit` exists as a near-equivalent but is not the same protocol method.

---

## P2 — Not-Started Features (from GAPS-TODO.md)

### [T-07] subscribeRepos verified mode
**File:** `src/atompds/Endpoints/Xrpc/Com/Atproto/Sync/SubscribeReposEndpoints.cs`
No commit validation or content verification on the subscriber side. Events streamed as-is from sequencer.

### [T-08] rebaseRepo
Not implemented anywhere in the codebase.

### [T-21] ModerationService
No local `ModerationService` class or interface exists. `CreateReport` is a remote proxy only.

### [T-28] Sequencer ack
No ack/nack mechanism. Outbox uses `Channel<ISeqEvt>` without delivery confirmation.

### [T-31] Firehose filtering
No collection-based filtering on `subscribeRepos`. All event types are streamed.

### [T-33] Identity binding check on writes
Auth verifies JWT but does not explicitly verify the authenticated DID matches the target repo.

### [T-35] Resource limits per DID
Rate limiting is IP-based only. No per-DID limits (max records, blob storage, connections).

### [T-38] Metrics instrumentation
No `Meter`, `Counter`, `Histogram`, or OpenTelemetry usage anywhere.

---

## P3 — Partially Done Features

### [T-11] Account recovery email
Account deletion email is done. Account recovery (hacked/compromised account via email) is not implemented.

### [T-22] Moderation reporting (local)
`com.atproto.moderation.createReport` exists but is proxy-only. No local report storage, processing, or moderation queue.

### [T-27] Label propagation
Proxy pass-through for `atproto-accept-labelers` headers exists. No local label service, emission, or subscription.

### [GAP-03] Sequencer notarization (BIG-GAP #3)
All sequencer events are unsigned CBOR with no cryptographic commitment or identity binding. Fully open.

---

## P4 — Missing Features (from GAPS.md)

| Feature | Status |
|---------|--------|
| Custom domain `did.json` serving | Not implemented (`WellKnownEndpoints.cs` is host-based only) |
| Sign-in code auth mechanism | Not implemented |
| `X-RateLimit-*` response headers | Not added (`RateLimitMiddleware.cs` sets only `RetryAfter`) |
| `nodeinfo` endpoint | Not implemented |
| On-demand blob deletion endpoint (`com.atproto.repo.deleteBlob`) | Not implemented (GC-only) |
| Account deletion recovery / undelete | Not implemented |
| Seq-ack mechanism for subscribers | Not implemented (cursor-based backfill only) |
| OIDC identity provider integration | Not implemented (only `plc`/`web` DID methods) |

---

## P5 — CI/CD & Tooling

### [FIX-C5] CI/CD pipeline
No `.github/workflows/` directory exists. No automated build, test, or publish workflows.

---

## P6 — Documentation Staleness

### [DOC-01] AUDIT-CHECKLIST.md
- Claims "compiler warnings: 0" — actual is 20.
- Claims "vulnerable package report: clean" — NU1903 exists.
- Update summary and bump `_Last reviewed` date.

### [DOC-02] GAPS.md
Outdated claims to fix:
- "`admin.searchAccounts` missing" → ✅ exists
- "`tools.ozone.*` proxy missing" → ✅ exists
- "No app password auth" → ✅ fully implemented
- "Stub Mailer only, no SMTP" → ❌ `SmtpMailer.cs` exists
- "`listRecords` missing reverse param" → ✅ exists

### [DOC-03] FIX.md
Resolved items should be moved to a resolved section:
- C1, C2, C3, C4, H5, M4, M11 are all fixed.
- C5 remains open.

### [DOC-04] BIG-GAP.md
Gaps 1, 2, 4, 5 are partially addressed and should reflect current state (Consumer.cs verification code, SmtpMailer, real get/putPreferences, Dockerfile + compose.yaml).

### [DOC-05] GAPS-TODO.md
10 items are now done and should be marked as such:
- T-01, T-02, T-03, T-04, T-05, T-09, T-14, T-25, T-34/T-01d, T-37.
