# TESTING.MD — Test Implementation Plan

This document is an actionable plan for agents to implement the testing gaps identified in GAPS.md. It covers conventions, infrastructure, and a prioritized list of test files to write.

---

## 1. Current State

### 1.1 Test Projects

| Project | Framework | Tests | What It Tests |
|---|---|---|---|
| `test/CID.Tests/` | TUnit | 12 | CID v0/v1 parsing, creation, round-trip |
| `test/Common.Tests/` | TUnit | 10 | TID, S32 encoding, CBOR round-trip |
| `test/ActorStore.Tests/` | TUnit | 156 | `Prepare.ExtractBlobReferences` |
| `test/atompds.Tests/` | TUnit | ~90 | Integration tests via `WebApplicationFactory<Program>` |
| `test/SubscribeTester/` | — | — | Manual WebSocket tool (not automated) |

### 1.2 Existing Test Patterns

All new tests go into `test/atompds.Tests/`. Follow these conventions exactly:

**Framework:** TUnit (`[Test]`, `[Arguments]`, `Assert.That(...).IsEqualTo(...)`)

**Test class structure:**
```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using atompds.Tests.Infrastructure;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace atompds.Tests;

public class MyFeatureTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();

    [Test]
    public async Task Endpoint_Behavior_ExpectedResult()
    {
        // arrange
        var token = AuthTestHelper.CreateAccessToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/xrpc/...");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // act
        var response = await Client.SendAsync(request);

        // assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
```

**Auth helpers** (in `AuthTestHelper.cs`):
- `CreateAccessToken(did, scope, audience)` — creates `at+jwt` with HS256
- `CreateRefreshToken(did, jti)` — creates `refresh+jwt`
- `CreateAppPasswordToken(did, scope)` — creates token with `appPass` scope
- `CreatePrivilegedToken(did)` — creates token with `appPassPrivileged` scope
- `GetAdminBasicAuth()` — returns `Basic` header for admin:secret
- `GetBearerAuth(token)` — returns `Bearer` header
- `ReadJsonAsync<T>(response)` / `ReadJsonAsync(response)` — deserialize response

**Test server:** `TestWebAppFactory` creates a full in-memory ASP.NET host with SQLite databases in a temp directory. Auto-cleans on dispose. Config includes `PDS_DEV_MODE=true`, `PDS_INVITE_REQUIRED=false`.

**Admin requests:** Use helper pattern from `AdminTests.cs`:
```csharp
private HttpRequestMessage CreateAdminRequest(string method, string url, string? body = null)
{
    var httpMethod = method == "GET" ? HttpMethod.Get : HttpMethod.Post;
    var request = new HttpRequestMessage(httpMethod, url);
    request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
    if (body != null)
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    else if (httpMethod == HttpMethod.Post)
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
    return request;
}
```

---

## 2. Infrastructure to Add Before Writing Tests

### 2.1 Account Provisioning Helper

The biggest missing piece is the ability to create test accounts within integration tests. Currently tests only verify auth rejection and route existence — they cannot exercise happy paths that require a real account.

**Create `test/atompds.Tests/Infrastructure/AccountHelper.cs`:**

```csharp
public static class AccountHelper
{
    public static async Task<AccountInfo> CreateAccountAsync(
        HttpClient client,
        string email = "test@test.test",
        string handle = "testuser.test",
        string? password = "test-password-123",
        string? inviteCode = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["handle"] = handle,
            ["password"] = password,
        };
        if (inviteCode != null)
            body["inviteCode"] = inviteCode;

        var request = new HttpRequestMessage(HttpMethod.Post,
            "/xrpc/com.atproto.server.createAccount")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await AuthTestHelper.ReadJsonAsync(response);

        return new AccountInfo(
            Did: json.GetProperty("did").GetString()!,
            Handle: handle,
            Email: email,
            Password: password,
            AccessJwt: json.GetProperty("accessJwt").GetString()!,
            RefreshJwt: json.GetProperty("refreshJwt").GetString()!,
            Active: json.TryGetProperty("active", out var active) && active.GetBoolean()
        );
    }
}

public record AccountInfo(
    string Did, string Handle, string Email, string? Password,
    string AccessJwt, string RefreshJwt, bool Active);
```

### 2.2 Invite Code Helper

When `PDS_INVITE_REQUIRED=true` tests need invite codes. Add to `AccountHelper`:

