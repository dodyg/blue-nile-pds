# Configuration

_Last reviewed: 2026-04-29_

## Source of truth

Runtime configuration is bound from the `Config` section into:

- `src/Host/Config/ServerEnvironment.cs`

It is then mapped and validated in:

- `src/Host/Config/ServerConfig.cs`

## Required settings

At minimum, local development needs:

- `PDS_JWT_SECRET`
- `PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX`
- `PDS_BLOBSTORE_DISK_LOCATION`
- `PDS_BLOBSTORE_DISK_TMP_LOCATION`

## Common local-development settings

Start from `src/Host/appsettings.Development.json.example`.

Typical fields:

- `PDS_HOSTNAME`
- `PDS_PORT`
- `PDS_DATA_DIRECTORY`
- blobstore disk paths
- AppView URL and DID
- crawler URLs
- metadata links such as privacy policy and support URL

## Account emails

The PDS sends the codes for email confirmation, email change, password reset, account deletion and identity (PLC) changes, plus the pending-signup and admin emails. Each email carries the code only, never a link, because the PDS serves accounts for any app and can't know which page should take the code.

| Setting | Default | Notes |
|---|---|---|
| `PDS_SMTP_HOST` | unset | Without it, emails are only logged (`StubMailer`) and nothing is sent. |
| `PDS_SMTP_PORT` | `587` | STARTTLS. |
| `PDS_SMTP_USERNAME`, `PDS_SMTP_PASSWORD` | unset | Keep the password out of committed files. |
| `PDS_SMTP_FROM_ADDRESS` | `noreply@{PDS_HOSTNAME}` | Use an address on a domain with SPF, DKIM and DMARC set up for the SMTP provider, or mail lands in spam. |
| `PDS_SERVICE_NAME` | `Blue Nile PDS` | The From display name, and shown in the subject, the header and the footer. Use a name, not a domain. |

The subject is `{CODE} is your {service} {purpose} code for {handle}`, so the code shows in notifications and codes for accounts that share an inbox are told apart. Every email names the account and gives the expiry (15 minutes) as a duration and a UTC time, counted from when the code was made. Codes are compared ignoring case and surrounding spaces.

## Safety-sensitive settings

### `PDS_DEV_MODE`

Development mode relaxes some expectations. In particular, if `PDS_ADMIN_PASSWORD` is not supplied and development mode is enabled, a default admin password is allowed.

Do not rely on this in production.

### `PDS_RATE_LIMITS_ENABLED`

Rate limiting is enabled by default and can be disabled for local testing.

Policies currently include:
- global per-IP limiter
- auth-sensitive limiter
- repo-write limiter

### Proxy / SSRF settings

Proxy settings are mapped through `ProxyConfig`.

Review carefully before enabling risky overrides such as SSRF-protection disablement or altered upstream timeout behavior.

## DID cache TTLs

The DID cache now uses consistent semantics:

- `PDS_DID_CACHE_STALE_TTL`
- `PDS_DID_CACHE_MAX_TTL`

Validation requires:

- stale TTL <= max TTL

Defaults are:
- stale TTL: 1 hour
- max TTL: 1 day

## Config field status notes

Most fields in `ServerEnvironment` are active. A few remain compatibility placeholders or are not currently wired end-to-end.

### Documented as currently unused / not materially wired

- `PDS_DID_CACHE_DB_LOCATION`
  - mapped into database config, but the current DID cache implementation is in-memory
- `PDS_FETCH_MAX_RESPONSE_SIZE`
  - present in environment binding, but current runtime behavior is driven by proxy settings instead
- `PDS_HCAPTCHA_SITE_KEY`
  - useful for front-end integration, but not consumed by server-side runtime logic in this repo

These are documented so operators do not assume they currently change server behavior.

## Recommended workflow

1. copy the development example file
2. fill in required secrets locally
3. keep production-only secrets out of source control
4. validate with:

```bash
dotnet build BlueNilePds.slnx
dotnet test --solution BlueNilePds.slnx
```
