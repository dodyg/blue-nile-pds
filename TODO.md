# TODO.md — Consolidated Prioritized Work

_Generated 2026-07-20 from FIX.md, GAPS.md, GAPS-TODO.md, BIG-GAP.md, AUDIT-CHECKLIST.md._

Only items that are **still open** are listed. Cross-ref IDs in `[brackets]`.

---

## P0 — Security & Correctness (Fix First)

| ID | Item | Why | File(s) |
|----|------|-----|---------|
| **[FIX-C1]** | Hardcoded admin password defaults to `"secret"` | Any client can gain full admin access | `ServerConfig.cs:315`, `ServerEnvironment.cs` |
| **[FIX-C3]** | CORS middleware after routing — preflight `OPTIONS` hits 401 before CORS headers | Breaks browser-based clients | `Program.cs:57-81` |
| **[FIX-C4]** | `_caughtUp` bool in Outbox read/written without sync — firehose reordering | Data integrity | `Outbox.cs:133-149` |
| **[NU1903]** | `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 has high-severity vuln (GHSA-2m69-gcr7-jv3q) | Known CVE | `test/ActorStore.Tests/ActorStore.Tests.csproj` |
| **[FIX-C5]** | No CI/CD pipeline | No automated validation | `.github/workflows/` (new) |

## P2 — Security Hardening

| ID | Item | Why | File(s) |
|----|------|-----|---------|
| **[FIX-H2]** | SSRF protection flag exists but is never enforced — proxy makes HTTP requests to DID-resolved URLs | SSRF vector | `AppViewProxyEndpoints.cs:193-194`, `ProxyConfig.cs` |
| **[FIX-H3]** | OAuth `redirect_uri` not validated against allowlist | Open redirect | `OAuthAuthorizeEndpoints.cs:48,98` |
| **[FIX-H4]** | `.Result` blocking in `RefreshSessionEndpoints.cs` | Sync-over-async, thread pool starvation | `RefreshSessionEndpoints.cs:46-47` |
| **[FIX-H5]** | `SequencerRepository` scoped (per-request) but spawns `Task.Run(PollTaskAsync)` in constructor | Creates runaway polling tasks | `SequencerRepository.cs:35`, `ServerConfig.cs:324` |
| **[FIX-H6]** | No audit logging for account delete, deactivate, password/email changes | Ops safety | `AccountStore.cs`, `DeleteAccountEndpoints.cs` |
| **[FIX-H7]** | Proxy logs full response bodies at `Information` level | PII exposure | `AppViewProxyEndpoints.cs:224,256,301` |
| **[FIX-H8]** | `services.BuildServiceProvider()` anti-pattern for SmtpMailer DI | Creates duplicate container | `ServerConfig.cs:343` |
| **[FIX-H1]** | Rate limiting disabled by default (`PDS_RATE_LIMITS_ENABLED=false`) | No brute-force protection | `ServerEnvironment.cs:118` |

## P3 — Protocol & Infrastructure Gaps

| ID | Item | Phase (GAPS-TODO) | File(s) |
|----|------|-------------------|---------|
| **[T-08]** | CAR streamable block ordering (MST pre-order DFS) | Phase 2 — Protocol | `MST.cs`, `SqlRepoTransactor.cs` |
| **[T-10]** | Commit re-signing (`resignCommit` for key rotation) | Phase 2 — Protocol | `Repo.cs` |
| **[T-11]** | Blob constraints enforcement (MIME/size per lexicon field) | Phase 2 — Protocol | `Prepare.cs`, `RepoRepository.cs` |
| **[T-17]** | Dedicated chat service routing (`PDS_CHAT_SERVICE_URL`/`DID`) | Phase 3 — Routing | `AppViewProxyEndpoints.cs` |
| **[T-18]** | Protected/privileged method enforcement in proxy (`PROTECTED_METHODS`, `PRIVILEGED_METHODS`) | Phase 3 — Routing | `AppViewProxyEndpoints.cs` |
| **[T-20]** | Moderator auth / service JWT auth (required for Ozone) | Phase 4 — Auth | `AuthVerifier.cs` |
| **[T-21]** | Takendown auth scope (`com.atproto.takendown`) | Phase 4 — Auth | `AuthVerifier.cs` |
| **[T-22]** | Entryway PLC rotation key + admin token config | Phase 4 — Auth | `ServerEnvironment.cs`, `AuthVerifier.cs` |
| **[T-23]** | Restore commented-out DB tables (used_refresh_token, device, authorization_request, etc.) | Phase 5 — Database | Migration (new) |
| **[T-24]** | Persistent DID cache via SQLite (stale-while-revalidate) | Phase 5 — Database | `src/projects/Identity/` |
| **[T-25]** | OAuth persistence (SQLite-backed sessions, codes, clients) | Phase 6 — OAuth | `OAuthSessionStore.cs` |
| **[T-26]** | OAuth PAR (pushed authorization requests) | Phase 6 — OAuth | `Endpoints/OAuth/OAuthParEndpoint.cs` (new) |
| **[T-29]** | Rate limit bypass (`x-ratelimit-bypass` header, bypass IPs) | Phase 7 — Rate Limits | `RateLimitMiddleware.cs` |
| **[T-30]** | Redis-backed distributed rate limiting | Phase 7 — Rate Limits | `RateLimitMiddleware.cs` |
| **[T-31]** | Remaining missing config vars (14 vars from GAPS.md §5.1) | Phase 7 — Config | `ServerEnvironment.cs` |
| **[T-36]** | Recovery & maintenance scripts (rebuild-repo, publish-identity, rotate-keys, sequencer-recovery) | Phase 8 — Infra | `src/pdsadmin-cli/` |
| **[T-32]** | Blob GC is implemented; verify schedule config and distributed lock | Phase 8 — Infra | `BlobGarbageCollectionService.cs` |
| **[T-33]** | Separate moderation mailer (`PDS_MODERATION_EMAIL_SMTP_URL`) | Phase 8 — Infra | `src/pds_projects/Mailer/` |
| **[T-34]** | HTML email templates (Handlebars or Razor) | Phase 8 — Infra | `src/pds_projects/Mailer/Templates/` |
| **[T-35]** | Handle backup nameservers (`PDS_HANDLE_BACKUP_NAMESERVERS`) | Phase 8 — Infra | `HandleManager.cs` |
| **[T-37]** | Docker + compose exist; add `installer.sh` and health checks | Phase 9 — Deploy | `Dockerfile`, `compose.yaml` |
| **[T-38]** | Graceful shutdown with queue drain | Phase 9 — Deploy | `BackgroundJobQueue.cs` |
| **[T-39]** | Missing integration test areas (10 test files from GAPS.md §9.1) | Phase 10 — Tests | `test/atompds.Tests/` |

## P4 — Cleanup & Tech Debt

| ID | Item | File(s) |
|----|------|---------|
| **[FIX-M1]** | Config records missing `required` keyword (9 CS8618 warnings) | `DatabaseConfig.cs`, `ActorStoreConfig.cs`, etc. |
| **[FIX-M2]** | JWT `exp` claim not validated in bearer token path | `AuthVerifier.cs:612-641` |
| **[FIX-M3]** | SQLite WAL mode + busy_timeout not set on global DBs (already set in Program.cs for AccountManagerDb/SequencerDb — verify) | `ServerConfig.cs` |
| **[FIX-M4]** | Password strength enforcement (min length, not empty) | `CreateAccountEndpoints.cs:219`, `ResetPasswordEndpoints.cs:19-26` |
| **[FIX-M5]** | BlobStore path traversal — DID used unsanitized in `Path.Join` | `DiskBlobStore.cs:36,46-47` |
| **[FIX-M6]** | Exception swallowing (15+ locations with bare `catch`, no logging) | Across multiple files |
| **[FIX-M7]** | Raw `Exception` throws bypassing XRPC error formatting (5 locations) | `ApplyWritesEndpoints.cs`, `AuthVerifier.cs`, etc. |
| **[FIX-M8]** | Health endpoint is a no-op (static version string, no DB check) | `HealthEndpoints.cs:14-17` |
| **[FIX-M9]** | No observability (OpenTelemetry, metrics, tracing, correlation IDs) | Codebase-wide |
| **[FIX-M10]** | Unused NuGet packages (Newtonsoft.Json, System.Drawing.Common, Scalar.AspNetCore, Microsoft.AspNetCore.OpenApi) | `Directory.Packages.props`, `atompds.csproj` |
| **[FIX-M11]** | AGENTS.md incorrectly references xUnit (should be TUnit) | `AGENTS.md` |
| **[FIX-M12]** | Background job DI split — registrations in `Program.cs` instead of `ServerConfig.RegisterServices()` | `Program.cs:49-53` |
| **[FIX-M13]** | HTTP logging configured but middleware commented out | `Program.cs:37-42,85` |

## Quick Reference: Execution Order

```
P0 (Security)     → C1 → C3 → C4 → NU1903 → C5
P2 (Hardening)    → H3 → H2 → H4 → H5 → H6 → H7 → H8 → H1
P3 (Protocol)     → T-08 → T-10 → T-11 → T-17 → T-18 → T-20 → T-21 → T-22 → T-23 → T-24 → T-25 → T-26 → T-29 → T-30 → T-31 → T-36 → T-32..38 → T-39
P4 (Cleanup)      → M1..M13 (can parallel within P3)
```

**Hard dependencies:**
- H3 (OAuth redirect) blocks T-25/OAuth persistence
- T-20 (moderator auth) blocks T-16/Ozone proxy usage
- T-23 (DB tables) blocks T-25/OAuth persistence and T-26/PAR
- T-24 (DID cache SQLite) depends on schema from T-23