```csharp
public static async Task<string> CreateInviteCodeAsync(HttpClient client)
{
    var request = new HttpRequestMessage(HttpMethod.Post,
        "/xrpc/com.atproto.server.createInviteCode");
    request.Headers.Add("Authorization", AuthTestHelper.GetAdminBasicAuth());
    request.Content = new StringContent(
        """{"useCount": 1}""", Encoding.UTF8, "application/json");
    var response = await client.SendAsync(request);
    response.EnsureSuccessStatusCode();
    var json = await AuthTestHelper.ReadJsonAsync(response);
    return json.GetProperty("code").GetString()!;
}
```

### 2.3 JSON Content Helper

Add to `AuthTestHelper.cs`:

```csharp
public static HttpRequestMessage CreateJsonRequest(HttpMethod method, string url, object body)
{
    return new HttpRequestMessage(method, url)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
    };
}

public static HttpRequestMessage WithAuth(this HttpRequestMessage request, string authHeader)
{
    request.Headers.Add("Authorization", authHeader);
    return request;
}

public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
{
    var json = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<JsonElement>(json);
}

public static async Task AssertXrpcErrorAsync(HttpResponseMessage response, HttpStatusCode status, string error)
{
    await Assert.That(response.StatusCode).IsEqualTo(status);
    var json = await ReadJsonAsync(response);
    await Assert.That(json.GetProperty("error").GetString()).IsEqualTo(error);
}
```

### 2.4 Shared Factory Pattern Issue

Currently every test class creates its own `static readonly TestWebAppFactory Factory = new()`. This means each class gets its own database. This is fine for now — SQLite is lightweight and tests are isolated. Do **not** change this pattern.

If tests need to share state (e.g., an account created in one test used by another), keep them in the **same test class** and use a `[ClassDataSource]` or static shared state pattern:

```csharp
public class AccountLifecycleTests
{
    private static readonly TestWebAppFactory Factory = new();
    private HttpClient Client => Factory.CreateClient();
    private static AccountInfo? _account;

    [Test]
    [Order(1)]
    public async Task Step1_CreateAccount()
    {
        _account = await AccountHelper.CreateAccountAsync(Client);
        await Assert.That(_account.Did).IsNotNull();
    }

    [Test]
    [Order(2)]
    public async Task Step2_GetSession_WithAccount()
    {
        // uses _account from previous test
    }
}
```

---

## 3. Test Files to Write — Prioritized by Risk and Value

Each section is a self-contained task an agent can pick up. Files are listed in dependency order.

---

### Phase 1: Account Lifecycle (Highest Priority)

These tests exercise the most critical paths and unlock all subsequent test phases.

#### `test/atompds.Tests/AccountTests.cs`

