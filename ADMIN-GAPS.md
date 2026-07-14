# ADMIN-GAPS.md — Admin Features vs ATProto Lexicons

Comparison of admin features in `blue-nile-pds` (CLI `src/pdsadmin-cli/` and Web UI `src/pdsadmin-web/`) against ATProto Lexicon specs for `com.atproto.admin.*` and `com.atproto.server.*`.

---

## 1. Endpoint Coverage

### 1.1 Admin Endpoints (`com.atproto.admin.*`)

| # | Endpoint | Backend | CLI | Web UI | Notes |
|---|----------|---------|-----|--------|-------|
| 1 | `getAccountInfo` | ✅ | ✅ `info` | ✅ AccountDetail | |
| 2 | `getAccountInfos` | ✅ | ✅ `list` | ❌ | Bulk fetch; web has no use |
| 3 | `searchAccounts` | ✅ | ❌ | ✅ Accounts page | |
| 4 | `deleteAccount` | ✅ | ✅ `admin-delete` | ✅ AccountDetail | |
| 5 | `enableAccountInvites` | ✅ | ✅ `enable-invites` | ✅ AccountDetail | |
| 6 | `disableAccountInvites` | ✅ | ✅ `disable-invites` | ✅ AccountDetail | |
| 7 | `getInviteCodes` | ✅ | ❌ | ✅ InviteCodes page | |
| 8 | `disableInviteCodes` | ✅ | ❌ | ✅ InviteCodes page | |
| 9 | `getSubjectStatus` | ✅ | ❌ | ✅ SubjectStatus page | |
| 10 | `updateSubjectStatus` | ✅ | ✅ `takedown`/`untakedown` | ✅ SubjectStatus + Detail | |
| 11 | `sendEmail` | ✅ | ❌ | ❌ | Admin can email users |
| 12 | `updateAccountEmail` | ✅ | ✅ `update-email` | ✅ AccountDetail modal | |
| 13 | `updateAccountHandle` | ✅ | ✅ `update-handle` | ✅ AccountDetail modal | |
| 14 | `updateAccountPassword` | ✅ | ✅ `reset-password` | ✅ AccountDetail modal | |
| 15 | `updateAccountSigningKey` | ❌ **Missing** | ❌ | ❌ | Lexicon defined, no handler |

**Coverage:** 14/15 backend (93%), 11/15 web UI (73%), 8/15 CLI (53%), 1 entirely missing endpoint.

### 1.2 Server Endpoints (`com.atproto.server.*`) — Admin-Adjacent

| Endpoint | Backend | CLI | Web UI | Notes |
|----------|---------|-----|--------|-------|
| `createInviteCode` | ✅ | ❌ Stub | ✅ CreateInviteCodes | AdminToken auth, validates InvitesDisabled |
| `createInviteCodes` | ✅ | ❌ | ✅ CreateInviteCodes | ForAccounts wired, validates InvitesDisabled |
| `requestAccountDelete` | ✅ | ❌ | ❌ | Triggers email |
| `deactivateAccount` | ✅ | ❌ | ❌ | Account lifecycle |
| `activateAccount` | ✅ | ❌ | ❌ | Account lifecycle |
| `checkAccountStatus` | ✅ | ❌ | ❌ | Migration status |

---

## 2. Web UI Feature Gaps

### 2.1 Missing Pages

| Page | Priority | Needed Endpoints |
|------|----------|-----------------|
| **Send Email** | Medium | `sendEmail` |
| **Account Migration** | Low | `checkAccountStatus`, `deactivateAccount`, `activateAccount` |
| **Audit Log** | Low | (no logging endpoint exists) |
| **Content Browser** | Medium | `sync.listRepos`, `repo.describeRepo`, `repo.listRecords` |

### 2.2 Missing Features in Existing Pages

**Dashboard:** only total account count. Missing active/deactivated count, total blobs, recent signups, invite code usage stats, system health.

**Accounts:** only searches by email (no handle/DID search, no prefix matching). No initial "browse all". No sorting, no account status indicators (takedown, deactivated, invites disabled). No multi-select or bulk actions.

**AccountDetail:** missing from display — `emailConfirmedAt`, `inviteNote`, `relatedRecords`, `createdAt`, `deactivatedAt` (shown as raw field from response, not part of `accountView`), `threatSignatures`. Missing actions — deactivate/reactivate, update signing key, send email, view migration status. No per-button loading states.

**InviteCodes:** missing columns — `forAccount`, `createdBy`, `createdAt`. Missing features — sort by usage/recent, filter by disabled/active, pagination, disable all codes for a DID.

**SubjectStatus:** only supports DID lookup. Missing record URI (`uri`) and blob CID (`blob`) lookup. Can't update `deactivated` status (only `takedown`).

### 2.3 Architecture Issues

| Issue | Detail |
|-------|--------|
| No caching layer | Every navigation = fresh API call. No React Query/RTK/ stale-while-revalidate. |
| No optimistic updates | Actions wait for server response before reflecting changes. |
| Auth in sessionStorage | No token refresh, no cross-tab persistence. |
| No error boundaries | Unhandled render errors blank the page. |
| No pagination state in URL | Cursor/query params lost on browser refresh. |
| No TypeScript codegen | Types in `types/admin.ts` may drift from actual API responses. |
| No integration tests | Zero tests for the admin web UI. |

