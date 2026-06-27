# Configuration

_Last reviewed: 2026-04-29_

## Source of truth

Runtime configuration is bound from the `Config` section into:

- `src/atompds/Config/ServerEnvironment.cs`

It is then mapped and validated in:

- `src/atompds/Config/ServerConfig.cs`

## Required settings

At minimum, local development needs:

- `PDS_JWT_SECRET`
- `PDS_PLC_ROTATION_KEY_K256_PRIVATE_KEY_HEX`
- `PDS_BLOBSTORE_DISK_LOCATION`
- `PDS_BLOBSTORE_DISK_TMP_LOCATION`

## Common local-development settings

Start from `src/atompds/appsettings.Development.json.example`.

Typical fields:

- `PDS_HOSTNAME`
- `PDS_PORT`
- `PDS_DATA_DIRECTORY`
- blobstore disk paths
- AppView URL and DID
- crawler URLs
- metadata links such as privacy policy and support URL

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
dotnet build atompds.slnx
dotnet test --solution atompds.slnx
```