**Purpose:** Test full account creation, login, session management, and deletion.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `CreateAccount_BasicSucceeds` | Create account with email+handle+password. Assert 200, response has `did`, `accessJwt`, `refreshJwt`, `handle`. |
| 2 | `CreateAccount_ReturnsDidPlc` | Assert DID starts with `did:plc:`. |
| 3 | `CreateAccount_DuplicateEmail_ReturnsError` | Create account, then create another with same email (different handle). Assert `AccountTaken` error. |
| 4 | `CreateAccount_DuplicateHandle_ReturnsError` | Create account, then create another with same handle (different email). Assert error. |
| 5 | `CreateAccount_InvalidHandle_ReturnsError` | Try handles: too short, invalid chars, single char. Assert `InvalidHandle` error. |
| 6 | `CreateAccount_InvalidEmail_ReturnsError` | Try malformed emails. Assert error. |
| 7 | `CreateAccount_ReservedHandle_ReturnsError` | Try reserved handles like "admin", "moderator". |
| 8 | `CreateAccount_WithInviteCode_Succeeds` | Set `PDS_INVITE_REQUIRED=true` in factory, create invite code via admin, then create account with code. |
| 9 | `CreateAccount_InvalidInviteCode_ReturnsError` | Use a bad invite code. Assert `InvalidInviteCode` error. |
| 10 | `CreateSession_BasicLogin` | Create account, then `createSession` with email+password. Assert tokens returned. |
| 11 | `CreateSession_BadPassword_ReturnsError` | Create account, login with wrong password. Assert `InvalidPassword` error. |
| 12 | `CreateSession_UnknownEmail_ReturnsError` | Login with non-existent email. Assert same error type as bad password (no user enumeration). |
| 13 | `GetSession_WithValidAccess_ReturnsSession` | Create account, use accessJwt from creation to call `getSession`. Assert `did`, `handle`, `email`. |
| 14 | `RefreshSession_WithRefreshToken` | Create account, call `refreshSession` with refreshJwt. Assert new tokens returned. |
| 15 | `RefreshSession_WithAccessToken_ReturnsError` | Try to refresh using accessJwt. Assert `ExpiredToken` or `InvalidToken`. |
| 16 | `DeleteSession_RevokesRefreshToken` | Create account, delete session, then try to refresh. Assert error. |
| 17 | `RequestAccountDelete_SendsToken` | Create account, call `requestAccountDelete`. Assert 200 (email token created internally). |
| 18 | `DeleteAccount_WithToken_Succeeds` | Create account, request delete, get token from DB, call `deleteAccount`. Assert account gone from `getAccountInfo`. |
| 19 | `ActivateAccount_Succeeds` | Create account, deactivate, then activate. Assert `active` flag changes. |
| 20 | `DeactivateAccount_Succeeds` | Create account, deactivate. Assert `active` is false. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/CreateAccountController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/CreateSessionController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/GetSessionController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/RefreshSessionController.cs`
- `src/pds_projects/AccountManager/AccountRepository.cs`

---

#### `test/atompds.Tests/PasswordTests.cs`

**Purpose:** Password reset and change flows.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `RequestPasswordReset_Succeeds` | Create account, request reset. Assert 200. |
| 2 | `ResetPassword_WithToken_Succeeds` | Request reset, extract token from `email_token` table directly (use `AccountManagerDb` from DI), reset password. Assert new password works for login. |
| 3 | `ResetPassword_OldPasswordFails` | After reset, login with old password fails. |
| 4 | `ResetPassword_InvalidToken_ReturnsError` | Use a bad token. Assert error. |
| 5 | `ResetPassword_TokenSingleUse` | Use token twice. Second use fails. |
| 6 | `AdminUpdatePassword_Succeeds` | Use admin endpoint to update password. Assert new password works. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/RequestPasswordResetController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/ResetPasswordController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Admin/UpdateAccountPasswordAdminController.cs`
- `src/pds_projects/AccountManager/Db/PasswordStore.cs`

---

#### `test/atompds.Tests/AppPasswordTests.cs`

**Purpose:** App-specific password lifecycle.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `CreateAppPassword_Succeeds` | Create account, create app password with name. Assert 200, `name`, `password` returned. |
| 2 | `ListAppPasswords_ReturnsCreated` | Create app password, list. Assert the created one appears. |
| 3 | `RevokeAppPassword_Succeeds` | Create, then revoke. List should be empty. |
| 4 | `CreateAppPassword_DuplicateName_ReturnsError` | Create two with same name. Second fails. |
| 5 | `AppPasswordToken_GrantsAccess` | Login with app password token, verify `getSession` works but shows `via` field. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/CreateAppPasswordController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/ListAppPasswordsController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/RevokeAppPasswordController.cs`

---

### Phase 2: Record CRUD Operations

#### `test/atompds.Tests/CrudTests.cs`

**Purpose:** Full record lifecycle — create, read, update, delete, list, applyWrites.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `CreateRecord_Succeeds` | Create account, create a record (`app.bsky.feed.post`). Assert 200, `uri`, `cid`. |
| 2 | `CreateRecord_ReturnsValidAtUri` | Assert `uri` is `at://<did>/<collection>/<rkey>` format. |
| 3 | `GetRecord_ReturnsCreated` | Create record, get it back. Assert record body matches. |
| 4 | `ListRecords_ReturnsCreated` | Create multiple records, list. Assert all appear. |
| 5 | `ListRecords_Pagination` | Create 5+ records, list with `limit=2`. Assert `cursor` returned, next page works. |
| 6 | `PutRecord_Succeeds` | Create, then put (update). Assert new value stored. |
| 7 | `PutRecord_CreateIfMissing` | Put without prior create. Assert record created. |
| 8 | `DeleteRecord_Succeeds` | Create, delete. `getRecord` returns 400 or empty. |
| 9 | `DeleteRecord_NonExistent_NoError` | Delete a record that doesn't exist. Assert 200 (no-op). |
| 10 | `ApplyWrites_Create` | Use `applyWrites` with create action. Assert record created. |
| 11 | `ApplyWrites_Update` | Create, then `applyWrites` update. Assert changed. |
| 12 | `ApplyWrites_Delete` | Create, then `applyWrites` delete. Assert removed. |
| 13 | `ApplyWrites_BatchOperations` | Multiple creates in single `applyWrites` call. |
| 14 | `CreateRecord_WrongUser_ReturnsError` | Try to create record in another user's repo. |
| 15 | `CreateRecord_InvalidCollection_ReturnsError` | Use malformed collection name. |
| 16 | `DescribeRepo_ReturnsInfo` | Create account, describe repo. Assert `handle`, `did`, `collections`. |
| 17 | `CreateRecord_ProfileSelfRkey` | Create profile record. Assert rkey is `self`. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Repo/ApplyWritesController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Repo/ListRecordsController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Repo/DescribeRepoController.cs`
- `src/pds_projects/ActorStore/Repo/Prepare.cs`
- `src/pds_projects/ActorStore/Record/RecordRepository.cs`

---

#### `test/atompds.Tests/BlobTests.cs`

**Purpose:** Blob upload, retrieval, and deletion.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `UploadBlob_Succeeds` | Create account, upload small PNG bytes. Assert 200, `blob` with `ref` (CID). |
| 2 | `UploadBlob_ExceedsLimit_ReturnsError` | Upload a blob larger than `PDS_BLOB_UPLOAD_LIMIT_IN_BYTES`. Assert error. |
| 3 | `GetBlob_ReturnsUploaded` | Upload blob via `uploadBlob`, then `sync.getBlob` with did+cid. Assert bytes match. |
| 4 | `ListBlobs_ReturnsUploaded` | Upload blob, `sync.listBlobs`. Assert CID appears in list. |
| 5 | `UploadBlob_NoAuth_ReturnsError` | Assert 401 without token. |
| 6 | `UploadBlob_WithImage_Succeeds` | Upload actual image bytes (small test PNG). Verify dimensions detected if applicable. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Repo/BlobController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Sync/GetBlobController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Sync/ListBlobsController.cs`
- `src/pds_projects/BlobStore/DiskBlobStore.cs`