---

## 3. CLI Feature Gaps

| Command | Status | Problem |
|---------|--------|---------|
| `account create` | Stub (`NotSupportedException`) | Needs an endpoint or flow |
| `create-invite-code` | Stub (`NotSupportedException`) | Same |
| `update` | Stub (`NotImplementedException`) | Self-update not implemented |
| `send-email` | ❌ Missing | Endpoint exists |
| `get-invite-codes` | ❌ Missing | Endpoint exists |
| `disable-invite-codes` | ❌ Missing | Endpoint exists |
| `get-subject-status` | ❌ Missing | Endpoint exists |
| `search-accounts` | ❌ Missing | Endpoint exists |
| `update-signing-key` | ❌ Missing | No endpoint exists |
| `deactivate` / `activate` | ❌ Missing | Via `updateSubjectStatus` |
| `send-email` | ❌ Missing | Via `sendEmail` endpoint |
| `account list` hardcoded limit 100 | ⚠️ Partial | Uses `sync.listRepos` + `getAccountInfos`, large set handling absent |

**12 missing or broken commands** out of the CLI's command set.

---

## 4. Lexicon Compliance Gaps

| Gap | Endpoint | Issue |
|-----|----------|-------|
| Response shape | `getAccountInfo` | Returns flat object, not `accountView`. Missing `relatedRecords`, `invitedBy`, `invites`, `threatSignatures`, `indexedAt`. |
| Property name | `getAccountInfos` | Returns `{ accounts: [...] }`, lexicon expects `{ infos: [...] }`. |
| Input field name | `enableAccountInvites`/`disableAccountInvites` | Accepts `did`, lexicon says `account`. Also ignores optional `note` field. |
| Missing field | `sendEmail` | Requires `senderDid` per lexicon; local allows it to be absent. |
| Input field name | `updateAccountEmail` | Accepts `did`, lexicon says `account` (format `at-identifier` — allows DID or handle). |
| Auth attribute | `createInviteCode`, `createInviteCodes` | Used `AccessPrivileged`; spec says admin auth. Changed to `AdminToken`. |
| Missing endpoint | `updateAccountSigningKey` | Lexicon defines it, no handler. |

---

## 5. Security & Operational Issues

| # | Issue | Severity | Location |
|---|-------|----------|----------|
| 1 | Admin password hardcoded to `"secret"` in DevMode | Critical | `ServerConfig.cs:315` |
| 2 | No rate limiting on admin endpoints | High | No per-endpoint policies for admin routes |
| 3 | No audit logging for sensitive admin actions | High | Only `deleteAccount` logs; rest are silent |
| 4 | No confirmation before destructive actions in web UI | ✅ Resolved | ConfirmDialog component added to AccountDetail and SubjectStatus |
| 5 | No IP allowlist for admin access | Medium | Admin routes accessible from any IP |
| 6 | No session timeout for admin web UI | Medium | Password stored indefinitely in sessionStorage |
| 7 | No CSRF protection | Low | Basic auth only |
| 8 | No admin action approval workflow | Low | Single admin, no review step |

---

## 6. Capability Matrix: CLI vs Web UI

| Capability | CLI | Web UI |
|-----------|-----|--------|
| List accounts | ✅ | ✅ |
| Search accounts | ❌ | ✅ |
| View account detail | ✅ | ✅ |
| Delete account (admin) | ✅ | ✅ |
| Takedown/untakedown | ✅ | ✅ |
| Enable/disable invites | ✅ | ✅ |
| Reset password | ✅ | ✅ |
| Update email | ✅ | ✅ |
| Update handle | ✅ | ✅ |
| List invite codes | ❌ | ✅ |
| Disable invite codes | ❌ | ✅ |
| Subject status lookup | ❌ | ✅ |
| Send email | ❌ | ❌ |
| Create invite code | ❌ (stub) | ✅ |
| Create account | ❌ (stub) | ❌ |
| Request crawl | ✅ | ❌ |
| Self-update | ❌ (stub) | ❌ |
| Deactivate/activate | ❌ | ❌ |
| Check account status | ❌ | ❌ |
| Update signing key | ❌ | ❌ |
| Content browsing | ❌ | ❌ |
| Audit log | ❌ | ❌ |
| Bulk operations | ❌ | ❌ |

---

## Summary Statistics

| Metric | Count |
|--------|-------|
| Admin lexicon endpoints defined | 15 |
| Admin endpoints implemented | 14 (93%) |
| Admin endpoints with web UI | 11 (73%) |
| Admin endpoints with CLI | 8 (53%) |
| Missing backend endpoint | 1 (`updateAccountSigningKey`) |
| Missing web UI pages | 4 |
| Missing CLI commands | 12 |
| Lexicon compliance gaps (response shape) | 4+ |
| Lexicon compliance gaps (input field name) | 3 |
| Security issues identified | 7 (1 resolved) |