**Test data:** Use files in `test/data/` or generate small PNG bytes inline:
```csharp
private static byte[] CreateTestPng() => [
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG header
    // ... minimal 1x1 PNG
];
```

---

### Phase 3: Sync and Federation

#### `test/atompds.Tests/SyncFederationTests.cs`

**Purpose:** Repo sync endpoints — getRepo, getBlocks, getLatestCommit, subscribeRepos.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `GetRepo_ReturnsCarFile` | Create account with records, `getRepo`. Assert response is CAR bytes (starts with magic bytes). |
| 2 | `GetRepo_NonExistentDid_ReturnsError` | Use a DID that doesn't exist. |
| 3 | `GetBlocks_ReturnsBlocks` | Create records, `getBlocks` with did+commit CID. Assert bytes returned. |
| 4 | `GetLatestCommit_ReturnsCid` | Create account, `getLatestCommit`. Assert `cid` and `rev` returned. |
| 5 | `GetLatestCommit_NonExistentDid_ReturnsError` | Unknown DID. |
| 6 | `GetRepoStatus_Active_ReturnsActive` | Active account returns `active: true`. |
| 7 | `GetRepoStatus_NonExistent_ReturnsError` | Unknown DID. |
| 8 | `SyncGetRecord_ReturnsRecord` | Create record, get via `sync.getRecord`. Assert record bytes. |
| 9 | `ListRepos_ReturnsCreatedAccount` | Create account, `listRepos`. Assert DID appears. |
| 10 | `ListRepos_Pagination` | Create multiple accounts, paginate. |
| 11 | `SubscribeRepos_ConnectsAndReceivesEvents` | Open WebSocket, create a record, assert event received. (May need `SubscribeTester` patterns or `ClientWebSocket`.) |
| 12 | `ImportRepo_Succeeds` | Export repo CAR, import to new DID. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Sync/GetRepoController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Sync/GetBlocksController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Sync/GetLatestCommitController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Sync/SubscribeReposController.cs`
- `src/pds_projects/Sequencer/Outbox.cs`
- `test/SubscribeTester/Program.cs` (for WebSocket patterns)

**WebSocket test pattern:**
```csharp
[Test]
public async Task SubscribeRepos_ReceivesCommitEvent()
{
    // Create account first
    var account = await AccountHelper.CreateAccountAsync(Client);

    // Connect WebSocket
    using var ws = new ClientWebSocket();
    await ws.ConnectAsync(
        new Uri($"ws://localhost/xrpc/com.atproto.sync.subscribeRepos"),
        CancellationToken.None);

    // Create a record to trigger event
    var recordBody = new { /* post record */ };
    // ... POST createRecord ...

    // Read from WebSocket
    var buffer = new byte[4096];
    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
    // Assert we got data
    await Assert.That(result.Count).IsGreaterThan(0);

    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
}
```

---

### Phase 4: Admin Operations

#### `test/atompds.Tests/AdminLifecycleTests.cs`

**Purpose:** Admin endpoints that require real accounts (takedown, invites, account management).

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `GetAccountInfo_ReturnsAccount` | Create account, admin `getAccountInfo`. Assert email, handle, did. |
| 2 | `GetAccountInfos_ReturnsAccounts` | Create 2 accounts, `getAccountInfos` with both DIDs. |
| 3 | `AdminUpdateAccountEmail` | Change email via admin endpoint. Assert changed. |
| 4 | `AdminUpdateAccountHandle` | Change handle via admin endpoint. Assert changed. |
| 5 | `AdminDeleteAccount` | Delete account via admin. Assert gone from `listRepos`. |
| 6 | `Takedown_Account` | Takedown account. Assert `getSubjectStatus` shows takedown. Assert taken-down account cannot `getSession`. |
| 7 | `Untakedown_Account` | Remove takedown. Assert account works again. |
| 8 | `EnableInvites_ForAccount` | Disable invites, then enable. Assert flag toggled. |
| 9 | `DisableInvites_ForAccount` | Disable invites for account. Assert `invitesDisabled` is true. |
| 10 | `GetInviteCodes_ReturnsCodes` | Create invite code, list via admin. Assert code appears. |
| 11 | `DisableInviteCodes` | Create code, disable it. Assert no longer usable. |
| 12 | `UpdateSubjectStatus_Takedown` | Use `updateSubjectStatus` for takedown. |
| 13 | `SendEmail_ToAccount` | Send email via admin endpoint. Assert 200 (email queued). |

**Key source files to read:**
- All controllers in `src/atompds/Controllers/Xrpc/Com/Atproto/Admin/`
- `src/pds_projects/AccountManager/AccountRepository.cs`

---

### Phase 5: Invite Code System

#### `test/atompds.Tests/InviteCodeTests.cs`

**Purpose:** Invite code creation, usage, tracking.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `CreateInviteCode_Succeeds` | Admin creates code. Assert code string returned. |
| 2 | `CreateInviteCodes_MultipleCodes` | Create multiple codes in one call. |
| 3 | `CreateAccount_WithValidCode_Succeeds` | Use invite code during signup. |
| 4 | `CreateAccount_WithUsedUpCode_ReturnsError` | Use code that's been used `useCount` times. |
| 5 | `CreateAccount_WithDisabledCode_ReturnsError` | Disable code, try to use. |
| 6 | `GetAccountInviteCodes_ReturnsCodes` | Create codes, user fetches their codes. |
| 7 | `InviteCodeUseTracking` | Use a code, check `invite_code_use` has entry. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/CreateInviteCodeController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/CreateInviteCodesController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/GetAccountInviteCodesController.cs`
- `src/pds_projects/AccountManager/Db/InviteStore.cs`

---

### Phase 6: Auth Scope Enforcement

#### `test/atompds.Tests/AuthScopeTests.cs`

**Purpose:** Verify that auth scopes are correctly enforced on all endpoints.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `AccessStandard_AcceptsAccessToken` | Call `createRecord` with `access` scope token. Assert passes auth. |
| 2 | `AccessStandard_AcceptsAppPassToken` | Call `createRecord` with `appPass` scope token. Assert passes auth. |
| 3 | `AccessStandard_AcceptsPrivilegedToken` | Call `createRecord` with `appPassPrivileged` scope token. |
| 4 | `AccessStandard_RejectsRefreshToken` | Call `createRecord` with `refresh` scope token. Assert 401. |
| 5 | `AccessFull_RejectsAppPassToken` | Call `deleteAccount` (full) with `appPass` token. Assert 401. |
| 6 | `AccessFull_AcceptsAccessToken` | Call `deleteAccount` (full) with `access` token (should pass auth check, may fail on body validation). |
| 7 | `AccessPrivileged_RejectsAppPassToken` | Call `updateHandle` with `appPass` token. Assert 401. |
| 8 | `AccessPrivileged_AcceptsPrivilegedToken` | Call `updateHandle` with `appPassPrivileged` token. |
| 9 | `AdminToken_RejectsBearerToken` | Call admin endpoint with Bearer token. Assert 401. |
| 10 | `AdminToken_AcceptsBasicAuth` | Call admin endpoint with Basic auth. Assert passes. |
| 11 | `Refresh_OnlyAcceptsRefreshToken` | Call `refreshSession` with refresh token. Assert passes. With access token, assert fails. |
| 12 | `ExpiredToken_ReturnsExpiredError` | Create token with past `exp`. Assert `ExpiredToken` error type. |

**Key source files to read:**
- `src/atompds/Middleware/AuthMiddleware.cs`
- `src/atompds/Middleware/AuthVerifier.cs`
- `src/atompds/Services/AuthVerifierConfig.cs`

---

### Phase 7: Email Flows

#### `test/atompds.Tests/EmailTests.cs`

**Purpose:** Email confirmation, update, and token-based flows.

**Prerequisite:** The `TestWebAppFactory` registers `StubMailer` by default (no SMTP configured). Tests verify email tokens are created in the database, not that emails are actually sent.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `ConfirmEmail_WithToken_Succeeds` | Create account (unconfirmed email), get token from `email_token` DB, confirm. Assert `emailConfirmedAt` set. |
| 2 | `ConfirmEmail_InvalidToken_ReturnsError` | Use wrong token. |
| 3 | `RequestEmailConfirmation_Succeeds` | Request confirmation. Assert token created in DB. |
| 4 | `RequestEmailUpdate_Succeeds` | Authenticated request. Assert new token created. |
| 5 | `UpdateEmail_WithToken_Succeeds` | Update email with token. Assert new email in account. |
| 6 | `UpdateEmail_InvalidToken_ReturnsError` | Wrong token. |

**How to access DB in tests:**
```csharp
// Get the scoped DbContext from the test server's service provider
using var scope = Factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AccountManagerDb>();
var token = await db.EmailTokens
    .FirstOrDefaultAsync(t => t.Did == account.Did && t.Purpose == EmailTokenPurpose.ConfirmEmail);
```

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/ConfirmEmailController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/RequestEmailConfirmationController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/RequestEmailUpdateController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Server/UpdateEmailController.cs`
- `src/pds_projects/AccountManager/Db/EmailTokenStore.cs`

---

### Phase 8: Identity and Handle Management

#### `test/atompds.Tests/HandleTests.cs`

**Purpose:** Handle resolution, update, validation.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `ResolveHandle_ByHandle_ReturnsDid` | Create account, resolve handle. Assert DID returned. |
| 2 | `ResolveHandle_UnknownHandle_ReturnsError` | Non-existent handle. |
| 3 | `UpdateHandle_Succeeds` | Create account, update handle. Assert new handle works for resolution. |
| 4 | `UpdateHandle_InvalidHandle_ReturnsError` | Malformed handle. |
| 5 | `UpdateHandle_TakenHandle_ReturnsError` | Create two accounts, try to set second's handle to first's. |
| 6 | `GetRecommendedDidCredentials_ReturnsInfo` | Authenticated call. Assert `rotationKeys`, `alsoKnownAs` returned. |
| 7 | `RequestPlcOperationSignature_Queues` | Authenticated call. Assert 200. |
| 8 | `SignPlcOperation_ReturnsOperation` | Authenticated call with body. |
| 9 | `SubmitPlcOperation_ReturnsResult` | Authenticated call with body. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Identity/` (all 6 controllers)
- `src/pds_projects/AccountManager/AccountRepository.cs`
- `src/projects/Handle/`

---

### Phase 9: Moderation and Takedown

#### `test/atompds.Tests/ModerationTests.cs`

**Purpose:** Report creation, takedown lifecycle.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `CreateReport_Succeeds` | Create account, report a repo/record. Assert `id` returned. |
| 2 | `CreateReport_InvalidSubject_ReturnsError` | Bad subject type. |
| 3 | `Takedown_HidesRecords` | Create record, takedown account. `getRecord` for that account returns error. |
| 4 | `Takedown_HidesFromListRecords` | Takedown account. `listRecords` returns empty or error. |
| 5 | `Takedown_PreventsLogin` | Takedown account. `createSession` fails. |
| 6 | `Takedown_InvalidatesTokens` | Create session, takedown account. `getSession` fails. |
| 7 | `Untakedown_RestoresAccess` | Untakedown. Everything works again. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/Com/Atproto/Moderation/CreateReportController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Admin/SubjectStatusController.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Admin/AdminDeleteAccountController.cs`

---

### Phase 10: Sequencer and Event Stream

#### `test/atompds.Tests/SequencerTests.cs`

**Purpose:** Event sequencing, cursor management, firehose behavior.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `Sequencer_EventsCreated` | Create account + record. Query `repo_seq` table directly. Assert events exist. |
| 2 | `Sequencer_CommitEvent` | Create record. Verify `commit` event type in DB. |
| 3 | `Sequencer_HandleEvent` | Update handle. Verify `handle` event type in DB. |
| 4 | `Sequencer_AccountEvent` | Create account. Verify `account` event type in DB. |
| 5 | `Sequencer_IdentityEvent` | Update handle. Verify `identity` event type in DB. |
| 6 | `Sequencer_CursorIncreases` | Multiple operations. Verify `seq` values increase. |
| 7 | `SubscribeRepos_ReceivesCommitEvent` | WebSocket test: subscribe, create record, verify event received with correct structure. |
| 8 | `SubscribeRepos_WithCursor_SkipsOld` | Create events, subscribe with cursor. Assert only newer events received. |

**How to access sequencer DB:**
```csharp
using var scope = Factory.Services.CreateScope();
var sequencerDbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SequencerDb>>();
using var db = await sequencerDbFactory.CreateDbContextAsync();
var events = await db.RepoSeqs.OrderBy(e => e.Seq).ToListAsync();
```

**Key source files to read:**
- `src/pds_projects/Sequencer/SequencerRepository.cs`
- `src/pds_projects/Sequencer/Outbox.cs`
- `src/atompds/Controllers/Xrpc/Com/Atproto/Sync/SubscribeReposController.cs`

---

### Phase 11: OAuth Flow

#### `test/atompds.Tests/OAuthFlowTests.cs`

**Purpose:** End-to-end OAuth PKCE flow.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `Authorize_WithPKCE_ReturnsAuthorizationId` | Call `authorize` with all params. Assert `authorization_id` returned. |
| 2 | `Authorize_MissingClientId_ReturnsError` | Omit `client_id`. |
| 3 | `Authorize_MissingRedirectUri_ReturnsError` | Omit `redirect_uri`. |
| 4 | `Authorize_MissingCodeChallenge_ReturnsError` | Omit `code_challenge`. |
| 5 | `Authorize_UnsupportedChallengeMethod_ReturnsError` | Use `plain` instead of `S256`. |
| 6 | `Authorize_TrustedClient_WithAuth_ReturnsRedirect` | Configure `PDS_OAUTH_TRUSTED_CLIENTS`, authenticate, authorize. Assert redirect with code. |
| 7 | `Consent_WithValidAuth_ReturnsRedirect` | Create authorization, consent. Assert redirect URL with code and state. |
| 8 | `Consent_InvalidAuthorizationId_ReturnsError` | Use bad auth ID. |
| 9 | `Token_AuthorizationCode_Succeeds` | Full flow: authorize → consent → exchange code for token. Assert access+refresh tokens. |
| 10 | `Token_RefreshToken_Succeeds` | Get tokens, refresh. Assert new tokens. |
| 11 | `Token_InvalidCode_ReturnsError` | Bad authorization code. |
| 12 | `Token_InvalidCodeVerifier_ReturnsError` | Wrong verifier. |
| 13 | `Token_UnsupportedGrantType_ReturnsError` | Use `client_credentials` grant. |

**Key source files to read:**
- `src/atompds/Controllers/OAuth/OAuthAuthorizeController.cs`
- `src/atompds/Controllers/OAuth/OAuthTokenController.cs`
- `src/atompds/Services/OAuth/OAuthSessionStore.cs`

---

### Phase 12: Proxy and AppView Routing

#### `test/atompds.Tests/ProxyTests.cs`

**Purpose:** Verify proxy routing, catch-all behavior, service JWT creation.

**Tests to write:**

| # | Test Name | What It Does |
|---|---|---|
| 1 | `Proxy_BskyAppViewDisabled_Returns404` | With no AppView configured, app.bsky.* returns 404. |
| 2 | `Proxy_Catchall_RoutesAppBsky` | Verify `app.bsky.*` methods hit the catch-all (even without AppView, they should not 404 from routing). |
| 3 | `Proxy_Catchall_RoutesChatBsky` | Verify `chat.bsky.*` methods hit the catch-all. |
| 4 | `Proxy_Catchall_RoutesModeration` | Verify `com.atproto.moderation.*` hits the catch-all. |
| 5 | `Proxy_Catchall_RejectsUnknownNamespace` | `unknown.namespace.method` returns 404. |
| 6 | `Proxy_AtpProtoProxyHeader_Parsed` | Verify `atproto-proxy` header with `did:plc:xxx#serviceId` format is parsed. |
| 7 | `Proxy_AtpProtoProxyHeader_Invalid_ReturnsError` | Bad header format. |
| 8 | `Proxy_NsidParsing_ValidNsid` | Verify valid NSID parsing via controller behavior. |
| 9 | `Proxy_NsidParsing_InvalidNsid` | Bad characters, empty, too short. |
| 10 | `Proxy_ReadAfterWrite_PatchesProfile` | Create profile, get via proxy (mock AppView response), verify local write patched in. |

**Key source files to read:**
- `src/atompds/Controllers/Xrpc/AppViewProxyController.cs`
- `src/atompds/Services/WriteSnapshotCache.cs`
- `src/atompds/Services/ServiceJwtBuilder.cs`

---

### Phase 13: Account Deactivation and Deletion

#### `test/atompds.Tests/AccountDeactivationTests.cs`

**Tests to write:**

| # | Test Name |
|---|---|
| 1 | `DeactivateAccount_SetsDeactivatedAt` |
| 2 | `DeactivateAccount_CheckAccountStatus_ShowsDeactivated` |
| 3 | `ActivateAccount_ClearsDeactivatedAt` |
| 4 | `DeactivatedAccount_CannotCreateRecords` |
| 5 | `DeactivatedAccount_CannotLogin` |
| 6 | `DeleteAccount_RemovesFromListRepos` |
| 7 | `DeleteAccount_RemovesActorStore` |

#### `test/atompds.Tests/AccountDeletionTests.cs`

**Tests to write:**

| # | Test Name |
|---|---|
| 1 | `RequestAccountDelete_CreatesToken` |
| 2 | `DeleteAccount_WithValidToken_Succeeds` |
| 3 | `DeleteAccount_WithWrongToken_ReturnsError` |
| 4 | `DeleteAccount_WithTokenFromOtherAccount_ReturnsError` |
| 5 | `DeleteAccount_AlreadyDeleted_ReturnsError` |

---

## 4. Implementation Order

Agents should implement in this exact order, as later phases depend on earlier ones:

```
Phase 1  → Infrastructure (AccountHelper, InviteCodeHelper, JsonContentHelper)
         → AccountTests (20 tests)
         → PasswordTests (6 tests)
         → AppPasswordTests (5 tests)

Phase 2  → CrudTests (17 tests)
         → BlobTests (6 tests)

Phase 3  → SyncFederationTests (12 tests)

Phase 4  → AdminLifecycleTests (13 tests)

Phase 5  → InviteCodeTests (7 tests)

Phase 6  → AuthScopeTests (12 tests)

Phase 7  → EmailTests (6 tests)

Phase 8  → HandleTests (9 tests)

Phase 9  → ModerationTests (7 tests)

Phase 10 → SequencerTests (8 tests)

Phase 11 → OAuthFlowTests (13 tests)

Phase 12 → ProxyTests (10 tests)

Phase 13 → AccountDeactivationTests (7 tests)
         → AccountDeletionTests (5 tests)
```

**Total: ~146 new tests** across 14 new test files + 3 infrastructure additions.

---

## 5. Running Tests

After implementing each phase, run:

```bash
dotnet test test/atompds.Tests/atompds.Tests.csproj --filter "FullyQualifiedName~atompds.Tests.PhaseName"
```

Or run all tests:

```bash
dotnet test test/atompds.Tests/atompds.Tests.csproj
```

Run full solution tests to verify no regressions:

```bash
dotnet test atompds.slnx
```

---

## 6. Validation Checklist for Each Phase

After implementing a phase, verify:

- [ ] All new tests pass (`dotnet test test/atompds.Tests/atompds.Tests.csproj`)
- [ ] All existing tests still pass (`dotnet test atompds.slnx`)
- [ ] No compilation warnings in test project (`dotnet build test/atompds.Tests/atompds.Tests.csproj`)
- [ ] Test names follow `<Method>_<Scenario>_<Expected>` pattern
- [ ] Each test is independent (creates its own account/data or uses class-level shared state with `[Order]`)
- [ ] Auth helpers are used consistently (no raw JWT construction in test methods)
- [ ] No test relies on network access (all external services mocked via `PDS_DEV_MODE=true`)
